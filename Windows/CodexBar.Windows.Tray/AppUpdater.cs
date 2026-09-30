using Velopack;
using Velopack.Sources;

namespace CodexBar.Windows.Tray;

/// <summary>
/// Checks GitHub Releases for new Velopack packages and downloads them in the background.
/// Portable or development builds (not installed through Setup.exe) never update.
/// </summary>
public sealed class AppUpdater
{
    public const string RepositoryUrl = "https://github.com/reiterstahl/CodexBar-win";

    private readonly UpdateManager _manager =
        new(new GithubSource(RepositoryUrl, accessToken: null, prerelease: false));
    private VelopackAsset? _ready;

    public bool IsInstalled => _manager.IsInstalled;

    public string? ReadyVersion => _ready?.Version.ToString();

    /// <summary>Returns the version that is downloaded and waiting for a restart, if any.</summary>
    public async Task<string?> CheckAndDownloadAsync(CancellationToken cancellationToken)
    {
        if (!_manager.IsInstalled)
        {
            return null;
        }

        if (_ready is not null)
        {
            return ReadyVersion;
        }

        if (_manager.UpdatePendingRestart is VelopackAsset pending)
        {
            _ready = pending;
            return ReadyVersion;
        }

        UpdateInfo? update = await _manager.CheckForUpdatesAsync();
        if (update is null)
        {
            return null;
        }

        await _manager.DownloadUpdatesAsync(update, cancelToken: cancellationToken);
        _ready = update.TargetFullRelease;
        return ReadyVersion;
    }

    /// <summary>
    /// Starts the Velopack updater, which waits for this process to exit, installs the
    /// downloaded version, and relaunches CodexBar.
    /// </summary>
    public bool ApplyAfterExit()
    {
        if (_ready is null)
        {
            return false;
        }

        _manager.WaitExitThenApplyUpdates(_ready, silent: true, restart: true);
        return true;
    }
}
