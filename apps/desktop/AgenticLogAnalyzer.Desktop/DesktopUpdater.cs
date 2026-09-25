using Velopack;
using Velopack.Sources;

namespace AgenticLogAnalyzer.Desktop;

/// <summary>Snapshot of the auto-update state, returned by <c>GET /api/app/update</c>.</summary>
/// <param name="State">disabled, idle, checking, up-to-date, downloading, ready or error.</param>
public sealed record AppUpdateStatus(
    bool Installed,
    string? CurrentVersion,
    string? AvailableVersion,
    string State,
    int Progress,
    string? Error,
    DateTimeOffset? LastChecked);

/// <summary>
/// Checks GitHub Releases (or ALA_UPDATE_SOURCE: a folder or URL, for testing) for a newer build, downloads it
/// in the background and applies it on request or when the app exits. Only active in an installed build.
/// </summary>
internal sealed class DesktopUpdater : IDisposable
{
    private const string RepositoryUrl = "https://github.com/hackee-fr/agentic-log-analyzer";

    private readonly UpdateManager _manager;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private UpdateInfo? _pending;
    private string _state;
    private int _progress;
    private string? _error;
    private DateTimeOffset? _lastChecked;

    public DesktopUpdater()
    {
        var customSource = Environment.GetEnvironmentVariable("ALA_UPDATE_SOURCE");
        IUpdateSource source = string.IsNullOrWhiteSpace(customSource)
            ? new GithubSource(RepositoryUrl, accessToken: null, prerelease: false)
            : Uri.TryCreate(customSource, UriKind.Absolute, out var uri) && uri.Scheme.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? new SimpleWebSource(customSource)
                : new SimpleFileSource(new DirectoryInfo(customSource));
        _manager = new UpdateManager(source);
        _state = _manager.IsInstalled ? "idle" : "disabled";
    }

    public void Dispose() => _gate.Dispose();

    public AppUpdateStatus GetStatus() => new(
        _manager.IsInstalled,
        _manager.CurrentVersion?.ToString(),
        _pending?.TargetFullRelease.Version.ToString(),
        _state,
        _progress,
        _error,
        _lastChecked);

    public async Task CheckAndDownloadAsync(CancellationToken cancellationToken)
    {
        if (!_manager.IsInstalled || !await _gate.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            if (_pending is not null)
            {
                return;
            }

            _state = "checking";
            _error = null;
            var update = await _manager.CheckForUpdatesAsync();
            _lastChecked = DateTimeOffset.UtcNow;
            if (update is null)
            {
                _state = "up-to-date";
                return;
            }

            _state = "downloading";
            await _manager.DownloadUpdatesAsync(update, progress => _progress = progress, cancellationToken);
            _pending = update;
            _progress = 100;
            _state = "ready";
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Offline machines or GitHub rate limits must never break the app: report and retry later.
            _state = "error";
            _error = exception.Message;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Installs the downloaded update and restarts the app. Returns false when nothing is ready.</summary>
    public bool ApplyAndRestart()
    {
        if (_pending is null)
        {
            return false;
        }

        _manager.ApplyUpdatesAndRestart(_pending.TargetFullRelease);
        return true;
    }

    /// <summary>Installs a downloaded update silently once the app has exited.</summary>
    public void ApplyOnExit()
    {
        if (_pending is not null)
        {
            _manager.WaitExitThenApplyUpdates(_pending.TargetFullRelease, silent: true, restart: false);
        }
    }
}

/// <summary>Checks for updates shortly after startup, then every six hours.</summary>
internal sealed class DesktopUpdateWorker(DesktopUpdater updater) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        do
        {
            await updater.CheckAndDownloadAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
