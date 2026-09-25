using AgenticLogAnalyzer.Agentic.Chat;

namespace AgenticLogAnalyzer.Tests;

public sealed class LlmAnswerVerifierTests
{
    private const string Facts = "3 failed authentication events for admin from 10.10.20.15 within ten minutes.";

    [Fact]
    public void Verify_FaithfulAnswer_HasNoIssues()
    {
        var issues = LlmAnswerVerifier.Verify(
            "La détection AUTH-001 signale 3 échecs pour admin depuis 10.10.20.15 : tentative de force brute à vérifier.",
            Facts,
            hasDetections: true);

        Assert.Empty(issues);
    }

    [Fact]
    public void Verify_UnknownIpAddress_IsReported()
    {
        var issues = LlmAnswerVerifier.Verify("L'attaque vient de 203.0.113.7.", Facts, hasDetections: true);

        Assert.Contains(issues, issue => issue.Contains("203.0.113.7", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Non, il n'y a pas d'indication d'attaque.")]
    [InlineData("Aucune menace n'a été observée.")]
    [InlineData("There is no sign of attack here.")]
    public void Verify_DenialWhileDetectionExists_IsReported(string answer)
    {
        Assert.NotEmpty(LlmAnswerVerifier.Verify(answer, Facts, hasDetections: true));
    }

    [Fact]
    public void Verify_DenialWithoutDetection_IsAccepted()
    {
        Assert.Empty(LlmAnswerVerifier.Verify("Aucune attaque détectée.", Facts, hasDetections: false));
    }
}
