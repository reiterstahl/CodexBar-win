using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Resources;
using System.Windows.Threading;
using CodexBar.EngineClient;
using Forms = System.Windows.Forms;

namespace CodexBar.Windows.Tray;

public partial class App : System.Windows.Application
{
    private readonly CancellationTokenSource _shutdown = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private DispatcherTimer? _clockTimer;
    private DispatcherTimer? _refreshTimer;
    private DispatcherTimer? _trayClickTimer;
    private DispatcherTimer? _updateTimer;
    private Forms.ToolStripMenuItem? _updateMenuItem;
    private Forms.ToolStripMenuItem? _openMenuItem;
    private Forms.ToolStripMenuItem? _refreshMenuItem;
    private Forms.ToolStripMenuItem? _customizeMenuItem;
    private Forms.ToolStripMenuItem? _exitMenuItem;
    private IReadOnlyList<ProviderProfileResult> _lastResults = [];
    private readonly AppUpdater _updater = new();
    private Forms.NotifyIcon? _notifyIcon;
    private Icon? _normalIcon;
    private Icon? _dynamicIcon;
    private string? _dynamicIconKey;
    private MainWindow? _window;
    private AppSettingsStore? _settingsStore;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _settingsStore = new AppSettingsStore();
        _normalIcon = LoadApplicationIcon();
        _window = new MainWindow(_settingsStore);
        MainWindow = _window;
        _ = new WindowInteropHelper(_window).EnsureHandle();
        _window.RefreshRequested += (_, _) => _ = RefreshAsync();
        _window.ViewModel.QuotaRecovered += (_, recovered) => ShowRecoveryNotification(recovered);
        _window.ViewModel.AppearanceChanged += (_, _) =>
        {
            UpdateTrayTexts();
            UpdateTrayIcon();
        };
        _window.InstallUpdateRequested += (_, _) => InstallUpdate();

        var menu = new Forms.ContextMenuStrip();
        _updateMenuItem = new Forms.ToolStripMenuItem(string.Empty, null, (_, _) => InstallUpdate())
        {
            Visible = false,
        };
        _openMenuItem = new Forms.ToolStripMenuItem(string.Empty, null, (_, _) => ShowWindow());
        _refreshMenuItem = new Forms.ToolStripMenuItem(string.Empty, null, (_, _) => _ = RefreshAsync());
        _customizeMenuItem = new Forms.ToolStripMenuItem(string.Empty, null, (_, _) =>
        {
            ShowWindow();
            _window?.ShowCustomization();
        });
        _exitMenuItem = new Forms.ToolStripMenuItem(string.Empty, null, (_, _) => ExitApplication());
        menu.Items.Add(_updateMenuItem);
        menu.Items.Add(_openMenuItem);
        menu.Items.Add(_refreshMenuItem);
        menu.Items.Add(_customizeMenuItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_exitMenuItem);

        _notifyIcon = new Forms.NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = _normalIcon,
            Text = "CodexBar",
            Visible = true,
        };
        UpdateTrayTexts();
        _notifyIcon.MouseClick += (_, eventArgs) =>
        {
            if (eventArgs.Button == Forms.MouseButtons.Left)
            {
                ScheduleTrayToggle();
            }
        };

        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMinutes(5),
        };
        _refreshTimer.Tick += (_, _) => _ = RefreshAsync();
        _refreshTimer.Start();

        _clockTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMinutes(1),
        };
        _clockTimer.Tick += (_, _) => _window?.ViewModel.RefreshTimeLabels();
        _clockTimer.Start();

        if (ScreenshotCapture.OutputDirectory is string captureDirectory)
        {
            _ = CaptureAndExitAsync(captureDirectory);
            return;
        }

        if (_updater.IsInstalled)
        {
            // First check shortly after startup, then every six hours.
            _updateTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(30),
            };
            _updateTimer.Tick += (_, _) =>
            {
                _updateTimer.Interval = TimeSpan.FromHours(6);
                _ = CheckForUpdatesAsync();
            };
            _updateTimer.Start();
        }

        _ = RefreshAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _shutdown.Cancel();
        _clockTimer?.Stop();
        _refreshTimer?.Stop();
        _trayClickTimer?.Stop();
        _updateTimer?.Stop();
        _notifyIcon?.Dispose();
        _normalIcon?.Dispose();
        _dynamicIcon?.Dispose();
        base.OnExit(e);
    }

    private async Task RefreshAsync()
    {
        if (_window is null || !await _refreshGate.WaitAsync(0))
        {
            return;
        }

        try
        {
            _window.ViewModel.BeginRefresh();
            IReadOnlyList<ProviderProfile> profiles = ProviderProfileDiscovery.Discover();
            ProviderProfileResult[] results = await Task.WhenAll(
                profiles.Select(profile => FetchProfileAsync(profile, _shutdown.Token)));
            _window.ViewModel.Apply(results);
            UpdateTooltip(results);
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _window.ViewModel.ApplyError(exception.Message);
        }
        finally
        {
            _window.ViewModel.EndRefresh();
            _refreshGate.Release();
        }
    }

    private static async Task<ProviderProfileResult> FetchProfileAsync(
        ProviderProfile profile,
        CancellationToken cancellationToken)
    {
        try
        {
            var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [profile.EnvironmentVariable] = profile.ConfigDirectory,
            };
            var client = new EngineProcessClient(
                provider: profile.Provider,
                environmentOverrides: environment);
            EngineSnapshot engineSnapshot = await client.FetchAsync(cancellationToken);
            ProviderSnapshot? providerSnapshot = engineSnapshot.Providers.SingleOrDefault(
                provider => provider.Provider == profile.Provider);
            ProviderFailure? failure = engineSnapshot.Failures.SingleOrDefault(
                candidate => candidate.Provider == profile.Provider);
            failure ??= providerSnapshot is null
                ? new ProviderFailure(
                    profile.Provider,
                    "missing_result",
                    Loc.F("NoUsageInfo", profile.Label))
                : null;
            return new ProviderProfileResult(
                profile,
                engineSnapshot.GeneratedAt,
                providerSnapshot,
                failure);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new ProviderProfileResult(
                profile,
                DateTimeOffset.Now,
                null,
                new ProviderFailure(profile.Provider, "engine_error", exception.Message));
        }
    }

    private void UpdateTooltip(IReadOnlyList<ProviderProfileResult> results)
    {
        if (_notifyIcon is null)
        {
            return;
        }

        _lastResults = results;
        UpdateTrayTexts();
        UpdateTrayIcon();
    }

    private void UpdateTrayTexts()
    {
        if (_notifyIcon is null)
        {
            return;
        }

        int accountCount = _lastResults.Count(result => result.Snapshot is not null);
        int availableCount = _window?.ViewModel.Providers.Count(card => card.HasQuota) ?? 0;
        _notifyIcon.Text = accountCount == 0
            ? Loc.T("TrayNoAccounts")
            : Loc.F("TraySummary", availableCount, accountCount);
        _openMenuItem!.Text = Loc.T("TrayOpen");
        _refreshMenuItem!.Text = Loc.T("Refresh");
        _customizeMenuItem!.Text = Loc.T("TrayCustomize");
        _exitMenuItem!.Text = Loc.T("TrayExit");
        if (_updater.ReadyVersion is string version)
        {
            _updateMenuItem!.Text = Loc.F("TrayRestartToUpdate", version);
        }
    }

    private void UpdateTrayIcon()
    {
        if (_notifyIcon is null || _window is null)
        {
            return;
        }

        TrayIconState? state = _window.ViewModel.CurrentTrayState();
        if (!_window.ViewModel.Settings.DynamicTrayIcon || state is null)
        {
            // Reapply the stable icon after refresh to repair stale shell rendering.
            _notifyIcon.Icon = _normalIcon;
            _dynamicIcon?.Dispose();
            _dynamicIcon = null;
            _dynamicIconKey = null;
            return;
        }

        string key = TrayIconRenderer.CacheKey(state, _window.ViewModel.Palette);
        if (key == _dynamicIconKey && _dynamicIcon is not null)
        {
            _notifyIcon.Icon = _dynamicIcon;
            return;
        }

        try
        {
            Icon rendered = TrayIconRenderer.Render(state, _window.ViewModel.Palette);
            Icon? previous = _dynamicIcon;
            _notifyIcon.Icon = rendered;
            _dynamicIcon = rendered;
            _dynamicIconKey = key;
            previous?.Dispose();
        }
        catch (Exception)
        {
            _notifyIcon.Icon = _normalIcon;
        }
    }

    private async Task CaptureAndExitAsync(string directory)
    {
        try
        {
            await RefreshAsync();
            if (_window is null)
            {
                return;
            }

            ShowWindow();
            await Task.Delay(TimeSpan.FromSeconds(1.5));
            ScreenshotCapture.Save(_window, Path.Combine(directory, $"{ScreenshotCapture.Name}.png"));
            if (ScreenshotCapture.CustomizationName is string customizationName)
            {
                SettingsWindow settings = _window.ShowCustomization();
                await Task.Delay(TimeSpan.FromSeconds(1.5));
                ScreenshotCapture.Save(settings, Path.Combine(directory, $"{customizationName}.png"));
            }
        }
        finally
        {
            ExitApplication();
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        if (_updater.ReadyVersion is not null)
        {
            return;
        }

        try
        {
            string? version = await _updater.CheckAndDownloadAsync(_shutdown.Token);
            if (version is null || _window is null)
            {
                return;
            }

            _window.ViewModel.SetUpdateReady(version);
            if (_updateMenuItem is not null)
            {
                _updateMenuItem.Text = Loc.F("TrayRestartToUpdate", version);
                _updateMenuItem.Visible = true;
            }
            _notifyIcon?.ShowBalloonTip(
                8000,
                Loc.F("UpdateReadyTitle", version),
                Loc.T("UpdateReadyBody"),
                Forms.ToolTipIcon.None);
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            // Offline, rate-limited or no published releases: try again on the next tick.
        }
    }

    private void InstallUpdate()
    {
        try
        {
            if (_updater.ApplyAfterExit())
            {
                ExitApplication();
            }
        }
        catch (Exception exception)
        {
            _window?.ViewModel.ShowStatus(Loc.F("UpdateFailed", exception.Message));
        }
    }

    private void ShowRecoveryNotification(QuotaRecoveredEventArgs recovered)
    {
        if (_notifyIcon is null || _window?.ViewModel.Settings.NotifyOnRecovery != true)
        {
            return;
        }

        _notifyIcon.ShowBalloonTip(
            8000,
            Loc.F("RecoveredTitle", recovered.DisplayName),
            recovered.Detail,
            Forms.ToolTipIcon.None);
    }

    private void ScheduleTrayToggle()
    {
        _trayClickTimer?.Stop();
        _trayClickTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(260),
        };
        _trayClickTimer.Tick += (_, _) =>
        {
            _trayClickTimer?.Stop();
            ToggleWindow();
        };
        _trayClickTimer.Start();
    }

    private void ToggleWindow()
    {
        if (_window?.WindowState == WindowState.Minimized || _window?.IsVisible != true)
        {
            ShowWindow();
        }
        else
        {
            _window.Hide();
        }
    }

    private void ShowWindow()
    {
        if (_window is null)
        {
            return;
        }

        _window.RestoreFromTray();
    }

    private void ExitApplication()
    {
        _window?.CloseForExit();
        Shutdown();
    }

    private static Icon LoadApplicationIcon()
    {
        var resourceUri = new Uri(
            "pack://application:,,,/CodexBar.Windows.Tray;component/Assets/CodexBar.ico",
            UriKind.Absolute);
        StreamResourceInfo resource = GetResourceStream(resourceUri)
            ?? throw new InvalidOperationException("The CodexBar application icon is missing.");
        using Stream stream = resource.Stream;
        using var icon = new Icon(stream, 32, 32);
        return (Icon)icon.Clone();
    }

}
