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
        _window.ViewModel.AppearanceChanged += (_, _) => UpdateTrayIcon();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Abrir", null, (_, _) => ShowWindow());
        menu.Items.Add("Actualizar", null, (_, _) => _ = RefreshAsync());
        menu.Items.Add("Personalizar…", null, (_, _) =>
        {
            ShowWindow();
            _window?.ShowCustomization();
        });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Salir", null, (_, _) => ExitApplication());

        _notifyIcon = new Forms.NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = _normalIcon,
            Text = "CodexBar",
            Visible = true,
        };
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

        _ = RefreshAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _shutdown.Cancel();
        _clockTimer?.Stop();
        _refreshTimer?.Stop();
        _trayClickTimer?.Stop();
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
                    $"{profile.Label} no devolvió información de uso.")
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

        int accountCount = results.Count(result => result.Snapshot is not null);
        int availableCount = _window?.ViewModel.Providers.Count(card => card.HasQuota) ?? 0;
        _notifyIcon.Text = accountCount == 0
            ? "CodexBar — cuentas no disponibles"
            : $"CodexBar — {availableCount} de {accountCount} con cuota";
        UpdateTrayIcon();
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

    private void ShowRecoveryNotification(QuotaRecoveredEventArgs recovered)
    {
        if (_notifyIcon is null || _window?.ViewModel.Settings.NotifyOnRecovery != true)
        {
            return;
        }

        _notifyIcon.ShowBalloonTip(
            8000,
            $"{recovered.DisplayName} ya tiene cuota",
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
