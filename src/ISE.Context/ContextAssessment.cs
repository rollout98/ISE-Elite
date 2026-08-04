using System;
using System.Collections.Generic;

namespace ISE.Context
{
    /// <summary>
    /// Describes the dominant market context and the evidence supporting it.
    /// </summary>
    public sealed class ContextAssessment
    {
        /// <summary>
        /// Creates a context assessment.
        /// </summary>
        public ContextAssessment(MarketContextType context, double confidence, bool actionable, string reason)
        {
            if (confidence < 0d || confidence > 1d)
            {
                throw new ArgumentOutOfRangeException(nameof(confidence));
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("A context reason is required.", nameof(reason));
            }

            Context = context;
            Confidence = confidence;
            IsActionable = actionable;
            Reasons = new[] { reason };
        }

        /// <summary>Gets the dominant context.</summary>
        public MarketContextType Context { get; }

        /// <summary>Gets classification confidence from zero to one.</summary>
        public double Confidence { get; }

        /// <summary>Gets whether the context is sufficiently strong for downstream evaluation.</summary>
        public bool IsActionable { get; }

        /// <summary>Gets explainable classification reasons.</summary>
        public IReadOnlyList<string> Reasons { get; }
    }
}
