namespace ISE.TradeLifecycle;

/// <summary>Identifies the next lifecycle action.</summary>
public enum TradeLifecycleAction
{
    /// <summary>No lifecycle change is required.</summary>
    None = 0,
    /// <summary>Activate a newly filled position.</summary>
    Activate = 1,
    /// <summary>Move the protective stop.</summary>
    MoveStop = 2,
    /// <summary>Reduce part of the open position.</summary>
    PartialExit = 3,
    /// <summary>Close the entire remaining position.</summary>
    Close = 4
}
