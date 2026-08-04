using System;

namespace ISE.Context
{
    /// <summary>
    /// Supplies normalized evidence used to classify the current trading context.
    /// </summary>
    public sealed class ContextInput
    {
        /// <summary>
        /// Creates a context input.
        /// </summary>
        public ContextInput(
            double directionalStrength,
            double openingParticipation,
            double breakoutAcceptance,
            double pullbackQuality,
            double compression,
            double expansion,
            double reversalEvidence,
            double balance,
            bool isOpeningWindow,
            bool isSessionTransition,
            bool isReversalWindow)
        {
            DirectionalStrength = Validate(directionalStrength, nameof(directionalStrength));
            OpeningParticipation = Validate(openingParticipation, nameof(openingParticipation));
            BreakoutAcceptance = Validate(breakoutAcceptance, nameof(breakoutAcceptance));
            PullbackQuality = Validate(pullbackQuality, nameof(pullbackQuality));
            Compression = Validate(compression, nameof(compression));
            Expansion = Validate(expansion, nameof(expansion));
            ReversalEvidence = Validate(reversalEvidence, nameof(reversalEvidence));
            Balance = Validate(balance, nameof(balance));
            IsOpeningWindow = isOpeningWindow;
            IsSessionTransition = isSessionTransition;
            IsReversalWindow = isReversalWindow;
        }

        /// <summary>Gets directional conviction from zero to one.</summary>
        public double DirectionalStrength { get; }

        /// <summary>Gets participation quality around the open from zero to one.</summary>
        public double OpeningParticipation { get; }

        /// <summary>Gets breakout acceptance from zero to one.</summary>
        public double BreakoutAcceptance { get; }

        /// <summary>Gets trend-pullback quality from zero to one.</summary>
        public double PullbackQuality { get; }

        /// <summary>Gets compression evidence from zero to one.</summary>
        public double Compression { get; }

        /// <summary>Gets expansion evidence from zero to one.</summary>
        public double Expansion { get; }

        /// <summary>Gets reversal evidence from zero to one.</summary>
        public double ReversalEvidence { get; }

        /// <summary>Gets balanced-rotation evidence from zero to one.</summary>
        public double Balance { get; }

        /// <summary>Gets whether the market is inside the configured opening window.</summary>
        public bool IsOpeningWindow { get; }

        /// <summary>Gets whether the market is crossing a session boundary.</summary>
        public bool IsSessionTransition { get; }

        /// <summary>Gets whether the market is inside a configured reversal window.</summary>
        public bool IsReversalWindow { get; }

        private static double Validate(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d || value > 1d)
            {
                throw new ArgumentOutOfRangeException(name, "Normalized inputs must be between zero and one.");
            }

            return value;
        }
    }
}
