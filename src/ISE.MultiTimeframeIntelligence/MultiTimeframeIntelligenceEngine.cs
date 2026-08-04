using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.MultiTimeframeIntelligence
{
    public enum TimeframeDirection { Bearish = -1, Neutral = 0, Bullish = 1 }
    public enum TimeframeAlignment { AlignedBearish, Mixed, Neutral, AlignedBullish }
    public enum TimeframePosture { FullSize, ReducedSize, StandAside }

    public sealed class TimeframeEvidence
    {
        public TimeframeEvidence(string name, int minutes, TimeframeDirection direction,
            int trendStrength, int structureQuality, bool isTransitioning = false)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Value is required.", nameof(name));
            if (minutes < 1) throw new ArgumentOutOfRangeException(nameof(minutes));
            if (trendStrength < 0 || trendStrength > 100) throw new ArgumentOutOfRangeException(nameof(trendStrength));
            if (structureQuality < 0 || structureQuality > 100) throw new ArgumentOutOfRangeException(nameof(structureQuality));

            Name = name.Trim();
            Minutes = minutes;
            Direction = direction;
            TrendStrength = trendStrength;
            StructureQuality = structureQuality;
            IsTransitioning = isTransitioning;
        }

        public string Name { get; }
        public int Minutes { get; }
        public TimeframeDirection Direction { get; }
        public int TrendStrength { get; }
        public int StructureQuality { get; }
        public bool IsTransitioning { get; }
    }

    public sealed class MultiTimeframeDecision
    {
        public MultiTimeframeDecision(TimeframeAlignment alignment, TimeframeDirection controllingDirection,
            string controllingTimeframe, int confidence, TimeframePosture posture, IReadOnlyList<string> reasons)
        {
            Alignment = alignment;
            ControllingDirection = controllingDirection;
            ControllingTimeframe = controllingTimeframe;
            Confidence = confidence;
            Posture = posture;
            Reasons = reasons;
        }

        public TimeframeAlignment Alignment { get; }
        public TimeframeDirection ControllingDirection { get; }
        public string ControllingTimeframe { get; }
        public int Confidence { get; }
        public TimeframePosture Posture { get; }
        public IReadOnlyList<string> Reasons { get; }
    }

    public sealed class MultiTimeframeIntelligenceEngine
    {
        public MultiTimeframeDecision Evaluate(IEnumerable<TimeframeEvidence> evidence,
            bool authoritativeRiskBlock = false)
        {
            if (evidence == null) throw new ArgumentNullException(nameof(evidence));
            var frames = evidence.ToArray();
            if (frames.Length == 0) throw new ArgumentException("At least one timeframe is required.", nameof(evidence));

            if (authoritativeRiskBlock)
                return new MultiTimeframeDecision(TimeframeAlignment.Neutral, TimeframeDirection.Neutral,
                    string.Empty, 0, TimeframePosture.StandAside,
                    new[] { "Authoritative risk control requires the system to stand aside." });

            var ranked = frames
                .Select(x => new { Evidence = x, Weight = Weight(x) })
                .OrderByDescending(x => x.Weight)
                .ThenByDescending(x => x.Evidence.Minutes)
                .ToArray();

            var controller = ranked[0];
            decimal bullish = ranked.Where(x => x.Evidence.Direction == TimeframeDirection.Bullish).Sum(x => x.Weight);
            decimal bearish = ranked.Where(x => x.Evidence.Direction == TimeframeDirection.Bearish).Sum(x => x.Weight);
            decimal neutral = ranked.Where(x => x.Evidence.Direction == TimeframeDirection.Neutral).Sum(x => x.Weight);
            decimal directionalTotal = bullish + bearish;

            TimeframeAlignment alignment;
            if (directionalTotal == 0) alignment = TimeframeAlignment.Neutral;
            else if (bullish / directionalTotal >= 0.72m) alignment = TimeframeAlignment.AlignedBullish;
            else if (bearish / directionalTotal >= 0.72m) alignment = TimeframeAlignment.AlignedBearish;
            else alignment = TimeframeAlignment.Mixed;

            decimal dominant = Math.Max(bullish, bearish);
            decimal total = bullish + bearish + neutral;
            int confidence = total == 0 ? 0 : (int)Math.Round(100m * dominant / total, MidpointRounding.AwayFromZero);
            int transitions = frames.Count(x => x.IsTransitioning);
            confidence = Math.Max(0, confidence - transitions * 8);

            var direction = bullish > bearish ? TimeframeDirection.Bullish :
                bearish > bullish ? TimeframeDirection.Bearish : TimeframeDirection.Neutral;

            TimeframePosture posture = alignment == TimeframeAlignment.Mixed || transitions >= Math.Max(2, frames.Length / 2)
                ? TimeframePosture.ReducedSize
                : alignment == TimeframeAlignment.Neutral ? TimeframePosture.StandAside : TimeframePosture.FullSize;

            var reasons = new List<string>
            {
                $"{controller.Evidence.Name} is the controlling timeframe with the strongest weighted evidence.",
                $"Directional alignment is {alignment} with confidence {confidence}."
            };
            if (transitions > 0) reasons.Add($"{transitions} timeframe(s) are transitioning, reducing conviction.");
            if (posture == TimeframePosture.ReducedSize) reasons.Add("Cross-timeframe conflict requires reduced size.");

            return new MultiTimeframeDecision(alignment, direction, controller.Evidence.Name,
                confidence, posture, reasons);
        }

        private static decimal Weight(TimeframeEvidence evidence)
        {
            decimal horizon = 1m + Math.Min(240, evidence.Minutes) / 240m;
            decimal quality = (evidence.TrendStrength * 0.55m + evidence.StructureQuality * 0.45m) / 100m;
            decimal transitionPenalty = evidence.IsTransitioning ? 0.65m : 1m;
            return Math.Round(horizon * quality * transitionPenalty, 4, MidpointRounding.AwayFromZero);
        }
    }
}
