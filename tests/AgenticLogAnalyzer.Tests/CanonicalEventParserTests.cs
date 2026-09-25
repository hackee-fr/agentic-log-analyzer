using AgenticLogAnalyzer.Application.Parsing;
using AgenticLogAnalyzer.Domain.Logs;
using Xunit;

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

    [Fact]
    public void Parse_InvalidFieldCount_ThrowsFormatException()
    {
        var rawLog = new RawLog(
            "test",
            "2026-09-25T08:42:01Z|authentication|login",
            DateTimeOffset.UtcNow);

        var parser = new CanonicalEventParser();

        Assert.Throws<FormatException>(
            () => parser.Parse(rawLog));
    }

    [Fact]
    public void Parse_InvalidTimestamp_ThrowsFormatException()
    {
        var rawLog = new RawLog(
            "test",
            "not-a-timestamp|authentication|login|failure|john.doe|reader-01|192.168.1.50",
            DateTimeOffset.UtcNow);

        var parser = new CanonicalEventParser();

        Assert.Throws<FormatException>(
            () => parser.Parse(rawLog));
    }

    [Fact]
    public void Parse_EmptyOptionalFields_ReturnsNull()
    {
        var rawLog = new RawLog(
            "test",
            "2026-09-25T08:42:01Z|authentication|login|||reader-01|",
            DateTimeOffset.UtcNow);

        var parser = new CanonicalEventParser();

        var result = parser.Parse(rawLog);

        Assert.Null(result.Result);
        Assert.Null(result.User);
        Assert.Equal("reader-01", result.Device);
        Assert.Null(result.SourceIp);
    }
}
