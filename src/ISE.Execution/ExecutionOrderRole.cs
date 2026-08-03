namespace ISE.Execution;

/// <summary>Identifies an order's responsibility within a trade.</summary>
public enum ExecutionOrderRole
{
    /// <summary>Opens the planned position.</summary>
    Entry,
    /// <summary>Protects the open position from adverse movement.</summary>
    ProtectiveStop,
    /// <summary>Closes the position at the planned profit objective.</summary>
    ProfitTarget
}
