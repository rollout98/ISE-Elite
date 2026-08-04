using System;
using System.Collections.Generic;

namespace ISE.InstitutionalNarrative
{
    /// <summary>Identifies the dominant participant group.</summary>
    public enum ParticipantControl { Neutral, Buyers, Sellers }

    /// <summary>Describes the inferred inventory condition.</summary>
    public enum InventoryCondition { Neutral, Accumulation, Distribution, TrappedLongs, TrappedShorts }

    /// <summary>Describes the most important liquidity event.</summary>
    public enum LiquidityEvent { None, BuySideSweep, SellSideSweep, Absorption, LiquidityVacuum }

    /// <summary>Describes the auction response to the current area.</summary>
    public enum AuctionResponse { Balanced, AcceptanceHigher, AcceptanceLower, RejectionHigher, RejectionLower }

    /// <summary>Describes the expected directional outcome.</summary>
    public enum NarrativeExpectation { Neutral, ContinuationHigher, ContinuationLower, ReversalHigher, ReversalLower, StandAside }

    /// <summary>Normalized evidence consumed by the narrative engine.</summary>
    public sealed class InstitutionalNarrativeInput
    {
        /// <summary>Initializes a narrative input.</summary>
        public InstitutionalNarrativeInput(decimal directionalPressure, decimal absorption, decimal acceptance, decimal liquiditySweep, decimal contextConfidence, bool riskBlocked)
        {
            Validate(directionalPressure, nameof(directionalPressure));
            Validate(absorption, nameof(absorption));
            Validate(acceptance, nameof(acceptance));
            Validate(liquiditySweep, nameof(liquiditySweep));
            Validate(contextConfidence, nameof(contextConfidence));
            DirectionalPressure = directionalPressure;
            Absorption = absorption;
            Acceptance = acceptance;
            LiquiditySweep = liquiditySweep;
            ContextConfidence = contextConfidence;
            RiskBlocked = riskBlocked;
        }

        /// <summary>Gets signed directional pressure from -1 to 1.</summary>
        public decimal DirectionalPressure { get; }
        /// <summary>Gets signed absorption from -1 to 1.</summary>
        public decimal Absorption { get; }
        /// <summary>Gets signed acceptance from -1 to 1.</summary>
        public decimal Acceptance { get; }
        /// <summary>Gets signed liquidity-sweep evidence from -1 to 1.</summary>
        public decimal LiquiditySweep { get; }
        /// <summary>Gets context confidence from 0 to 1.</summary>
        public decimal ContextConfidence { get; }
        /// <summary>Gets whether an authoritative risk control blocks trading.</summary>
        public bool RiskBlocked { get; }

        private static void Validate(decimal value, string name)
        {
            if (name == nameof(ContextConfidence))
            {
                if (value < 0m || value > 1m) throw new ArgumentOutOfRangeException(name);
                return;
            }
            if (value < -1m || value > 1m) throw new ArgumentOutOfRangeException(name);
        }
    }

    /// <summary>Structured institutional market thesis.</summary>
    public sealed class InstitutionalNarrativeAssessment
    {
        /// <summary>Initializes an assessment.</summary>
        public InstitutionalNarrativeAssessment(ParticipantControl control, InventoryCondition inventory, LiquidityEvent liquidity, AuctionResponse auction, NarrativeExpectation expectation, decimal confidence, string invalidation, string thesis, IReadOnlyList<string> reasons)
        {
            Control = control;
            Inventory = inventory;
            Liquidity = liquidity;
            Auction = auction;
            Expectation = expectation;
            Confidence = confidence;
            Invalidation = invalidation ?? string.Empty;
            Thesis = thesis ?? string.Empty;
            Reasons = reasons ?? throw new ArgumentNullException(nameof(reasons));
        }

        /// <summary>Gets participant control.</summary>
        public ParticipantControl Control { get; }
        /// <summary>Gets inventory condition.</summary>
        public InventoryCondition Inventory { get; }
        /// <summary>Gets liquidity event.</summary>
        public LiquidityEvent Liquidity { get; }
        /// <summary>Gets auction response.</summary>
        public AuctionResponse Auction { get; }
        /// <summary>Gets directional expectation.</summary>
        public NarrativeExpectation Expectation { get; }
        /// <summary>Gets confidence from 0 to 1.</summary>
        public decimal Confidence { get; }
        /// <summary>Gets the thesis invalidation statement.</summary>
        public string Invalidation { get; }
        /// <summary>Gets the human-readable thesis.</summary>
        public string Thesis { get; }
        /// <summary>Gets supporting reasons.</summary>
        public IReadOnlyList<string> Reasons { get; }
    }

    /// <summary>Converts normalized market evidence into an explainable institutional thesis.</summary>
    public sealed class InstitutionalNarrativeEngine
    {
        /// <summary>Evaluates institutional narrative evidence.</summary>
        public InstitutionalNarrativeAssessment Evaluate(InstitutionalNarrativeInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            var reasons = new List<string>();
            if (input.RiskBlocked)
            {
                reasons.Add("Authoritative risk control requires standing aside.");
                return new InstitutionalNarrativeAssessment(ParticipantControl.Neutral, InventoryCondition.Neutral, LiquidityEvent.None, AuctionResponse.Balanced, NarrativeExpectation.StandAside, 1m, "Risk control must clear.", "Trading is blocked despite market evidence.", reasons);
            }

            var control = input.DirectionalPressure >= 0.25m ? ParticipantControl.Buyers : input.DirectionalPressure <= -0.25m ? ParticipantControl.Sellers : ParticipantControl.Neutral;
            var inventory = input.Absorption >= 0.35m ? InventoryCondition.Accumulation : input.Absorption <= -0.35m ? InventoryCondition.Distribution : InventoryCondition.Neutral;
            var liquidity = input.LiquiditySweep >= 0.4m ? LiquidityEvent.SellSideSweep : input.LiquiditySweep <= -0.4m ? LiquidityEvent.BuySideSweep : Math.Abs(input.Absorption) >= 0.55m ? LiquidityEvent.Absorption : LiquidityEvent.None;
            var auction = input.Acceptance >= 0.3m ? AuctionResponse.AcceptanceHigher : input.Acceptance <= -0.3m ? AuctionResponse.AcceptanceLower : input.DirectionalPressure > 0m ? AuctionResponse.RejectionLower : input.DirectionalPressure < 0m ? AuctionResponse.RejectionHigher : AuctionResponse.Balanced;

            NarrativeExpectation expectation;
            if (control == ParticipantControl.Buyers && auction == AuctionResponse.AcceptanceHigher) expectation = NarrativeExpectation.ContinuationHigher;
            else if (control == ParticipantControl.Sellers && auction == AuctionResponse.AcceptanceLower) expectation = NarrativeExpectation.ContinuationLower;
            else if (liquidity == LiquidityEvent.SellSideSweep && control != ParticipantControl.Sellers) expectation = NarrativeExpectation.ReversalHigher;
            else if (liquidity == LiquidityEvent.BuySideSweep && control != ParticipantControl.Buyers) expectation = NarrativeExpectation.ReversalLower;
            else expectation = NarrativeExpectation.Neutral;

            reasons.Add("Participant control: " + control + ".");
            reasons.Add("Inventory condition: " + inventory + ".");
            reasons.Add("Auction response: " + auction + ".");
            if (liquidity != LiquidityEvent.None) reasons.Add("Liquidity event: " + liquidity + ".");

            var confidence = Math.Min(1m, (Math.Abs(input.DirectionalPressure) + Math.Abs(input.Acceptance) + Math.Abs(input.Absorption) + input.ContextConfidence) / 4m);
            var thesis = BuildThesis(control, inventory, liquidity, auction, expectation);
            var invalidation = expectation == NarrativeExpectation.ContinuationHigher || expectation == NarrativeExpectation.ReversalHigher ? "Acceptance below the defended area invalidates the bullish thesis." : expectation == NarrativeExpectation.ContinuationLower || expectation == NarrativeExpectation.ReversalLower ? "Acceptance above the defended area invalidates the bearish thesis." : "A clear directional auction is required before action.";
            return new InstitutionalNarrativeAssessment(control, inventory, liquidity, auction, expectation, confidence, invalidation, thesis, reasons);
        }

        private static string BuildThesis(ParticipantControl control, InventoryCondition inventory, LiquidityEvent liquidity, AuctionResponse auction, NarrativeExpectation expectation)
        {
            return control + " control the auction; inventory is " + inventory + ", liquidity shows " + liquidity + ", and price demonstrates " + auction + ". The current expectation is " + expectation + ".";
        }
    }
}
