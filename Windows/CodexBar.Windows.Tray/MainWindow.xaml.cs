using System.ComponentModel;
using System.Windows;

namespace CodexBar.Windows.Tray;

public partial class MainWindow : Window
{
    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();
        ViewModel = new MainWindowViewModel();
        DataContext = ViewModel;
        Deactivated += (_, _) => Hide();
    }

    public event EventHandler? RefreshRequested;

    public MainWindowViewModel ViewModel { get; }

    public void PositionNearNotificationArea()
    {
        Rect workArea = SystemParameters.WorkArea;
        Left = Math.Max(workArea.Left, workArea.Right - Width - 12);
        Top = Math.Max(workArea.Top, workArea.Bottom - ActualHeight - 12);
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
}
