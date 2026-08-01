using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
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
    private DispatcherTimer? _availabilityAlertTimer;
    private Forms.NotifyIcon? _notifyIcon;
    private Icon? _normalIcon;
    private Icon? _alertYellowIcon;
    private Icon? _alertGreenIcon;
    private MainWindow? _window;
    private AppSettingsStore? _settingsStore;
    private readonly Dictionary<string, bool> _quotaAvailability = new(StringComparer.OrdinalIgnoreCase);
    private bool _hasQuotaBaseline;
    private bool _availabilityAlertActive;
    private bool _alertGreen;
    private string _lastTooltipText = "CodexBar";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _settingsStore = new AppSettingsStore();
        _window = new MainWindow(_settingsStore);
        _window.RefreshRequested += (_, _) => _ = RefreshAsync();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => ShowWindow());
        menu.Items.Add("Refresh", null, (_, _) => _ = RefreshAsync());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());

        _normalIcon = LoadApplicationIcon();
        _alertYellowIcon = CreateStatusIcon(_normalIcon, Color.FromArgb(217, 217, 0));
        _alertGreenIcon = CreateStatusIcon(_normalIcon, Color.FromArgb(57, 210, 112));
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
        _notifyIcon.MouseDoubleClick += (_, eventArgs) =>
        {
            if (eventArgs.Button == Forms.MouseButtons.Left)
            {
                _trayClickTimer?.Stop();
                DismissAvailabilityAlert();
            }
        };

        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMinutes(5),
        };
        _refreshTimer.Tick += (_, _) => _ = RefreshAsync();
        _refreshTimer.Start();

        _availabilityAlertTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(700),
        };
        _availabilityAlertTimer.Tick += (_, _) =>
        {
            _alertGreen = !_alertGreen;
            ApplyAlertIcon();
        };

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
        _availabilityAlertTimer?.Stop();
        _notifyIcon?.Dispose();
        _normalIcon?.Dispose();
        _alertYellowIcon?.Dispose();
        _alertGreenIcon?.Dispose();
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
            UpdateQuotaAvailability(results);
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
                    $"{profile.Label} returned no usage information.")
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

        int availableCount = results.Count(result => result.Snapshot is not null);
        string summary = availableCount switch
        {
            0 => "CodexBar — accounts unavailable",
            1 => "CodexBar — 1 account",
            _ => $"CodexBar — {availableCount} accounts",
        };
        _lastTooltipText = _availabilityAlertActive
            ? $"{summary} — quota available"
            : summary;
        _notifyIcon.Text = _lastTooltipText;
    }

    private void UpdateQuotaAvailability(IReadOnlyList<ProviderProfileResult> results)
    {
        bool quotaBecameAvailable = false;
        foreach (ProviderProfileResult result in results)
        {
            if (result.Snapshot is null)
            {
                continue;
            }

            foreach (RateWindow window in result.Snapshot.Windows)
            {
                string key = $"{result.Profile.Key}|{window.Id}";
                bool isAvailable = window.RemainingPercent > 0;
                if (_hasQuotaBaseline &&
                    isAvailable &&
                    _quotaAvailability.TryGetValue(key, out bool wasAvailable) &&
                    !wasAvailable)
                {
                    quotaBecameAvailable = true;
                }
                _quotaAvailability[key] = isAvailable;
            }
        }

        _hasQuotaBaseline = true;
        if (quotaBecameAvailable)
        {
            StartAvailabilityAlert();
        }
    }

    private void StartAvailabilityAlert()
    {
        if (_availabilityAlertActive)
        {
            return;
        }

        _availabilityAlertActive = true;
        _alertGreen = false;
        ApplyAlertIcon();
        _availabilityAlertTimer?.Start();
    }

    private void DismissAvailabilityAlert()
    {
        if (!_availabilityAlertActive)
        {
            return;
        }

        _availabilityAlertActive = false;
        _availabilityAlertTimer?.Stop();
        if (_notifyIcon is not null && _normalIcon is not null)
        {
            _notifyIcon.Icon = _normalIcon;
            _notifyIcon.Text = _lastTooltipText.Replace(" — quota available", string.Empty, StringComparison.Ordinal);
        }
    }

    private void ApplyAlertIcon()
    {
        if (!_availabilityAlertActive || _notifyIcon is null)
        {
            return;
        }

        Icon? icon = _alertGreen ? _alertGreenIcon : _alertYellowIcon;
        if (icon is not null)
        {
            _notifyIcon.Icon = icon;
        }
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
        using var icon = new Icon(stream);
        return (Icon)icon.Clone();
    }

    private static Icon CreateStatusIcon(Icon source, Color statusColor)
    {
        using Bitmap sourceBitmap = source.ToBitmap();
        using var bitmap = new Bitmap(
            sourceBitmap.Width,
            sourceBitmap.Height,
            PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.DrawImageUnscaled(sourceBitmap, 0, 0);

            int diameter = Math.Max(6, bitmap.Width / 3);
            int margin = Math.Max(1, bitmap.Width / 16);
            var badge = new Rectangle(
                bitmap.Width - diameter - margin,
                bitmap.Height - diameter - margin,
                diameter,
                diameter);
            using var outline = new Pen(Color.FromArgb(230, 16, 18, 22), Math.Max(1, bitmap.Width / 16));
            using var fill = new SolidBrush(statusColor);
            graphics.FillEllipse(fill, badge);
            graphics.DrawEllipse(outline, badge);
        }

        IntPtr handle = bitmap.GetHicon();
        try
        {
            using Icon icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);
}
