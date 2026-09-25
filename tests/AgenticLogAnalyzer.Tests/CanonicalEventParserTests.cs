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

    [Fact]
    public void Parse_TimestampLevelComponentFormat_ExtractsCanonicalFields()
    {
        var rawLog = CreateRawLog(
            "2026-09-25T17:00:30.118Z WARN  [security] Failed authentication attempt username=admin source=10.10.20.15");

        var result = _parser.Parse(rawLog);

        Assert.Equal("security", result.Category);
        Assert.Equal("Failed authentication attempt", result.Action);
        Assert.Equal("failure", result.Result);
        Assert.Equal("admin", result.User);
        Assert.Equal("10.10.20.15", result.SourceIp);
        Assert.Equal(rawLog.Content, result.RawContent);
    }

    [Fact]
    public void Parse_EscapedKeyAndQuotedValue_ExtractsStructuredAttributes()
    {
        var rawLog = CreateRawLog(
            "2026-09-25T17:00:23.118Z ERROR [database] Query execution failed error=\"timeout expired\" query\\_id=q_71291");

        var result = _parser.Parse(rawLog);

        Assert.Equal("database", result.Category);
        Assert.Equal("Query execution failed", result.Action);
        Assert.Equal("failure", result.Result);
    }

    [Fact]
    public void Parse_SameLineAndSource_ReturnsSameId()
    {
        var first = _parser.Parse(CreateRawLog(ValidLine));
        var second = _parser.Parse(CreateRawLog(ValidLine) with { ReceivedAt = DateTimeOffset.UnixEpoch });

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(8, first.Id.Version);
    }

    [Fact]
    public void Parse_SameLineFromAnotherSource_ReturnsDifferentId()
    {
        var first = _parser.Parse(CreateRawLog(ValidLine));
        var second = _parser.Parse(CreateRawLog(ValidLine) with { SourceName = "other-source" });

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void Parse_LaterTimestamp_ReturnsGreaterIdInBigEndianOrder()
    {
        var earlier = _parser.Parse(CreateRawLog("2026-09-25T08:42:01Z|system|boot| | | |"));
        var later = _parser.Parse(CreateRawLog("2026-09-25T08:42:02Z|system|boot| | | |"));

        Assert.True(earlier.Id.ToByteArray(bigEndian: true).AsSpan()
            .SequenceCompareTo(later.Id.ToByteArray(bigEndian: true)) < 0);
    }

    [Fact]
    public void Parse_StructuredLine_KeepsEveryAttribute()
    {
        var result = _parser.Parse(CreateRawLog(
            "2026-09-25T17:00:35.401Z INFO  [firewall] Connection accepted source=192.168.1.55 destination=10.10.0.20 port=443 protocol=TCP"));

        Assert.NotNull(result.Attributes);
        Assert.Equal("192.168.1.55", result.Attributes["source"]);
        Assert.Equal("10.10.0.20", result.Attributes["destination"]);
        Assert.Equal("443", result.Attributes["port"]);
        Assert.Equal("TCP", result.Attributes["protocol"]);
        Assert.Equal("192.168.1.55", result.SourceIp);
    }

    [Fact]
    public void Parse_QuotedValuesAndEscapedKeys_AreNormalizedInAttributes()
    {
        var result = _parser.Parse(CreateRawLog(
            "2026-09-25T17:00:23.118Z ERROR [database] Query execution failed error=\"timeout expired\" query\\_id=q_71291 Status=500"));

        Assert.NotNull(result.Attributes);
        Assert.Equal("timeout expired", result.Attributes["error"]);
        Assert.Equal("q_71291", result.Attributes["query_id"]);
        Assert.Equal("500", result.Attributes["status"]);
    }

    [Fact]
    public void Parse_RepeatedKey_KeepsLastValueInsteadOfFailing()
    {
        var result = _parser.Parse(CreateRawLog(
            "2026-09-25T17:00:23.118Z WARN  [api] Retry attempt=1 attempt=2"));

        Assert.Equal("2", result.Attributes?["attempt"]);
    }

    [Fact]
    public void Parse_PipeLineOrMessageWithoutPairs_HasNoAttributes()
    {
        Assert.Null(_parser.Parse(CreateRawLog(ValidLine)).Attributes);
        Assert.Null(_parser.Parse(CreateRawLog("2026-09-25T17:00:01.124Z INFO  [api-gateway] Server started on port 8080")).Attributes);
    }
}
