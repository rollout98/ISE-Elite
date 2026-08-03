using System;
using ISE.TradePlanning;
using Xunit;

namespace ISE.PositionManagement.Tests;

/// <summary>Tests deterministic position-management behavior.</summary>
public sealed class PositionManagementEngineTests
{
    /// <summary>Verifies break-even activation after sufficient favorable movement.</summary>
    [Fact]
    public void Long_position_moves_stop_to_break_even()
    {
        var input = Create(TradeDirection.Long, 3, 100m, 102.5m, 98m, 2m, 1m, 0.25m, null, 0);
        var decision = new PositionManagementEngine().Evaluate(input);

        Assert.Equal(PositionManagementAction.MoveStop, decision.Action);
        Assert.Equal(PositionManagementReason.BreakEvenActivated, decision.Reason);
        Assert.Equal(100.25m, decision.ReplacementStopPrice);
    }

    /// <summary>Verifies partial exits preserve the runner quantity.</summary>
    [Fact]
    public void Partial_exit_preserves_remaining_runner()
    {
        var input = Create(TradeDirection.Long, 3, 100m, 101m, 98m, 2m, 1m, 0m, null, 2);
        var decision = new PositionManagementEngine().Evaluate(input);

        Assert.Equal(PositionManagementAction.PartialExit, decision.Action);
        Assert.Equal(2, decision.ExitQuantity);
        Assert.Equal(1, decision.RemainingQuantity);
    }

    /// <summary>Verifies a looser stop is rejected after entry.</summary>
    [Fact]
    public void Stop_change_that_increases_risk_is_rejected()
    {
        var input = Create(TradeDirection.Long, 2, 100m, 101m, 98m, 2m, 1m, 0m, 97m, 0);
        var decision = new PositionManagementEngine().Evaluate(input);

        Assert.Equal(PositionManagementAction.None, decision.Action);
        Assert.Equal(PositionManagementReason.RiskIncreaseRejected, decision.Reason);
    }

    /// <summary>Verifies a valid short trailing stop is accepted.</summary>
    [Fact]
    public void Short_position_accepts_tighter_trailing_stop()
    {
        var input = Create(TradeDirection.Short, 2, 100m, 96m, 102m, 2m, 1m, 0m, 98m, 0);
        var decision = new PositionManagementEngine().Evaluate(input);

        Assert.Equal(PositionManagementAction.MoveStop, decision.Action);
        Assert.Equal(PositionManagementReason.TrailingStopAccepted, decision.Reason);
        Assert.Equal(98m, decision.ReplacementStopPrice);
    }

    private static PositionManagementInput Create(TradeDirection direction, int quantity, decimal entry, decimal current, decimal stop, decimal risk, decimal trigger, decimal offset, decimal? trailing, int exitQuantity)
    {
        return new PositionManagementInput(Guid.NewGuid(), direction, quantity, entry, current, stop, risk, trigger, offset, trailing, exitQuantity);
    }
}
