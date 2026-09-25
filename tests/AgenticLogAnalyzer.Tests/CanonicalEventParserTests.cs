using AgenticLogAnalyzer.Application.Parsing;
using AgenticLogAnalyzer.Domain.Logs;

namespace AgenticLogAnalyzer.Tests;

public sealed class CanonicalEventParserTests
{
    private const string ValidLine =
        "2026-09-25T08:42:01Z|authentication|login|failure|john.doe|reader-01|192.168.1.50";

    private readonly CanonicalEventParser _parser = new();

    private static RawLog CreateRawLog(string content) =>
        new("test", "test-source", content, DateTimeOffset.UtcNow);

    [Fact]
    public void Parse_ValidLog_ReturnsCanonicalEvent()
    {
        var rawLog = CreateRawLog(ValidLine);

        var result = _parser.Parse(rawLog);

        Assert.Equal(new DateTimeOffset(2026, 9, 25, 8, 42, 1, TimeSpan.Zero), result.Timestamp);
        Assert.Equal("test", result.SourceType);
        Assert.Equal("test-source", result.SourceName);
        Assert.Equal("authentication", result.Category);
        Assert.Equal("login", result.Action);
        Assert.Equal("failure", result.Result);
        Assert.Equal("john.doe", result.User);
        Assert.Equal("reader-01", result.Device);
        Assert.Equal("192.168.1.50", result.SourceIp);
        Assert.Equal(rawLog.Content, result.RawContent);
    }

    [Fact]
    public void Parse_EmptyOptionalFields_ReturnsNulls()
    {
        var result = _parser.Parse(CreateRawLog("2026-09-25T08:42:01Z|system|boot| | | |"));

        Assert.Null(result.Result);
        Assert.Null(result.User);
        Assert.Null(result.Device);
        Assert.Null(result.SourceIp);
    }

    [Fact]
    public void Parse_SomeEmptyOptionalFields_KeepsNonEmptyValues()
    {
        var result = _parser.Parse(CreateRawLog("2026-09-25T08:42:01Z|authentication|login|||reader-01|"));

        Assert.Null(result.Result);
        Assert.Null(result.User);
        Assert.Equal("reader-01", result.Device);
        Assert.Null(result.SourceIp);
    }

    [Fact]
    public void Parse_TimestampWithoutOffset_AssumesUtc()
    {
        var result = _parser.Parse(CreateRawLog("2026-09-25 08:42:01|system|boot|||| "));

        Assert.Equal(TimeSpan.Zero, result.Timestamp.Offset);
        Assert.Equal(8, result.Timestamp.Hour);
    }

    [Theory]
    [InlineData("2026-09-25T08:42:01Z|authentication|login")]
    [InlineData("2026-09-25T08:42:01Z|a|b|c|d|e|f|extra")]
    public void Parse_WrongFieldCount_ThrowsFormatException(string content)
    {
        var rawLog = CreateRawLog(content);

        Assert.False(_parser.CanParse(rawLog));
        Assert.Throws<FormatException>(() => _parser.Parse(rawLog));
    }

    [Fact]
    public void Parse_InvalidTimestamp_ThrowsFormatException()
    {
        var rawLog = CreateRawLog("not-a-date|authentication|login|failure|john.doe|reader-01|192.168.1.50");

        Assert.True(_parser.CanParse(rawLog));
        Assert.Throws<FormatException>(() => _parser.Parse(rawLog));
    }

    [Fact]
    public void CanParse_ValidLog_ReturnsTrue()
    {
        Assert.True(_parser.CanParse(CreateRawLog(ValidLine)));
    }
}
