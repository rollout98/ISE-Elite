using System;

namespace ISE.Playbooks;

/// <summary>Selects the most specific eligible trading playbook from normalized evidence.</summary>
public sealed class PlaybookEngine
{
    /// <summary>Evaluates evidence and returns the highest-priority eligible playbook.</summary>
    public PlaybookSelection Evaluate(PlaybookInput input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        if (input.ConfirmationEvidence < 0.55m)
            return new PlaybookSelection(PlaybookType.None, input.ConfirmationEvidence, "Confirmation is below the minimum playbook threshold.");

        if (input.OpeningWindow && input.ReversalEvidence >= 0.75m)
            return Select(PlaybookType.OpeningReversal, input.ReversalEvidence, input.ConfirmationEvidence, "Opening-window rejection and reversal evidence are aligned.");

        if (input.LiquiditySweepEvidence >= 0.75m && input.ReversalEvidence >= 0.65m)
            return Select(PlaybookType.LiquiditySweepReversal, input.LiquiditySweepEvidence, input.ConfirmationEvidence, "Liquidity sweep and rejection evidence are aligned.");

        if (input.OpeningWindow && input.BreakoutEvidence >= 0.75m)
            return Select(PlaybookType.OpeningRangeBreakout, input.BreakoutEvidence, input.ConfirmationEvidence, "Opening-range breakout acceptance is confirmed.");

        if (input.BreakoutEvidence >= 0.70m && input.PullbackEvidence >= 0.60m)
            return Select(PlaybookType.BreakoutRetest, input.BreakoutEvidence, input.ConfirmationEvidence, "Breakout acceptance and retest quality are aligned.");

        if (input.TrendEvidence >= 0.70m && input.PullbackEvidence >= 0.65m)
            return Select(PlaybookType.PullbackContinuation, input.PullbackEvidence, input.ConfirmationEvidence, "Established trend and controlled pullback are aligned.");

        if (input.TrendEvidence >= 0.78m)
            return Select(PlaybookType.TrendContinuation, input.TrendEvidence, input.ConfirmationEvidence, "Directional trend persistence is strong.");

        if (input.RangeEvidence >= 0.72m && input.ExtensionEvidence >= 0.60m)
            return Select(PlaybookType.RangeFade, input.RangeEvidence, input.ConfirmationEvidence, "Range boundary extension supports rotation toward balance.");

        if (input.ExtensionEvidence >= 0.78m && input.ReversalEvidence >= 0.55m)
            return Select(PlaybookType.VwapReversion, input.ExtensionEvidence, input.ConfirmationEvidence, "Material extension and reversion evidence are aligned.");

        return new PlaybookSelection(PlaybookType.None, input.ConfirmationEvidence, "No playbook met its complete eligibility requirements.");
    }

    private static PlaybookSelection Select(PlaybookType playbook, decimal setupEvidence, decimal confirmation, string reason)
    {
        var confidence = Math.Min(1m, (setupEvidence * 0.65m) + (confirmation * 0.35m));
        return new PlaybookSelection(playbook, confidence, reason);
    }
}
