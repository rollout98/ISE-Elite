using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ISE.DecisionExplainability;

public enum ExplanationDecision
{
    None,
    Long,
    Short,
    Hold,
    Protect,
    Reduce,
    PromoteRunner,
    Exit,
    ForceExit
}

public enum EvidenceCategory
{
    Supporting,
    Blocking,
    Risk
}

public sealed class DecisionEvidence
{
    public DecisionEvidence(EvidenceCategory category, string source, string message)
    {
        Category = category;
        Source = string.IsNullOrWhiteSpace(source) ? throw new ArgumentException("Source is required.", nameof(source)) : source;
        Message = string.IsNullOrWhiteSpace(message) ? throw new ArgumentException("Message is required.", nameof(message)) : message;
    }

    public EvidenceCategory Category { get; }
    public string Source { get; }
    public string Message { get; }
}

public sealed class ExplainabilityInput
{
    public ExplainabilityInput(
        ExplanationDecision decision,
        int confidence,
        IEnumerable<DecisionEvidence> evidence,
        bool runnerActive = false,
        bool thesisInvalidated = false,
        bool authoritativeRiskOverride = false,
        bool endOfDayForceFlat = false)
    {
        if (confidence < 0 || confidence > 100)
            throw new ArgumentOutOfRangeException(nameof(confidence));

        Decision = decision;
        Confidence = confidence;
        Evidence = (evidence ?? throw new ArgumentNullException(nameof(evidence))).ToArray();
        RunnerActive = runnerActive;
        ThesisInvalidated = thesisInvalidated;
        AuthoritativeRiskOverride = authoritativeRiskOverride;
        EndOfDayForceFlat = endOfDayForceFlat;
    }

    public ExplanationDecision Decision { get; }
    public int Confidence { get; }
    public IReadOnlyList<DecisionEvidence> Evidence { get; }
    public bool RunnerActive { get; }
    public bool ThesisInvalidated { get; }
    public bool AuthoritativeRiskOverride { get; }
    public bool EndOfDayForceFlat { get; }
}

public sealed class DecisionExplanation
{
    public DecisionExplanation(
        ExplanationDecision decision,
        int confidence,
        IReadOnlyList<string> supportingEvidence,
        IReadOnlyList<string> blockingEvidence,
        IReadOnlyList<string> riskFactors,
        string summary,
        string recommendedAction,
        string machineReadable)
    {
        Decision = decision;
        Confidence = confidence;
        SupportingEvidence = supportingEvidence;
        BlockingEvidence = blockingEvidence;
        RiskFactors = riskFactors;
        Summary = summary;
        RecommendedAction = recommendedAction;
        MachineReadable = machineReadable;
    }

    public ExplanationDecision Decision { get; }
    public int Confidence { get; }
    public IReadOnlyList<string> SupportingEvidence { get; }
    public IReadOnlyList<string> BlockingEvidence { get; }
    public IReadOnlyList<string> RiskFactors { get; }
    public string Summary { get; }
    public string RecommendedAction { get; }
    public string MachineReadable { get; }
}

public sealed class DecisionExplainabilityEngine
{
    public DecisionExplanation Explain(ExplainabilityInput input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));

        var supporting = Format(input.Evidence, EvidenceCategory.Supporting);
        var blocking = Format(input.Evidence, EvidenceCategory.Blocking);
        var risks = Format(input.Evidence, EvidenceCategory.Risk);
        var summary = BuildSummary(input, supporting.Count, blocking.Count, risks.Count);
        var action = BuildRecommendedAction(input);
        var machineReadable = BuildMachineReadable(input, supporting, blocking, risks, summary, action);

        return new DecisionExplanation(input.Decision, input.Confidence, supporting, blocking, risks, summary, action, machineReadable);
    }

    private static IReadOnlyList<string> Format(IEnumerable<DecisionEvidence> evidence, EvidenceCategory category)
        => evidence.Where(item => item.Category == category)
            .OrderBy(item => item.Source, StringComparer.Ordinal)
            .ThenBy(item => item.Message, StringComparer.Ordinal)
            .Select(item => item.Source + ": " + item.Message)
            .ToArray();

    private static string BuildSummary(ExplainabilityInput input, int supporting, int blocking, int risks)
    {
        if (input.EndOfDayForceFlat || input.Decision == ExplanationDecision.ForceExit)
            return "The position must be closed to comply with the authoritative end-of-day flat rule.";
        if (input.AuthoritativeRiskOverride)
            return "Authoritative risk controls override all supporting market evidence.";
        if (input.ThesisInvalidated || input.Decision == ExplanationDecision.Exit)
            return "The original trade thesis is no longer valid and the position should be exited.";
        if (input.RunnerActive || input.Decision == ExplanationDecision.PromoteRunner)
            return "Trend persistence supports continued runner management while risk remains controlled.";

        return string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "Decision {0} has {1} supporting, {2} blocking, and {3} risk evidence items.",
            input.Decision, supporting, blocking, risks);
    }

    private static string BuildRecommendedAction(ExplainabilityInput input)
    {
        if (input.EndOfDayForceFlat || input.Decision == ExplanationDecision.ForceExit)
            return "Flatten the position immediately and cancel working entry orders.";
        if (input.AuthoritativeRiskOverride)
            return "Do not enter; exit any open position required by the risk control.";
        if (input.ThesisInvalidated || input.Decision == ExplanationDecision.Exit)
            return "Exit the position and block additional entries until the thesis is rebuilt.";

        return input.Decision switch
        {
            ExplanationDecision.Long => "Enter or maintain a long position under configured risk limits.",
            ExplanationDecision.Short => "Enter or maintain a short position under configured risk limits.",
            ExplanationDecision.Hold => "Hold the current position and continue supervision.",
            ExplanationDecision.Protect => "Protect open profit without invalidating normal market structure.",
            ExplanationDecision.Reduce => "Reduce exposure while retaining only the justified position size.",
            ExplanationDecision.PromoteRunner => "Promote the trade to runner management and trail structurally.",
            _ => "Stand aside until a qualified decision is available."
        };
    }

    private static string BuildMachineReadable(
        ExplainabilityInput input,
        IReadOnlyList<string> supporting,
        IReadOnlyList<string> blocking,
        IReadOnlyList<string> risks,
        string summary,
        string action)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        AppendProperty(builder, "decision", input.Decision.ToString());
        builder.Append(',');
        builder.Append("\"confidence\":").Append(input.Confidence);
        builder.Append(',');
        AppendArray(builder, "supportingEvidence", supporting);
        builder.Append(',');
        AppendArray(builder, "blockingEvidence", blocking);
        builder.Append(',');
        AppendArray(builder, "riskFactors", risks);
        builder.Append(',');
        AppendProperty(builder, "summary", summary);
        builder.Append(',');
        AppendProperty(builder, "recommendedAction", action);
        builder.Append('}');
        return builder.ToString();
    }

    private static void AppendProperty(StringBuilder builder, string name, string value)
        => builder.Append('"').Append(Escape(name)).Append("\":\"").Append(Escape(value)).Append('"');

    private static void AppendArray(StringBuilder builder, string name, IReadOnlyList<string> values)
    {
        builder.Append('"').Append(Escape(name)).Append("\":[");
        for (var index = 0; index < values.Count; index++)
        {
            if (index > 0) builder.Append(',');
            builder.Append('"').Append(Escape(values[index])).Append('"');
        }
        builder.Append(']');
    }

    private static string Escape(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
}
