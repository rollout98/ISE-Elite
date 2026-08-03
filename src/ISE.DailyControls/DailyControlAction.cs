namespace ISE.DailyControls;

/// <summary>Describes the account-level action permitted by the daily controls engine.</summary>
public enum DailyControlAction
{
    /// <summary>New trades may be initiated at normal approved risk.</summary>
    AllowTrading,

    /// <summary>New trades may continue only with reduced risk.</summary>
    ReduceRisk,

    /// <summary>No new trades may be initiated.</summary>
    StopTrading,

    /// <summary>The account should cancel working entries and flatten open positions.</summary>
    ForceFlat
}
