using AgenticLogAnalyzer.Application.Parsing;
using AgenticLogAnalyzer.Domain.Logs;

namespace AgenticLogAnalyzer.Tests;

public sealed class CanonicalEventParserTests
{
    [Fact]
    public void Parse_ValidLog_ReturnsCanonicalEvent()
    {
        var rawLog = new RawLog(
            "test",
            "2026-09-25T08:42:01Z|authentication|login|failure|john.doe|reader-01|192.168.1.50",
            DateTimeOffset.UtcNow);

        var parser = new CanonicalEventParser();

        var result = parser.Parse(rawLog);

        Assert.Equal("test", result.SourceType);
        Assert.Equal("authentication", result.Category);
        Assert.Equal("login", result.Action);
        Assert.Equal("failure", result.Result);
        Assert.Equal("john.doe", result.User);
        Assert.Equal("reader-01", result.Device);
        Assert.Equal("192.168.1.50", result.SourceIp);
        Assert.Equal(rawLog.Content, result.RawContent);
    }
}
