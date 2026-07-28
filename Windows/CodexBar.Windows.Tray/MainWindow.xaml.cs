using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace CodexBar.Windows.Tray;

public partial class MainWindow : Window
{
    private bool _allowClose;

    public MainWindow(AppSettingsStore settingsStore)
    {
        InitializeComponent();
        ViewModel = new MainWindowViewModel(settingsStore);
        DataContext = ViewModel;
        Topmost = ViewModel.IsAlwaysOnTop;
        ApplyViewMode();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
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

    public void PositionNearNotificationArea()
    {
        Rect workArea = SystemParameters.WorkArea;
        Left = Math.Max(workArea.Left, workArea.Right - Width - 12);
        Top = Math.Max(workArea.Top, workArea.Bottom - ActualHeight - 12);
    }

    public void RestoreFromTray()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Show();
        UpdateLayout();
        PositionNearNotificationArea();
        Activate();
    }

    public void CloseForExit()
    {
        _allowClose = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
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

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CompactButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ToggleCompact();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsCompact))
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
        Width = ViewModel.IsCompact ? 820 : 440;
        MaxHeight = ViewModel.IsCompact ? 520 : 720;
        if (IsVisible)
        {
            UpdateLayout();
            PositionNearNotificationArea();
        }
    }
}
