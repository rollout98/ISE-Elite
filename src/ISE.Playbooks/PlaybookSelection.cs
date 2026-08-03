using System;

namespace ISE.Playbooks;

/// <summary>Represents the selected playbook and the evidence supporting it.</summary>
public sealed class PlaybookSelection
{
    /// <summary>Initializes a playbook selection.</summary>
    public PlaybookSelection(PlaybookType playbook, decimal confidence, string reason)
    {
        if (confidence < 0m || confidence > 1m)
            throw new ArgumentOutOfRangeException(nameof(confidence));

        Playbook = playbook;
        Confidence = confidence;
        Reason = reason ?? throw new ArgumentNullException(nameof(reason));
    }

    /// <summary>Gets the selected playbook.</summary>
    public PlaybookType Playbook { get; }
    /// <summary>Gets confidence from zero to one.</summary>
    public decimal Confidence { get; }
    /// <summary>Gets an explainable selection reason.</summary>
    public string Reason { get; }
    /// <summary>Gets whether a playbook is eligible.</summary>
    public bool IsEligible => Playbook != PlaybookType.None;
}
