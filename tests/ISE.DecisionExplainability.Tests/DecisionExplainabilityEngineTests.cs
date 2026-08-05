using ISE.DecisionExplainability;
using Xunit;

namespace ISE.DecisionExplainability.Tests;

public sealed class DecisionExplainabilityEngineTests
{
    private readonly DecisionExplainabilityEngine _engine = new();

    [Fact]
    public void EntryExplanationIncludesSupportingEvidence()
    {
        var result = _engine.Explain(Input(ExplanationDecision.Long, 91,
            Support("MarketNarrative", "Bullish expansion"),
            Support("Pullback", "Healthy retracement")));

        Assert.Equal(2, result.SupportingEvidence.Count);
        Assert.Contains("MarketNarrative: Bullish expansion", result.SupportingEvidence);
        Assert.Contains("long position", result.RecommendedAction);
    }

    [Fact]
    public void BlockingEvidenceIsSeparatedFromSupport()
    {
        var result = _engine.Explain(Input(ExplanationDecision.Long, 68,
            Support("ORB", "Breakout confirmed"),
            Block("Liquidity", "Overhead liquidity remains")));

        Assert.Single(result.BlockingEvidence);
        Assert.Empty(result.RiskFactors);
        Assert.Equal(68, result.Confidence);
    }

    [Fact]
    public void RiskFactorsAppearInExplanation()
    {
        var result = _engine.Explain(Input(ExplanationDecision.Protect, 74,
            Risk("AdaptiveRisk", "Daily loss pressure increased")));

        Assert.Single(result.RiskFactors);
        Assert.Contains("AdaptiveRisk", result.RiskFactors[0]);
    }

    [Fact]
    public void RunnerDecisionProducesRunnerNarrative()
    {
        var result = _engine.Explain(new ExplainabilityInput(
            ExplanationDecision.PromoteRunner, 94,
            new[] { Support("Runner", "Trend persistence is elite") }, runnerActive: true));

        Assert.Contains("runner management", result.Summary);
        Assert.Contains("trail structurally", result.RecommendedAction);
    }

    [Fact]
    public void ExitDecisionExplainsThesisFailure()
    {
        var result = _engine.Explain(new ExplainabilityInput(
            ExplanationDecision.Exit, 96,
            new[] { Block("MarketNarrative", "Institutional reversal confirmed") },
            thesisInvalidated: true));

        Assert.Contains("no longer valid", result.Summary);
        Assert.Contains("Exit the position", result.RecommendedAction);
    }

    [Fact]
    public void ForceFlatReferencesEndOfDayRule()
    {
        var result = _engine.Explain(new ExplainabilityInput(
            ExplanationDecision.ForceExit, 100,
            new[] { Risk("SessionControl", "Five minutes remain before 3:00 PM CT") },
            endOfDayForceFlat: true));

        Assert.Contains("end-of-day flat rule", result.Summary);
        Assert.Contains("Flatten the position immediately", result.RecommendedAction);
    }

    [Fact]
    public void AuthoritativeRiskOverrideTakesPrecedence()
    {
        var result = _engine.Explain(new ExplainabilityInput(
            ExplanationDecision.Long, 88,
            new[] { Support("ORB", "Strong breakout"), Risk("Risk", "Account locked") },
            authoritativeRiskOverride: true));

        Assert.Contains("override", result.Summary);
        Assert.Contains("Do not enter", result.RecommendedAction);
    }

    [Fact]
    public void MachineReadableOutputIsDeterministic()
    {
        var input = Input(ExplanationDecision.Hold, 87,
            Support("Runner", "Confirmed"),
            Risk("Risk", "Normal"),
            Block("Liquidity", "Minor resistance"));

        var first = _engine.Explain(input).MachineReadable;
        var second = _engine.Explain(input).MachineReadable;

        Assert.Equal(first, second);
        Assert.Equal("{\"decision\":\"Hold\",\"confidence\":87,\"supportingEvidence\":[\"Runner: Confirmed\"],\"blockingEvidence\":[\"Liquidity: Minor resistance\"],\"riskFactors\":[\"Risk: Normal\"],\"summary\":\"Decision Hold has 1 supporting, 1 blocking, and 1 risk evidence items.\",\"recommendedAction\":\"Hold the current position and continue supervision.\"}", first);
    }

    private static ExplainabilityInput Input(ExplanationDecision decision, int confidence, params DecisionEvidence[] evidence)
        => new(decision, confidence, evidence);

    private static DecisionEvidence Support(string source, string message)
        => new(EvidenceCategory.Supporting, source, message);

    private static DecisionEvidence Block(string source, string message)
        => new(EvidenceCategory.Blocking, source, message);

    private static DecisionEvidence Risk(string source, string message)
        => new(EvidenceCategory.Risk, source, message);
}
