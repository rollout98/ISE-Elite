using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    public enum MorningRiskControlPolicy
    {
        FixedTwo = 0,
        SizeTwoOrOne = 1,
        StrictTwoOneZero = 2
    }

    /// <summary>
    /// V7.6 diagnostic decomposition of Risk-layer behavior.
    ///
    /// Signal and management authority remain frozen:
    /// Entry Efficiency >=70, V5.6 Potential >=80, one position at a time,
    /// max two executed attempts, V7.3 management, unchanged structural stop.
    ///
    /// Policies:
    /// FixedTwo          = always 2 MNQ.
    /// SizeTwoOrOne      = 2 MNQ if two-contract risk <= objective, otherwise 1 MNQ.
    ///                     This policy is DIAGNOSTIC ONLY because 1 MNQ may still exceed the objective.
    /// StrictTwoOneZero  = V7.5 behavior: 2 / 1 / reject based on the risk objective.
    /// </summary>
    public sealed class MorningRiskControlDecompositionAnalyzer
    {
        private readonly decimal riskObjectiveDollars;
        private readonly decimal dollarsPerTickPerContract;
        private readonly MorningProtectedPositionIntelligenceAnalyzer manager;

        public MorningRiskControlDecompositionAnalyzer(
            decimal riskObjectiveDollars = 150m,
            decimal dollarsPerTickPerContract = 0.50m,
            MorningProtectedPositionConfig? managementConfig = null)
        {
            if (riskObjectiveDollars <= 0m)
                throw new ArgumentOutOfRangeException(nameof(riskObjectiveDollars));

            if (dollarsPerTickPerContract <= 0m)
                throw new ArgumentOutOfRangeException(nameof(dollarsPerTickPerContract));

            this.riskObjectiveDollars = riskObjectiveDollars;
            this.dollarsPerTickPerContract = dollarsPerTickPerContract;

            manager = new MorningProtectedPositionIntelligenceAnalyzer(
                managementConfig
                ?? new MorningProtectedPositionConfig(
                    enablePreExtensionAdaptiveBreakeven: false));
        }

        public MorningRiskSizedExecutionLifecycleResult Replay(
            IReadOnlyList<HistoricalBar> oneMinuteBars,
            IReadOnlyList<MorningDailySequencingCandidate> candidates,
            MorningRiskControlPolicy policy,
            int maximumAttempts = 2,
            decimal highEntryMinimum = 70m,
            decimal upperPotentialMinimum = 80m)
        {
            if (oneMinuteBars == null)
                throw new ArgumentNullException(nameof(oneMinuteBars));

            if (candidates == null)
                throw new ArgumentNullException(nameof(candidates));

            if (maximumAttempts < 1)
                throw new ArgumentOutOfRangeException(nameof(maximumAttempts));

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

                    var quantity = ResolveQuantity(
                        candidate.Entry.Source.Source.InitialRiskTicks,
                        policy);

                    if (quantity < 1)
                    {
                        riskRejected.Add(candidate);

                        // Risk rejection is not an execution attempt and creates no occupancy.
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

        public int ResolveQuantity(
            decimal initialRiskTicks,
            MorningRiskControlPolicy policy)
        {
            if (initialRiskTicks <= 0m)
                return 0;

            if (policy == MorningRiskControlPolicy.FixedTwo)
                return 2;

            var twoContractRisk =
                initialRiskTicks * dollarsPerTickPerContract * 2m;

            if (twoContractRisk <= riskObjectiveDollars)
                return 2;

            if (policy == MorningRiskControlPolicy.SizeTwoOrOne)
                return 1;

            return initialRiskTicks * dollarsPerTickPerContract
                <= riskObjectiveDollars
                    ? 1
                    : 0;
        }
    }
}
