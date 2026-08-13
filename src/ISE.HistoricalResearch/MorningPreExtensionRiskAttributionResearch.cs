using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    /// <summary>
    /// Diagnostic-only V7.4 risk attribution for already-selected V7.3 trades.
    ///
    /// No stop is moved, no entry threshold is changed, and no order-sizing rule is promoted here.
    /// The purpose is to determine whether structural risk can be respected by sizing 2 MNQ -> 1 MNQ,
    /// or whether a setup would still exceed the existing $150 risk objective even at 1 MNQ.
    /// </summary>
    public sealed class MorningPreExtensionRiskAttributionObservation
    {
        public MorningPreExtensionRiskAttributionObservation(
            MorningProtectedManagedTrade managedTrade,
            decimal riskObjectiveDollars,
            decimal dollarsPerTickPerContract,
            string riskBand,
            string entryTimeSegment)
        {
            ManagedTrade = managedTrade ?? throw new ArgumentNullException(nameof(managedTrade));
            if (riskObjectiveDollars <= 0m) throw new ArgumentOutOfRangeException(nameof(riskObjectiveDollars));
            if (dollarsPerTickPerContract <= 0m) throw new ArgumentOutOfRangeException(nameof(dollarsPerTickPerContract));

            RiskObjectiveDollars = riskObjectiveDollars;
            DollarsPerTickPerContract = dollarsPerTickPerContract;
            RiskBand = riskBand ?? string.Empty;
            EntryTimeSegment = entryTimeSegment ?? string.Empty;
        }

        public MorningProtectedManagedTrade ManagedTrade { get; }
        public MorningDailySequencingCandidate Candidate => ManagedTrade.Candidate;
        public MorningAdaptiveTradeOutcome Baseline => Candidate.Entry.Source.Source;

        public decimal RiskObjectiveDollars { get; }
        public decimal DollarsPerTickPerContract { get; }
        public decimal InitialRiskTicks => Baseline.InitialRiskTicks;
        public decimal RiskDollarsOneContract => InitialRiskTicks * DollarsPerTickPerContract;
        public decimal RiskDollarsTwoContracts => InitialRiskTicks * DollarsPerTickPerContract * 2m;
        public string RiskBand { get; }
        public string EntryTimeSegment { get; }

        public int MaximumContractsWithinRisk =>
            MorningPreExtensionRiskAttributionAnalyzer.MaximumContractsWithinRisk(
                InitialRiskTicks,
                RiskObjectiveDollars,
                DollarsPerTickPerContract,
                maximumContracts: 2);

        public bool TwoContractsWithinRisk => MaximumContractsWithinRisk >= 2;
        public bool OneContractWithinRisk => MaximumContractsWithinRisk >= 1;

        public decimal RiskSizedRealizedDollars =>
            ManagedTrade.RealizedTicks
            * DollarsPerTickPerContract
            * MaximumContractsWithinRisk;

        public decimal MfeRiskFraction =>
            InitialRiskTicks <= 0m
                ? 0m
                : ManagedTrade.MaxFavorableTicks / InitialRiskTicks;

        public bool IsPreExtension =>
            !ManagedTrade.ExtensionActivated;

        public bool IsStructuralStop =>
            ManagedTrade.ExitReason == MorningProtectedPositionExitReason.StructuralStop;

        public bool IsScalpTimeout =>
            ManagedTrade.ExitReason == MorningProtectedPositionExitReason.ScalpTimeout;
    }

    public sealed class MorningPreExtensionRiskAttributionAnalyzer
    {
        private readonly decimal riskObjectiveDollars;
        private readonly decimal dollarsPerTickPerContract;

        public MorningPreExtensionRiskAttributionAnalyzer(
            decimal riskObjectiveDollars = 150m,
            decimal dollarsPerTickPerContract = 0.50m)
        {
            if (riskObjectiveDollars <= 0m) throw new ArgumentOutOfRangeException(nameof(riskObjectiveDollars));
            if (dollarsPerTickPerContract <= 0m) throw new ArgumentOutOfRangeException(nameof(dollarsPerTickPerContract));

            this.riskObjectiveDollars = riskObjectiveDollars;
            this.dollarsPerTickPerContract = dollarsPerTickPerContract;
        }

        public IReadOnlyList<MorningPreExtensionRiskAttributionObservation> Analyze(
            IReadOnlyList<MorningProtectedManagedTrade> trades)
        {
            if (trades == null) throw new ArgumentNullException(nameof(trades));

            return trades
                .OrderBy(x => x.Candidate.EntryUtc)
                .Select(x => new MorningPreExtensionRiskAttributionObservation(
                    x,
                    riskObjectiveDollars,
                    dollarsPerTickPerContract,
                    RiskBand(x.Candidate.Entry.Source.Source.InitialRiskTicks),
                    EntryTimeSegmentCentral(x.Candidate.EntryUtc)))
                .ToList();
        }

        public static int MaximumContractsWithinRisk(
            decimal riskTicks,
            decimal riskObjectiveDollars = 150m,
            decimal dollarsPerTickPerContract = 0.50m,
            int maximumContracts = 2)
        {
            if (riskTicks <= 0m) return 0;
            if (riskObjectiveDollars <= 0m) throw new ArgumentOutOfRangeException(nameof(riskObjectiveDollars));
            if (dollarsPerTickPerContract <= 0m) throw new ArgumentOutOfRangeException(nameof(dollarsPerTickPerContract));
            if (maximumContracts < 1) throw new ArgumentOutOfRangeException(nameof(maximumContracts));

            var riskPerContract = riskTicks * dollarsPerTickPerContract;
            if (riskPerContract <= 0m) return 0;

            var affordable = (int)Math.Floor(riskObjectiveDollars / riskPerContract);
            if (affordable < 0) affordable = 0;
            if (affordable > maximumContracts) affordable = maximumContracts;
            return affordable;
        }

        public static string RiskBand(decimal riskTicks)
        {
            if (riskTicks <= 100m) return "<=100";
            if (riskTicks <= 150m) return "101-150";
            if (riskTicks <= 200m) return "151-200";
            if (riskTicks <= 300m) return "201-300";
            return "300+";
        }

        public static string EntryTimeSegmentCentral(DateTimeOffset entryUtc)
        {
            var central = ResolveCentralTimeZone();
            var time = TimeZoneInfo.ConvertTime(entryUtc, central).TimeOfDay;

            if (time < new TimeSpan(6, 0, 0)) return "03:00-05:59";
            if (time < new TimeSpan(8, 30, 0)) return "06:00-08:29";
            if (time < new TimeSpan(9, 30, 0)) return "08:30-09:29";
            return "09:30-10:59";
        }

        private static TimeZoneInfo ResolveCentralTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");
            }
        }
    }
}
