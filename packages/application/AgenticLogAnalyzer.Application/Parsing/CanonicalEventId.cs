using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using AgenticLogAnalyzer.Domain.Logs;

namespace AgenticLogAnalyzer.Application.Parsing;

/// <summary>
/// Builds deterministic event IDs so that re-ingesting the same line from the same source yields the same ID
/// and storage can deduplicate it. The ID is a UUIDv8: the first 48 bits hold the event's Unix time in
/// milliseconds (like UUIDv7, so IDs stay time-ordered) and the rest comes from a SHA-256 of the source and line.
/// </summary>
public static class CanonicalEventId
{
    public static Guid Create(RawLog rawLog, DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(rawLog);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{rawLog.Source}\n{rawLog.SourceName}\n{rawLog.Content}"));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);

        Span<byte> time = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(time, timestamp.ToUnixTimeMilliseconds());
        time[2..].CopyTo(bytes);

        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes, bigEndian: true);
    }
}
