namespace ISE.AccountObjectives;

/// <summary>Defines the business objective assigned to an account.</summary>
public enum ObjectiveMode
{
    /// <summary>Prioritize completing an evaluation within a planned number of trading days.</summary>
    PassEvaluation,
    /// <summary>Prioritize repeatable daily income while protecting a funded account.</summary>
    Income,
    /// <summary>Prioritize preservation after account or daily objectives are satisfied.</summary>
    CapitalPreservation
}
