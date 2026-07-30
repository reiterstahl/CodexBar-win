using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace CodexBar.Windows.Tray;

public partial class MainWindow : Window
{
    private readonly AppSettingsStore _settingsStore;
    private bool _allowClose;
    private bool _positionInitialized;

    public MainWindow(AppSettingsStore settingsStore)
    {
        InitializeComponent();
        _settingsStore = settingsStore;
        ViewModel = new MainWindowViewModel(settingsStore);
        DataContext = ViewModel;
        Topmost = ViewModel.IsAlwaysOnTop;
        ApplyViewMode();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        Deactivated += (_, _) => SaveWindowPosition();
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
            }
        };
    }

    public event EventHandler? RefreshRequested;

    public MainWindowViewModel ViewModel { get; }

    public void RestoreFromTray()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Show();
        UpdateLayout();
        EnsureWindowPosition();
        Activate();
    }

    public void CloseForExit()
    {
        _allowClose = true;
        Close();
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
            ViewModel.ShowStatus($"Login command copied for {provider.DisplayName}.");
        }
        catch (Exception)
        {
            ViewModel.ShowStatus("Could not copy the login command. Try again.");
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

    private void CompactButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ToggleCompact();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ToggleSettings();
    }

    private void DecreaseUiScaleButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.DecreaseUiScale();
    }

    private void IncreaseUiScaleButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.IncreaseUiScale();
    }

    private void ResetUiScaleButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ResetUiScale();
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
        if (e.PropertyName == nameof(MainWindowViewModel.IsCompact) ||
            e.PropertyName == nameof(MainWindowViewModel.UiScale))
        {
            ApplyViewMode();
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.IsAlwaysOnTop))
        {
            Topmost = ViewModel.IsAlwaysOnTop;
        }
    }

    private void ApplyViewMode()
    {
        double baseWidth = ViewModel.IsCompact ? 820 : 440;
        Width = baseWidth * ViewModel.UiScale;
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
}
