namespace AgenticLogAnalyzer.Infrastructure;

/// <summary>Resolves shared development data paths when apps start from different working directories.</summary>
public static class DevelopmentStoragePaths
{
    public static string GetDatabasePath(string? workingDirectory = null)
    {
        var root = FindRepositoryRoot(workingDirectory ?? Directory.GetCurrentDirectory());
        return Path.Combine(root, "data", "events.sqlite3");
    }

    public static IReadOnlyCollection<string> GetLegacyJsonLinesPaths(string? workingDirectory = null)
    {
        var currentDirectory = Path.GetFullPath(workingDirectory ?? Directory.GetCurrentDirectory());
        var root = FindRepositoryRoot(currentDirectory);
        return new[]
        {
            Path.Combine(currentDirectory, "data", "events.jsonl"),
            Path.Combine(root, "data", "events.jsonl"),
            Path.Combine(root, "apps", "api", "AgenticLogAnalyzer.Api", "data", "events.jsonl")
        }.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string FindRepositoryRoot(string directory)
    {
        DirectoryInfo? current = new(Path.GetFullPath(directory));
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Directory.Build.props")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Path.GetFullPath(directory);
    }
}
