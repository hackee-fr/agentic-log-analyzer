namespace AgenticLogAnalyzer.Desktop;

/// <summary>Per-user data location: %LOCALAPPDATA% (Windows), ~/Library/Application Support (macOS), ~/.local/share (Linux).</summary>
internal static class DesktopPaths
{
    public static string GetDataDirectory()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create),
            "AgenticLogAnalyzer");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
