using System.Drawing;
using System.Windows;
using System.Windows.Threading;
using CodexBar.EngineClient;
using Forms = System.Windows.Forms;

namespace CodexBar.Windows.Tray;

public partial class App : System.Windows.Application
{
    private readonly CancellationTokenSource _shutdown = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private DispatcherTimer? _refreshTimer;
    private Forms.NotifyIcon? _notifyIcon;
    private MainWindow? _window;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _window = new MainWindow();
        _window.RefreshRequested += (_, _) => _ = RefreshAsync();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => ShowWindow());
        menu.Items.Add("Refresh", null, (_, _) => _ = RefreshAsync());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());

        _notifyIcon = new Forms.NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = SystemIcons.Application,
            Text = "CodexBar",
            Visible = true,
        };
        _notifyIcon.MouseClick += (_, eventArgs) =>
        {
            if (eventArgs.Button == Forms.MouseButtons.Left)
            {
                ToggleWindow();
            }
        };

        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMinutes(5),
        };
        _refreshTimer.Tick += (_, _) => _ = RefreshAsync();
        _refreshTimer.Start();

        _ = RefreshAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _shutdown.Cancel();
        _refreshTimer?.Stop();
        _notifyIcon?.Dispose();
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
        _notifyIcon.Text = summary;
    }

    private void ToggleWindow()
    {
        if (_window?.IsVisible == true)
        {
            _window.Hide();
        }
        else
        {
            ShowWindow();
        }
    }

    private void ShowWindow()
    {
        if (_window is null)
        {
            return;
        }

        if (!_window.IsVisible)
        {
            _window.Show();
            _window.UpdateLayout();
        }

        _window.PositionNearNotificationArea();
        _window.Activate();
    }

    private void ExitApplication()
    {
        _window?.CloseForExit();
        Shutdown();
    }
}
