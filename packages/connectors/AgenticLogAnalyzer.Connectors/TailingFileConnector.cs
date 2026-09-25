using System.Runtime.CompilerServices;
using System.Text;
using AgenticLogAnalyzer.Application.Abstractions;
using AgenticLogAnalyzer.Domain.Logs;

namespace AgenticLogAnalyzer.Connectors;

/// <summary>
/// Reads a file from the start, then keeps polling it for appended lines until cancelled (like <c>tail -f</c>).
/// </summary>
/// <remarks>
/// A line is emitted only once its newline is written, so partially written lines are never split.
/// If the file is missing, the connector waits for it. If the file shrinks (truncation or rotation that
/// recreates it), reading restarts from the beginning; repeated lines are deduplicated by storage because
/// event IDs are deterministic. Each line's <see cref="RawLog.ReceivedAt"/> is the time it was read.
/// </remarks>
public sealed class TailingFileConnector(string path, TimeSpan pollInterval, TimeProvider? timeProvider = null)
    : ILogConnector
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public string Name => "file";

    public async IAsyncEnumerable<RawLog> ReadAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var chars = new char[Encoding.UTF8.GetMaxCharCount(buffer.Length)];
        var decoder = Encoding.UTF8.GetDecoder();
        var pending = new StringBuilder();
        long position = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(path))
            {
                await using var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    bufferSize: 4096,
                    useAsync: true);

                if (stream.Length < position)
                {
                    position = 0;
                    pending.Clear();
                    decoder.Reset();
                }

                stream.Seek(position, SeekOrigin.Begin);
                int read;
                while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    position += read;
                    var count = decoder.GetChars(buffer, 0, read, chars, 0);
                    for (var index = 0; index < count; index++)
                    {
                        if (chars[index] != '\n')
                        {
                            pending.Append(chars[index]);
                            continue;
                        }

                        var line = pending.ToString().TrimEnd('\r');
                        pending.Clear();
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            yield return new RawLog(Name, path, line, _timeProvider.GetUtcNow());
                        }
                    }
                }
            }

            await Task.Delay(pollInterval, _timeProvider, cancellationToken);
        }
    }
}
