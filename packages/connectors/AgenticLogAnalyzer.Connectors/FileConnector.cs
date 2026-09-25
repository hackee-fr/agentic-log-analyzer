using AgenticLogAnalyzer.Application.Abstractions;
using AgenticLogAnalyzer.Domain.Logs;

namespace AgenticLogAnalyzer.Connectors;

public sealed class FileConnector : ILogConnector
{
    private readonly string _path;

    public FileConnector(string path)
    {
        _path = path;
    }

    public string Name => "file";

    public async Task<IReadOnlyCollection<RawLog>> ReadAsync(
        CancellationToken cancellationToken)
    {
        var lines = await File.ReadAllLinesAsync(_path, cancellationToken);

        return lines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => new RawLog(Name, line, DateTimeOffset.UtcNow))
            .ToArray();
    }
}
