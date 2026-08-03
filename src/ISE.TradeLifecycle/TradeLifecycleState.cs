namespace ISE.TradeLifecycle;

/// <summary>Identifies the current state of a managed trade.</summary>
public enum TradeLifecycleState
{
    /// <summary>The trade is waiting for an entry fill.</summary>
    PendingEntry = 0,
    /// <summary>The trade is open with its initial protective stop.</summary>
    Active = 1,
    /// <summary>The trade has protected entry at break-even or better.</summary>
    BreakEvenProtected = 2,
    /// <summary>The trade is being managed by a favorable-direction trailing stop.</summary>
    Trailing = 3,
    /// <summary>The trade is fully closed.</summary>
    Closed = 4
}
