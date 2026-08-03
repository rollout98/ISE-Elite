using System;
using ISE.TradePlanning;

namespace ISE.PositionManagement;

/// <summary>Creates deterministic instructions for managing an open position.</summary>
public sealed class PositionManagementEngine
{
    /// <summary>Evaluates the current position state and returns the next management instruction.</summary>
    public PositionManagementDecision Evaluate(PositionManagementInput input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));

        if (input.Direction != TradeDirection.Long && input.Direction != TradeDirection.Short)
            return new PositionManagementDecision(PositionManagementAction.None, PositionManagementReason.InvalidPosition, 0, input.OpenQuantity, null);

        if (input.OpenQuantity == 0)
            return new PositionManagementDecision(PositionManagementAction.None, PositionManagementReason.PositionClosed, 0, 0, null);

        if (input.RequestedExitQuantity > 0)
        {
            var remaining = input.OpenQuantity - input.RequestedExitQuantity;
            var action = remaining == 0 ? PositionManagementAction.ClosePosition : PositionManagementAction.PartialExit;
            var reason = remaining == 0 ? PositionManagementReason.PositionClosed : PositionManagementReason.PartialExitRequested;
            return new PositionManagementDecision(action, reason, input.RequestedExitQuantity, remaining, null);
        }

        if (input.TrailingStopCandidate.HasValue)
        {
            var candidate = input.TrailingStopCandidate.Value;
            if (!TightensRisk(input.Direction, input.CurrentStopPrice, candidate, input.CurrentPrice))
                return new PositionManagementDecision(PositionManagementAction.None, PositionManagementReason.RiskIncreaseRejected, 0, input.OpenQuantity, null);

            return new PositionManagementDecision(PositionManagementAction.MoveStop, PositionManagementReason.TrailingStopAccepted, 0, input.OpenQuantity, candidate);
        }

        var favorableMove = input.Direction == TradeDirection.Long
            ? input.CurrentPrice - input.AverageEntryPrice
            : input.AverageEntryPrice - input.CurrentPrice;

        if (favorableMove >= input.InitialRiskPerContract * input.BreakEvenTriggerMultiple)
        {
            var breakEvenStop = input.Direction == TradeDirection.Long
                ? input.AverageEntryPrice + input.BreakEvenOffset
                : input.AverageEntryPrice - input.BreakEvenOffset;

            if (TightensRisk(input.Direction, input.CurrentStopPrice, breakEvenStop, input.CurrentPrice))
                return new PositionManagementDecision(PositionManagementAction.MoveStop, PositionManagementReason.BreakEvenActivated, 0, input.OpenQuantity, breakEvenStop);
        }

        return new PositionManagementDecision(PositionManagementAction.None, PositionManagementReason.NoChange, 0, input.OpenQuantity, null);
    }

    private static bool TightensRisk(TradeDirection direction, decimal currentStop, decimal candidate, decimal currentPrice)
    {
        if (direction == TradeDirection.Long)
            return candidate > currentStop && candidate < currentPrice;

        return candidate < currentStop && candidate > currentPrice;
    }
}
