using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    /// <summary>
    /// Diagnostic-only V7.2 attribution record.
    ///
    /// Full-path and post-exit excursion fields deliberately use future bars and therefore
    /// MUST NOT be consumed by entry or live management decisions. They exist only to explain
    /// what the already-completed V7.1 management decision captured, protected, or left behind.
    /// </summary>
    public sealed class MorningPositionManagementAttributionObservation
    {
        public MorningPositionManagementAttributionObservation(
            MorningProtectedManagedTrade managedTrade,
            decimal fullPathMfeTicks,
            decimal fullPathMaeTicks,
            decimal postExitMfeTicks,
            decimal postExitMaeTicks,
            string fullPathMfeBand)
        {
            ManagedTrade = managedTrade ?? throw new ArgumentNullException(nameof(managedTrade));
            FullPathMfeTicks = Math.Max(0m, fullPathMfeTicks);
            FullPathMaeTicks = Math.Max(0m, fullPathMaeTicks);
            PostExitMfeTicks = Math.Max(0m, postExitMfeTicks);
            PostExitMaeTicks = Math.Max(0m, postExitMaeTicks);
            FullPathMfeBand = fullPathMfeBand ?? string.Empty;
        }

        public MorningProtectedManagedTrade ManagedTrade { get; }
        public MorningDailySequencingCandidate Candidate => ManagedTrade.Candidate;
        public MorningAdaptiveTradeOutcome Baseline => Candidate.Entry.Source.Source;

        public decimal BaselineRealizedDollars => Baseline.RealizedDollars;
        public decimal BaselineRealizedTicks => Baseline.RealizedTicks;
        public decimal ManagedRealizedDollars => ManagedTrade.RealizedDollars;
        public decimal ManagedRealizedTicks => ManagedTrade.RealizedTicks;
        public decimal ManagedDeltaDollars => ManagedRealizedDollars - BaselineRealizedDollars;

        public decimal FullPathMfeTicks { get; }
        public decimal FullPathMaeTicks { get; }
        public decimal PostExitMfeTicks { get; }
        public decimal PostExitMaeTicks { get; }
        public string FullPathMfeBand { get; }

        public decimal ManagedCaptureFraction =>
            FullPathMfeTicks <= 0m ? 0m : ManagedRealizedTicks / FullPathMfeTicks;

        public decimal BaselineCaptureFraction =>
            FullPathMfeTicks <= 0m ? 0m : BaselineRealizedTicks / FullPathMfeTicks;

        public bool PostExitReached150 => PostExitMfeTicks >= 150m;
        public bool PostExitReached300 => PostExitMfeTicks >= 300m;
        public bool PostExitReached500 => PostExitMfeTicks >= 500m;
    }

    /// <summary>
    /// V7.2 management attribution.
    ///
    /// This analyzer cannot create, rank, defer, select, or manage a trade. It only evaluates
    /// already-managed V7.1 trades after the fact. The full path extends from frozen entry to the
    /// 11:00 CT research-window end. The post-exit path starts strictly after the managed exit bar
    /// so same-bar intrabar ordering is not invented.
    /// </summary>
    public sealed class MorningPositionManagementAttributionAnalyzer
    {
        private static readonly TimeSpan ResearchWindowEnd = new TimeSpan(11, 0, 0);
        private readonly decimal tickSize;

        public MorningPositionManagementAttributionAnalyzer(decimal tickSize = 0.25m)
        {
            if (tickSize <= 0m) throw new ArgumentOutOfRangeException(nameof(tickSize));
            this.tickSize = tickSize;
        }

        public IReadOnlyList<MorningPositionManagementAttributionObservation> Analyze(
            IReadOnlyList<HistoricalBar> oneMinuteBars,
            IReadOnlyList<MorningProtectedManagedTrade> managedTrades)
        {
            if (oneMinuteBars == null) throw new ArgumentNullException(nameof(oneMinuteBars));
            if (managedTrades == null) throw new ArgumentNullException(nameof(managedTrades));

            if (managedTrades.Count == 0)
                return Array.Empty<MorningPositionManagementAttributionObservation>();

            var central = ResolveCentralTimeZone();
            var ordered = oneMinuteBars.OrderBy(x => x.TimestampUtc).ToList();
            var result = new List<MorningPositionManagementAttributionObservation>();

            foreach (var trade in managedTrades.OrderBy(x => x.Candidate.EntryUtc))
            {
                var source = trade.Candidate.Entry.Source.Source;
                var entryLocal = TimeZoneInfo.ConvertTime(source.EntryUtc, central).DateTime;

                var fullPath = ordered
                    .Where(x => x.TimestampUtc >= source.EntryUtc)
                    .TakeWhile(x =>
                    {
                        var local = TimeZoneInfo.ConvertTime(x.TimestampUtc, central).DateTime;
                        return local.Date == entryLocal.Date && local.TimeOfDay < ResearchWindowEnd;
                    })
                    .ToList();

                decimal fullMfe = 0m;
                decimal fullMae = 0m;
                foreach (var bar in fullPath)
                    UpdateExcursions(bar, source.Direction, source.EntryPrice, ref fullMfe, ref fullMae);

                decimal postMfe = 0m;
                decimal postMae = 0m;
                foreach (var bar in fullPath.Where(x => x.TimestampUtc > trade.ExitUtc))
                    UpdateExcursions(bar, source.Direction, source.EntryPrice, ref postMfe, ref postMae);

                result.Add(new MorningPositionManagementAttributionObservation(
                    trade,
                    fullMfe,
                    fullMae,
                    postMfe,
                    postMae,
                    MfeBand(fullMfe)));
            }

            return result;
        }

        public static string MfeBand(decimal ticks)
        {
            if (ticks < 100m) return "000-099";
            if (ticks < 150m) return "100-149";
            if (ticks < 300m) return "150-299";
            if (ticks < 500m) return "300-499";
            return "500+";
        }

        private void UpdateExcursions(
            HistoricalBar bar,
            NewYorkResearchDirection direction,
            decimal entryPrice,
            ref decimal mfe,
            ref decimal mae)
        {
            var favorable = direction == NewYorkResearchDirection.Long
                ? (bar.High - entryPrice) / tickSize
                : (entryPrice - bar.Low) / tickSize;

            var adverse = direction == NewYorkResearchDirection.Long
                ? (entryPrice - bar.Low) / tickSize
                : (bar.High - entryPrice) / tickSize;

            mfe = Math.Max(mfe, Math.Max(0m, favorable));
            mae = Math.Max(mae, Math.Max(0m, adverse));
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
