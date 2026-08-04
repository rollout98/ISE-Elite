namespace ISE.Context
{
    /// <summary>
    /// Classifies the dominant trading context from normalized market evidence.
    /// </summary>
    public sealed class ContextEngine
    {
        /// <summary>
        /// Evaluates the supplied context evidence.
        /// </summary>
        public ContextAssessment Evaluate(ContextInput input)
        {
            if (input == null)
            {
                throw new System.ArgumentNullException(nameof(input));
            }

            if (input.IsOpeningWindow && input.DirectionalStrength >= 0.75d && input.OpeningParticipation >= 0.75d)
            {
                return Create(MarketContextType.OpeningDrive, Average(input.DirectionalStrength, input.OpeningParticipation), "Strong directional participation is controlling the opening window.");
            }

            if (input.BreakoutAcceptance <= 0.30d && input.ReversalEvidence >= 0.70d)
            {
                return Create(MarketContextType.FailedBreakout, Average(1d - input.BreakoutAcceptance, input.ReversalEvidence), "Breakout acceptance failed while reversal evidence strengthened.");
            }

            if (input.IsReversalWindow && input.ReversalEvidence >= 0.65d)
            {
                return Create(MarketContextType.ReversalWindow, input.ReversalEvidence, "Reversal evidence is active inside the configured reversal window.");
            }

            if (input.IsSessionTransition && (input.Expansion >= 0.55d || input.ReversalEvidence >= 0.55d))
            {
                return Create(MarketContextType.SessionTransition, System.Math.Max(input.Expansion, input.ReversalEvidence), "Behavior is changing across a session boundary.");
            }

            if (input.DirectionalStrength >= 0.60d && input.PullbackQuality >= 0.65d)
            {
                return Create(MarketContextType.TrendPullback, Average(input.DirectionalStrength, input.PullbackQuality), "Directional structure remains intact during a qualified pullback.");
            }

            if (input.Compression >= 0.70d && input.Expansion < 0.45d)
            {
                return Create(MarketContextType.Compression, input.Compression, "Range and volatility are compressing.");
            }

            if (input.Expansion >= 0.70d && input.DirectionalStrength >= 0.60d)
            {
                return Create(MarketContextType.Expansion, Average(input.Expansion, input.DirectionalStrength), "Directional range and participation are expanding.");
            }

            if (input.Balance >= 0.65d)
            {
                return Create(MarketContextType.BalancedRotation, input.Balance, "Price is rotating around accepted value.");
            }

            return new ContextAssessment(MarketContextType.None, 0d, false, "No market context met the minimum evidence threshold.");
        }

        private static ContextAssessment Create(MarketContextType context, double confidence, string reason)
        {
            return new ContextAssessment(context, confidence, confidence >= 0.65d, reason);
        }

        private static double Average(double first, double second)
        {
            return (first + second) / 2d;
        }
    }
}
