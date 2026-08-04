using System;

namespace ISE.TradeLifecycle;

/// <summary>Advances one trade through a deterministic lifecycle state machine.</summary>
public sealed class TradeLifecycleEngine
{
    /// <summary>Evaluates the next lifecycle action.</summary>
    public TradeLifecycleDecision Evaluate(TradeLifecycleInput input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        if (input.State == TradeLifecycleState.Closed)
            return Decision(input, TradeLifecycleState.Closed, TradeLifecycleAction.None, 0, input.StopPrice, "The trade is already closed.");

        if (input.EmergencyExit)
            return Decision(input, TradeLifecycleState.Closed, TradeLifecycleAction.Close, 0, input.StopPrice, "Emergency exit overrides all other lifecycle actions.");

        if (input.TargetReached)
            return Decision(input, TradeLifecycleState.Closed, TradeLifecycleAction.Close, 0, input.StopPrice, "The final target has been reached.");

        if (input.State == TradeLifecycleState.PendingEntry)
        {
            if (!input.EntryFilled)
                return Decision(input, input.State, TradeLifecycleAction.None, input.OpenQuantity, input.StopPrice, "The entry remains pending.");

            return Decision(input, TradeLifecycleState.Active, TradeLifecycleAction.Activate, input.OpenQuantity, input.StopPrice, "The entry filled and the position is now active.");
        }

        if (input.PartialExitRequested && input.PartialExitQuantity > 0)
        {
            var remaining = input.OpenQuantity - input.PartialExitQuantity;
            var state = remaining == 0 ? TradeLifecycleState.Closed : input.State;
            var action = remaining == 0 ? TradeLifecycleAction.Close : TradeLifecycleAction.PartialExit;
            return Decision(input, state, action, remaining, input.StopPrice, "The requested partial exit reduces the open position.");
        }

        var favorableMove = input.IsLong
            ? input.CurrentPrice - input.EntryPrice
            : input.EntryPrice - input.CurrentPrice;

        if (input.State == TradeLifecycleState.Active && input.BreakEvenTrigger > 0m && favorableMove >= input.BreakEvenTrigger)
        {
            var breakEvenStop = input.IsLong
                ? input.EntryPrice + input.BreakEvenOffset
                : input.EntryPrice - input.BreakEvenOffset;
            var improved = IsImprovedStop(input.IsLong, breakEvenStop, input.StopPrice) ? breakEvenStop : input.StopPrice;
            return Decision(input, TradeLifecycleState.BreakEvenProtected, TradeLifecycleAction.MoveStop, input.OpenQuantity, improved, "The break-even trigger was reached.");
        }

        if ((input.State == TradeLifecycleState.BreakEvenProtected || input.State == TradeLifecycleState.Trailing) && input.TrailingDistance > 0m)
        {
            var candidate = input.IsLong
                ? input.CurrentPrice - input.TrailingDistance
                : input.CurrentPrice + input.TrailingDistance;

            if (IsImprovedStop(input.IsLong, candidate, input.StopPrice))
                return Decision(input, TradeLifecycleState.Trailing, TradeLifecycleAction.MoveStop, input.OpenQuantity, candidate, "The trailing stop advances only in the favorable direction.");
        }

        return Decision(input, input.State, TradeLifecycleAction.None, input.OpenQuantity, input.StopPrice, "No lifecycle change is required.");
    }

    private static bool IsImprovedStop(bool isLong, decimal candidate, decimal current)
    {
        return isLong ? candidate > current : candidate < current;
    }

    private static TradeLifecycleDecision Decision(TradeLifecycleInput input, TradeLifecycleState state, TradeLifecycleAction action, int quantity, decimal stop, string reason)
    {
        return new TradeLifecycleDecision(state, action, quantity, stop, reason);
    }
}
