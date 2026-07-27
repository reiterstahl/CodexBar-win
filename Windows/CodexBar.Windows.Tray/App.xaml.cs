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
    private EngineProcessClient? _engineClient;

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
        if (_engineClient is null || _window is null || !await _refreshGate.WaitAsync(0))
        {
            return;
        }

        try
        {
            _engineClient ??= new EngineProcessClient();
            _window.ViewModel.BeginRefresh();
            EngineSnapshot snapshot = await _engineClient.FetchAsync(_shutdown.Token);
            _window.ViewModel.Apply(snapshot);
            UpdateTooltip(snapshot);
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

    private void UpdateTooltip(EngineSnapshot snapshot)
    {
        if (_notifyIcon is null)
        {
            return;
        }

        string summary = snapshot.Providers.Length switch
        {
            0 => "CodexBar — providers unavailable",
            1 => $"CodexBar — {snapshot.Providers[0].DisplayName}",
            _ => "CodexBar — Codex and Claude Code",
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
