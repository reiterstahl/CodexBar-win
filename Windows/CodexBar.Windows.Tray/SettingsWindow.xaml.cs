using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Forms = System.Windows.Forms;

namespace CodexBar.Windows.Tray;

public partial class SettingsWindow : Window
{
    private const double BaseWidth = 500;
    private const double ScreenGap = 12;

    private readonly MainWindowViewModel _viewModel;

    public SettingsWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        ApplyScale();
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    /// <summary>Opens beside the main window, on whichever side has room.</summary>
    public void PlaceNextTo(Window owner)
    {
        Rect workArea = SystemParameters.WorkArea;
        MaxHeight = workArea.Height - (ScreenGap * 2);
        double ownerWidth = owner.ActualWidth > 0 ? owner.ActualWidth : owner.Width;
        double left = owner.Left - Width - ScreenGap;
        if (left < workArea.Left)
        {
            left = Math.Min(owner.Left + ownerWidth + ScreenGap, workArea.Right - Width);
        }

        Left = Math.Max(workArea.Left, left);
        Top = Math.Clamp(owner.Top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - 600));
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        base.OnClosed(e);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.UiScale))
        {
            ApplyScale();
        }
    }

    private void ApplyScale()
    {
        Width = BaseWidth * _viewModel.UiScale;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // The mouse button was released before Windows began the move operation.
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void PickAccentButton_Click(object sender, RoutedEventArgs e)
    {
        if (PickColor(_viewModel.Customization.AccentHex) is string hex)
        {
            _viewModel.Customization.SetAccent(hex);
        }
    }

    private void PickProviderColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string provider })
        {
            return;
        }

        string current = provider == "claude"
            ? _viewModel.Customization.ClaudeColor
            : _viewModel.Customization.CodexColor;
        if (PickColor(current) is string hex)
        {
            _viewModel.Customization.SetProviderColor(provider, hex);
        }
    }

    private void HexBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is System.Windows.Controls.TextBox textBox)
        {
            textBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
            textBox.Text = _viewModel.Customization.AccentHex;
            textBox.SelectAll();
            e.Handled = true;
        }
    }

    private static string? PickColor(string currentHex)
    {
        RgbColor current = RgbColor.TryParse(currentHex, out RgbColor parsed) ? parsed : RgbColor.White;
        using var dialog = new Forms.ColorDialog
        {
            AllowFullOpen = true,
            AnyColor = true,
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(current.R, current.G, current.B),
        };
        return dialog.ShowDialog() == Forms.DialogResult.OK
            ? new RgbColor(dialog.Color.R, dialog.Color.G, dialog.Color.B).ToHex()
            : null;
    }
}
