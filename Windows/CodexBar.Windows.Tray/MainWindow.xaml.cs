using System.ComponentModel;
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
    private const int WmSetIcon = 0x0080;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const uint RedrawInvalidate = 0x0001;
    private const uint RedrawUpdateNow = 0x0100;
    private const uint RedrawFrame = 0x0400;
    private static readonly IntPtr IconSmall = IntPtr.Zero;
    private static readonly IntPtr IconBig = new(1);

    private readonly AppSettingsStore _settingsStore;
    private readonly DispatcherTimer _recoveryAlertTimer;
    private System.Drawing.Icon? _taskbarSmallIcon;
    private System.Drawing.Icon? _taskbarLargeIcon;
    private MemoryStream? _taskbarSmallIconStream;
    private MemoryStream? _taskbarLargeIconStream;
    private SettingsWindow? _settingsWindow;
    private bool _allowClose;
    private bool _positionInitialized;
    private bool _taskbarIconRefreshQueued;

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

    public void ShowCustomization()
    {
        if (_settingsWindow is { IsLoaded: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(ViewModel) { Owner = this };
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.PlaceNextTo(this);
        _settingsWindow.Show();
    }

    public void RestoreFromTray()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Show();
        UpdateLayout();
        ApplyTaskbarIcons(refreshNativeFrame: false);
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
        // Windows 11 can ask for the taskbar icon while WPF is still completing
        // source initialization. Set the HWND and window-class icons before WPF
        // raises SourceInitialized so Explorer never observes the generic icon.
        ApplyTaskbarIcons(refreshNativeFrame: false);
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
            ViewModel.ShowStatus($"Login copiado para {provider.DisplayName}. Pegalo en PowerShell.");
        }
        catch (Exception)
        {
            ViewModel.ShowStatus("No se pudo copiar el login. Intentá de nuevo.");
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

    private void ApplyTaskbarIcons(bool refreshNativeFrame)
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

        if (refreshNativeFrame)
        {
            // Explorer can retain WPF's provisional icon until it sees a native frame
            // update. This reproduces the refresh caused by moving the window without
            // changing its position, dimensions, activation, or Z order.
            SetWindowPos(
                windowHandle,
                IntPtr.Zero,
                0,
                0,
                0,
                0,
                SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
            RedrawWindow(
                windowHandle,
                IntPtr.Zero,
                IntPtr.Zero,
                RedrawInvalidate | RedrawUpdateNow | RedrawFrame);
        }
    }

    private void QueueTaskbarIconRefresh()
    {
        if (_taskbarIconRefreshQueued)
        {
            return;
        }

        _taskbarIconRefreshQueued = true;
        Dispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() =>
            {
                _taskbarIconRefreshQueued = false;
                ApplyTaskbarIcons(refreshNativeFrame: true);
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
        catch (Exception)
        {
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

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr windowHandle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RedrawWindow(
        IntPtr windowHandle,
        IntPtr updateRectangle,
        IntPtr updateRegion,
        uint flags);
}
