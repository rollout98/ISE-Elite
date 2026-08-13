using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    public sealed class MorningRiskSizedTrade
    {
        public MorningRiskSizedTrade(
            MorningProtectedManagedTrade managedTrade,
            int quantity,
            decimal dollarsPerTickPerContract,
            decimal riskObjectiveDollars)
        {
            ManagedTrade = managedTrade ?? throw new ArgumentNullException(nameof(managedTrade));
            if (quantity < 1) throw new ArgumentOutOfRangeException(nameof(quantity));
            if (dollarsPerTickPerContract <= 0m) throw new ArgumentOutOfRangeException(nameof(dollarsPerTickPerContract));
            if (riskObjectiveDollars <= 0m) throw new ArgumentOutOfRangeException(nameof(riskObjectiveDollars));

            Quantity = quantity;
            DollarsPerTickPerContract = dollarsPerTickPerContract;
            RiskObjectiveDollars = riskObjectiveDollars;
        }

        public MorningProtectedManagedTrade ManagedTrade { get; }
        public MorningDailySequencingCandidate Candidate => ManagedTrade.Candidate;
        public int Quantity { get; }
        public decimal DollarsPerTickPerContract { get; }
        public decimal RiskObjectiveDollars { get; }

        public decimal InitialRiskTicks => Candidate.Entry.Source.Source.InitialRiskTicks;
        public decimal PlannedRiskDollars =>
            InitialRiskTicks * DollarsPerTickPerContract * Quantity;

        public decimal RealizedTicks => ManagedTrade.RealizedTicks;
        public decimal RealizedDollars =>
            RealizedTicks * DollarsPerTickPerContract * Quantity;

        public DateTimeOffset ExitUtc => ManagedTrade.ExitUtc;
        public MorningProtectedPositionExitReason ExitReason => ManagedTrade.ExitReason;
        public MorningProtectedPositionMode FinalMode => ManagedTrade.FinalMode;
    }

    public sealed class MorningRiskSizedExecutionLifecycleResult
    {
        public MorningRiskSizedExecutionLifecycleResult(
            IReadOnlyList<MorningRiskSizedTrade> selectedTrades,
            IReadOnlyList<MorningDailySequencingCandidate> riskRejectedCandidates,
            int rejectedPositionOpen,
            int rejectedAttemptLimit,
            int rejectedEntryQuality,
            int rejectedPotential)
        {
            SelectedTrades = selectedTrades ?? throw new ArgumentNullException(nameof(selectedTrades));
            RiskRejectedCandidates = riskRejectedCandidates ?? throw new ArgumentNullException(nameof(riskRejectedCandidates));
            RejectedPositionOpen = rejectedPositionOpen;
            RejectedAttemptLimit = rejectedAttemptLimit;
            RejectedEntryQuality = rejectedEntryQuality;
            RejectedPotential = rejectedPotential;
        }

        public IReadOnlyList<MorningRiskSizedTrade> SelectedTrades { get; }
        public IReadOnlyList<MorningDailySequencingCandidate> RiskRejectedCandidates { get; }
        public int RejectedRisk => RiskRejectedCandidates.Count;
        public int RejectedPositionOpen { get; }
        public int RejectedAttemptLimit { get; }
        public int RejectedEntryQuality { get; }
        public int RejectedPotential { get; }
    }

    /// <summary>
    /// V7.5 execution-realistic risk-sized lifecycle.
    ///
    /// Frozen signal/management authority:
    /// - Entry Efficiency >= 70
    /// - V5.6 Potential >= 80
    /// - one position at a time
    /// - maximum two executed attempts per session
    /// - V7.3 management with pre-extension BE disabled
    ///
    /// New Risk-layer behavior only:
    /// - preserve the structural stop;
    /// - size at 2 MNQ when structural risk fits the $150 objective;
    /// - reduce to 1 MNQ when 2 does not fit but 1 does;
    /// - risk-reject when even 1 MNQ exceeds the objective;
    /// - risk rejection consumes neither an attempt nor position occupancy.
    ///
    /// This analyzer does not move stops and does not change entry, Potential, Core, or Runner thresholds.
    /// </summary>
    public sealed class MorningRiskSizedExecutionLifecycleAnalyzer
    {
        private readonly decimal riskObjectiveDollars;
        private readonly decimal dollarsPerTickPerContract;
        private readonly int maximumContracts;
        private readonly MorningProtectedPositionIntelligenceAnalyzer manager;

        public MorningRiskSizedExecutionLifecycleAnalyzer(
            decimal riskObjectiveDollars = 150m,
            decimal dollarsPerTickPerContract = 0.50m,
            int maximumContracts = 2,
            MorningProtectedPositionConfig? managementConfig = null)
        {
            if (riskObjectiveDollars <= 0m) throw new ArgumentOutOfRangeException(nameof(riskObjectiveDollars));
            if (dollarsPerTickPerContract <= 0m) throw new ArgumentOutOfRangeException(nameof(dollarsPerTickPerContract));
            if (maximumContracts < 1) throw new ArgumentOutOfRangeException(nameof(maximumContracts));

            this.riskObjectiveDollars = riskObjectiveDollars;
            this.dollarsPerTickPerContract = dollarsPerTickPerContract;
            this.maximumContracts = maximumContracts;

            manager = new MorningProtectedPositionIntelligenceAnalyzer(
                managementConfig
                ?? new MorningProtectedPositionConfig(
                    enablePreExtensionAdaptiveBreakeven: false));
        }

        public MorningRiskSizedExecutionLifecycleResult Replay(
            IReadOnlyList<HistoricalBar> oneMinuteBars,
            IReadOnlyList<MorningDailySequencingCandidate> candidates,
            int maximumAttempts = 2,
            decimal highEntryMinimum = 70m,
            decimal upperPotentialMinimum = 80m)
        {
            if (oneMinuteBars == null) throw new ArgumentNullException(nameof(oneMinuteBars));
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (maximumAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maximumAttempts));

            var selected = new List<MorningRiskSizedTrade>();
            var riskRejected = new List<MorningDailySequencingCandidate>();
            var positionOpenRejects = 0;
            var attemptRejects = 0;
            var entryRejects = 0;
            var potentialRejects = 0;

            foreach (var day in candidates
                .OrderBy(x => x.EntryUtc)
                .GroupBy(x => x.SessionDateCentral)
                .OrderBy(x => x.Key))
            {
                var attempts = 0;
                DateTimeOffset? openUntil = null;

                foreach (var candidate in day.OrderBy(x => x.EntryUtc))
                {
                    if (openUntil.HasValue && candidate.EntryUtc < openUntil.Value)
                    {
                        positionOpenRejects++;
                        continue;
                    }

                    if (attempts >= maximumAttempts)
                    {
                        attemptRejects++;
                        continue;
                    }

                    if (candidate.EntryEfficiencyScore < highEntryMinimum)
                    {
                        entryRejects++;
                        continue;
                    }

                    if (candidate.PotentialScore < upperPotentialMinimum)
                    {
                        potentialRejects++;
                        continue;
                    }

                    var quantity = MorningPreExtensionRiskAttributionAnalyzer
                        .MaximumContractsWithinRisk(
                            candidate.Entry.Source.Source.InitialRiskTicks,
                            riskObjectiveDollars,
                            dollarsPerTickPerContract,
                            maximumContracts);

                    if (quantity < 1)
                    {
                        riskRejected.Add(candidate);
                        continue;
                    }

                    var managed = manager.Manage(oneMinuteBars, candidate);
                    if (managed == null)
                        continue;

                    attempts++;
                    selected.Add(new MorningRiskSizedTrade(
                        managed,
                        quantity,
                        dollarsPerTickPerContract,
                        riskObjectiveDollars));

                    openUntil = managed.ExitUtc;
                }
            }

            return new MorningRiskSizedExecutionLifecycleResult(
                selected,
                riskRejected,
                positionOpenRejects,
                attemptRejects,
                entryRejects,
                potentialRejects);
        }
    }
}
