using System.Text.Json;
using AgenticLogAnalyzer.Application.Abstractions;
using AgenticLogAnalyzer.Domain.Logs;

namespace AgenticLogAnalyzer.Infrastructure;

/// <summary>Stores canonical events as one JSON object per line.</summary>
public sealed class JsonLinesEventRepository(string path) : IEventRepository, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _path = Path.GetFullPath(path);

    public void Dispose()
    {
        _gate.Dispose();
        GC.SuppressFinalize(this);
    }

    public async Task SaveAsync(CanonicalEvent canonicalEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(canonicalEvent);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            await using var stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, true);
            await JsonSerializer.SerializeAsync(stream, canonicalEvent, JsonOptions, cancellationToken);
            await stream.WriteAsync("\n"u8.ToArray(), cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyCollection<CanonicalEvent>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_path))
            {
                return [];
            }

            var events = new List<CanonicalEvent>();
            await foreach (var line in File.ReadLinesAsync(_path, cancellationToken))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var item = JsonSerializer.Deserialize<CanonicalEvent>(line, JsonOptions);
                if (item is not null && Matches(item, query))
                {
                    events.Add(item);
                }
            }

            // Appends are not deduplicated; re-ingested lines share a deterministic ID, so keep the latest copy.
            return events
                .AsEnumerable()
                .Reverse()
                .DistinctBy(item => item.Id)
                .OrderByDescending(item => item.Timestamp)
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<int> DeleteAsync(string? sourceName, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_path))
            {
                return 0;
            }

            var kept = new List<string>();
            var deletedIds = new HashSet<Guid>();
            await foreach (var line in File.ReadLinesAsync(_path, cancellationToken))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var item = JsonSerializer.Deserialize<CanonicalEvent>(line, JsonOptions);
                if (item is not null && (sourceName is null || string.Equals(item.SourceName, sourceName, StringComparison.Ordinal)))
                {
                    deletedIds.Add(item.Id);
                    continue;
                }

                kept.Add(line);
            }

            if (deletedIds.Count == 0)
            {
                return 0;
            }

            var temporaryPath = _path + ".tmp";
            await File.WriteAllLinesAsync(temporaryPath, kept, cancellationToken);
            File.Move(temporaryPath, _path, overwrite: true);
            return deletedIds.Count;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static bool Matches(CanonicalEvent item, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return item.RawContent.Contains(query, StringComparison.OrdinalIgnoreCase)
            || item.Category.Contains(query, StringComparison.OrdinalIgnoreCase)
            || item.Action.Contains(query, StringComparison.OrdinalIgnoreCase)
            || (item.Result?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
            || (item.User?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
            || (item.Device?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
            || (item.SourceIp?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
    }
}
