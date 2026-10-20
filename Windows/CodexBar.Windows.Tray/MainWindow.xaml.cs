using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace CodexBar.Windows.Tray;

public partial class MainWindow : Window
{
    private const int GclpHIcon = -14;
    private const int GclpHIconSmall = -34;
    private const int WmGetIcon = 0x007F;
    private const int WmSetIcon = 0x0080;
    private static readonly uint TaskbarButtonCreatedMessage = RegisterWindowMessage("TaskbarButtonCreated");
    private static readonly IntPtr IconSmall = IntPtr.Zero;
    private static readonly IntPtr IconBig = new(1);
    private static readonly IntPtr IconSmall2 = new(2);

    private readonly AppSettingsStore _settingsStore;
    private readonly DispatcherTimer _recoveryAlertTimer;
    private System.Drawing.Icon? _taskbarSmallIcon;
    private System.Drawing.Icon? _taskbarLargeIcon;
    private MemoryStream? _taskbarSmallIconStream;
    private MemoryStream? _taskbarLargeIconStream;
    private SettingsWindow? _settingsWindow;
    private HwndSource? _windowSource;
    private bool _allowClose;
    private bool _positionInitialized;
    private bool _taskbarIconRefreshQueued;
    private bool _taskbarButtonReady;
    private bool _isClosed;

    public MainWindow(AppSettingsStore settingsStore)
    {
        InitializeComponent();
        _settingsStore = settingsStore;
        ViewModel = new MainWindowViewModel(settingsStore);
        DataContext = ViewModel;
        Topmost = ViewModel.IsAlwaysOnTop;
        _recoveryAlertTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(700),
        };
        _recoveryAlertTimer.Tick += (_, _) => ViewModel.ToggleRecoveryPulse();
        ViewModel.Palette.ApplyTo(System.Windows.Application.Current.Resources);
        ApplyViewMode();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        Deactivated += (_, _) => SaveWindowPosition();
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible)
            {
                _taskbarButtonReady = false;
            }
        };
        ContentRendered += (_, _) => QueueTaskbarIconRefresh();
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Normal)
            {
                QueueTaskbarIconRefresh();
            }
        };
    }

    public event EventHandler? RefreshRequested;

    public event EventHandler? InstallUpdateRequested;

    public MainWindowViewModel ViewModel { get; }

    public SettingsWindow ShowCustomization()
    {
        if (_settingsWindow is { IsLoaded: true })
        {
            _settingsWindow.Activate();
            return _settingsWindow;
        }

        _settingsWindow = new SettingsWindow(ViewModel) { Owner = this };
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.PlaceNextTo(this);
        _settingsWindow.Show();
        return _settingsWindow;
    }

    public void RestoreFromTray()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Show();
        UpdateLayout();
        ApplyTaskbarIcons();
        EnsureWindowPosition();
        Activate();
        QueueTaskbarIconRefresh();
    }

    public void CloseForExit()
    {
        _allowClose = true;
        Close();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        ApplyTaskbarIcons();
        _windowSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        _windowSource?.AddHook(WindowMessageHook);
        base.OnSourceInitialized(e);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        SaveWindowPosition();
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _isClosed = true;
        if (_windowSource is { IsDisposed: false })
        {
            _windowSource.RemoveHook(WindowMessageHook);
        }
        _windowSource = null;
        _recoveryAlertTimer.Stop();
        _taskbarSmallIcon?.Dispose();
        _taskbarLargeIcon?.Dispose();
        _taskbarSmallIconStream?.Dispose();
        _taskbarLargeIconStream?.Dispose();
        base.OnClosed(e);
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CopyLoginCommand_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ProviderCardViewModel provider })
        {
            return;
        }

        try
        {
            System.Windows.Clipboard.SetText(provider.LoginCommand);
            ViewModel.ShowStatus(Loc.F("LoginCopied", provider.DisplayName));
        }
        catch (Exception)
        {
            ViewModel.ShowStatus(Loc.T("LoginCopyFailed"));
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
            SaveWindowPosition();
        }
    }

    private void Window_PreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DismissRecoveryAlertForSource(e.OriginalSource);
        }
    }

    private void CardList_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Buttons and the rename box handle their own clicks; anything that bubbles up here
        // is card background, which moves the window like the header does.
        if (e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
            SaveWindowPosition();
        }
        catch (InvalidOperationException)
        {
            // The mouse button was released before Windows began the move operation.
        }
    }

    private void DismissRecoveryAlertForSource(object source)
    {
        ProviderCardViewModel? provider = source switch
        {
            FrameworkElement element => element.DataContext as ProviderCardViewModel,
            FrameworkContentElement element => element.DataContext as ProviderCardViewModel,
            _ => null,
        };
        if (provider is not null)
        {
            ViewModel.DismissRecoveryAlert(provider);
        }
    }

    private void InstallUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        InstallUpdateRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CustomizeButton_Click(object sender, RoutedEventArgs e)
    {
        ShowCustomization();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        SaveWindowPosition();
        WindowState = WindowState.Minimized;
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        SaveWindowPosition();
        Hide();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.BaseWidth) ||
            e.PropertyName == nameof(MainWindowViewModel.UiScale))
        {
            ApplyViewMode();
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.Palette))
        {
            ViewModel.Palette.ApplyTo(System.Windows.Application.Current.Resources);
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.IsAlwaysOnTop))
        {
            Topmost = ViewModel.IsAlwaysOnTop;
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.HasRecoveryAlerts))
        {
            SyncRecoveryAlertTimer();
        }
    }

    private void SyncRecoveryAlertTimer()
    {
        if (ViewModel.HasRecoveryAlerts)
        {
            _recoveryAlertTimer.Start();
        }
        else
        {
            _recoveryAlertTimer.Stop();
        }
    }

    private void ApplyViewMode()
    {
        Width = ViewModel.BaseWidth * ViewModel.UiScale;
        MaxHeight = SystemParameters.WorkArea.Height - 24;
    }

    private void EnsureWindowPosition()
    {
        if (_positionInitialized)
        {
            return;
        }

        double? savedLeft = _settingsStore.Settings.WindowLeft;
        double? savedTop = _settingsStore.Settings.WindowTop;
        if (savedLeft is double left &&
            savedTop is double top &&
            IsVisibleOnScreen(left, top))
        {
            Left = left;
            Top = top;
        }
        else
        {
            Rect workArea = SystemParameters.WorkArea;
            Left = Math.Max(workArea.Left, workArea.Right - Width - 12);
            Top = Math.Max(workArea.Top, workArea.Bottom - ActualHeight - 12);
        }

        _positionInitialized = true;
        SaveWindowPosition();
    }

    private bool IsVisibleOnScreen(double left, double top)
    {
        if (!double.IsFinite(left) || !double.IsFinite(top))
        {
            return false;
        }

        var virtualScreen = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
        var windowBounds = new Rect(
            left,
            top,
            Width,
            Math.Max(ActualHeight, MinHeight));
        return virtualScreen.IntersectsWith(windowBounds);
    }

    private void SaveWindowPosition()
    {
        if (_positionInitialized &&
            WindowState == WindowState.Normal &&
            double.IsFinite(Left) &&
            double.IsFinite(Top))
        {
            _settingsStore.SetWindowPosition(Left, Top);
        }
    }

    private IntPtr WindowMessageHook(
        IntPtr windowHandle,
        int message,
        IntPtr wordParameter,
        IntPtr longParameter,
        ref bool handled)
    {
        if (TaskbarButtonCreatedMessage != 0 && (uint)message == TaskbarButtonCreatedMessage)
        {
            // The HWND can exist long before Explorer creates its taskbar button.
            // This also runs when Explorer recreates the button after a restart.
            _taskbarButtonReady = true;
            QueueTaskbarIconRefresh();
        }
        else if (message == WmGetIcon)
        {
            // Answer Explorer's first icon request directly, including ICON_SMALL2.
            // Keep these HICONs alive until the native window has been destroyed.
            IntPtr icon = wordParameter == IconBig
                ? _taskbarLargeIcon?.Handle ?? IntPtr.Zero
                : wordParameter == IconSmall || wordParameter == IconSmall2
                    ? _taskbarSmallIcon?.Handle ?? IntPtr.Zero
                    : IntPtr.Zero;
            if (icon != IntPtr.Zero)
            {
                handled = true;
                return icon;
            }
        }

        return IntPtr.Zero;
    }

    private void ApplyTaskbarIcons()
    {
        IntPtr windowHandle = new WindowInteropHelper(this).Handle;
        if (windowHandle == IntPtr.Zero)
        {
            return;
        }

        EnsureTaskbarIcons();
        if (_taskbarSmallIcon is not null)
        {
            SetClassLongPtr(windowHandle, GclpHIconSmall, _taskbarSmallIcon.Handle);
            SendMessage(windowHandle, WmSetIcon, IconSmall, _taskbarSmallIcon.Handle);
        }

        if (_taskbarLargeIcon is not null)
        {
            SetClassLongPtr(windowHandle, GclpHIcon, _taskbarLargeIcon.Handle);
            SendMessage(windowHandle, WmSetIcon, IconBig, _taskbarLargeIcon.Handle);
        }
    }

    private void QueueTaskbarIconRefresh()
    {
        if (_isClosed || _taskbarIconRefreshQueued)
        {
            return;
        }

        _taskbarIconRefreshQueued = true;
        Dispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() =>
            {
                _taskbarIconRefreshQueued = false;
                if (_isClosed || !IsVisible)
                {
                    return;
                }

                ApplyTaskbarIcons();
                if (_taskbarButtonReady)
                {
                    TaskbarShell.RefreshIcon(new WindowInteropHelper(this).Handle);
                }
            }));
    }

    private void EnsureTaskbarIcons()
    {
        if (_taskbarSmallIcon is not null && _taskbarLargeIcon is not null)
        {
            return;
        }

        try
        {
            var iconUri = new Uri(
                "pack://application:,,,/CodexBar.Windows.Tray;component/Assets/CodexBar.ico",
                UriKind.Absolute);
            var iconResource = System.Windows.Application.GetResourceStream(iconUri);
            if (iconResource is null)
            {
                Trace.TraceError("The embedded CodexBar taskbar icon is missing.");
                return;
            }

            using Stream source = iconResource.Stream;
            using var buffer = new MemoryStream();
            source.CopyTo(buffer);
            byte[] iconBytes = buffer.ToArray();

            _taskbarSmallIconStream = new MemoryStream(iconBytes, writable: false);
            _taskbarLargeIconStream = new MemoryStream(iconBytes, writable: false);
            _taskbarSmallIcon = new System.Drawing.Icon(_taskbarSmallIconStream, 16, 16);
            _taskbarLargeIcon = new System.Drawing.Icon(_taskbarLargeIconStream, 32, 32);
        }
        catch (Exception exception)
        {
            Trace.TraceError($"Could not load the CodexBar taskbar icon: {exception.Message}");
            _taskbarSmallIcon?.Dispose();
            _taskbarLargeIcon?.Dispose();
            _taskbarSmallIconStream?.Dispose();
            _taskbarLargeIconStream?.Dispose();
            _taskbarSmallIcon = null;
            _taskbarLargeIcon = null;
            _taskbarSmallIconStream = null;
            _taskbarLargeIconStream = null;
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(
        IntPtr windowHandle,
        int message,
        IntPtr wordParameter,
        IntPtr longParameter);

    [DllImport("user32.dll", EntryPoint = "SetClassLongPtrW")]
    private static extern IntPtr SetClassLongPtr(
        IntPtr windowHandle,
        int index,
        IntPtr newValue);

    [DllImport("user32.dll", EntryPoint = "RegisterWindowMessageW", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string message);
}
