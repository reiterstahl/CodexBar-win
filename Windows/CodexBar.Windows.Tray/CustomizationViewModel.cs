using System.ComponentModel;
using System.IO;
using System.Reflection;
using Brush = System.Windows.Media.Brush;

namespace CodexBar.Windows.Tray;

/// <summary>One selectable choice in the customization panel (theme tile, swatch, segment).</summary>
public sealed class OptionViewModel(string value, Func<string> label, Action select) : INotifyPropertyChanged
{
    private bool _isSelected;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Value { get; } = value;

    /// <summary>Re-evaluated on every language change.</summary>
    public string Label => label();

    public RelayCommand SelectCommand { get; } = new(select);

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public Brush? Swatch { get; init; }

    public Brush? PreviewBackground { get; init; }

    public Brush? PreviewFrame { get; init; }

    public Brush? PreviewTrack { get; init; }

    public Brush? PreviewText { get; set; }

    public Brush? PreviewAccent { get; set; }

    public void RaisePreviewChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
}

/// <summary>
/// Backs the "Personalizar" window. Every change is persisted immediately and pushed to the
/// main view model, which restyles the open window in place.
/// </summary>
public sealed class CustomizationViewModel : INotifyPropertyChanged
{
    private readonly AppSettingsStore _store;
    private readonly MainWindowViewModel _owner;

    public CustomizationViewModel(AppSettingsStore store, MainWindowViewModel owner)
    {
        _store = store;
        _owner = owner;
        Themes = ThemeCatalog.Themes.Select(CreateThemeOption).ToArray();
        Accents = ThemeCatalog.Accents
            .Select(accent => new OptionViewModel(
                accent.Hex,
                () => Loc.T(accent.NameKey),
                () => SetAccent(accent.Hex))
            {
                Swatch = Frozen(RgbColor.Parse(accent.Hex)),
            })
            .ToArray();
        Charts = Choices(
            [(ChartKind.Bar, "ChartBar"), (ChartKind.Ring, "ChartRing"), (ChartKind.Gauge, "ChartGauge"),
                (ChartKind.Blocks, "ChartBlocks"), (ChartKind.Numbers, "ChartNumbers")],
            value => Change(settings => settings.ChartKind = value));
        ColorModes = Choices(
            [(ChartColorMode.Accent, "ColorAccent"), (ChartColorMode.Level, "ColorLevel"), (ChartColorMode.Provider, "ColorProvider")],
            value => Change(settings => settings.ColorMode = value));
        PercentModes =
        [
            new OptionViewModel("available", () => Loc.T("PercentAvailable"), () => Change(settings => settings.ShowUsedPercent = false)),
            new OptionViewModel("used", () => Loc.T("PercentUsed"), () => Change(settings => settings.ShowUsedPercent = true)),
        ];
        Views = Choices(
            [(ViewMode.Cards, "ViewCards"), (ViewMode.Summary, "ViewSummary"), (ViewMode.Mini, "ViewMini")],
            value => Change(settings => settings.ViewMode = value));
        Densities = Choices(
            [(Density.Comfortable, "DensityComfortable"), (Density.Compact, "DensityCompact")],
            value => Change(settings => settings.Density = value));
        // Language names stay in their own language so they can always be found.
        Languages =
        [
            new OptionViewModel(Loc.Automatic, () => Loc.T("LanguageAuto"), () => Change(settings => settings.Language = Loc.Automatic)),
            new OptionViewModel(Loc.Spanish, () => "Español", () => Change(settings => settings.Language = Loc.Spanish)),
            new OptionViewModel(Loc.English, () => "English", () => Change(settings => settings.Language = Loc.English)),
        ];
        ResetCommand = new RelayCommand(() => Change(settings => settings.ResetAppearance()));
        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<OptionViewModel> Themes { get; }

    public IReadOnlyList<OptionViewModel> Accents { get; }

    public IReadOnlyList<OptionViewModel> Charts { get; }

    public IReadOnlyList<OptionViewModel> ColorModes { get; }

    public IReadOnlyList<OptionViewModel> PercentModes { get; }

    public IReadOnlyList<OptionViewModel> Views { get; }

    public IReadOnlyList<OptionViewModel> Densities { get; }

    public IReadOnlyList<OptionViewModel> Languages { get; }

    public RelayCommand ResetCommand { get; }

    public string AccentHex
    {
        get => Settings.AccentColor ?? ThemeCatalog.DefaultAccent;
        set
        {
            if (RgbColor.TryParse(value, out RgbColor color))
            {
                SetAccent(color.ToHex());
            }
        }
    }

    public string CodexColor => Settings.CodexColor ?? ThemeCatalog.DefaultCodexColor;

    public string ClaudeColor => Settings.ClaudeColor ?? ThemeCatalog.DefaultClaudeColor;

    public bool ShowProviderColors =>
        ThemeCatalog.ParseOption(Settings.ColorMode, ChartColorMode.Accent) == ChartColorMode.Provider;

    public bool ShowPace
    {
        get => Settings.ShowPace;
        set => Change(settings => settings.ShowPace = value);
    }

    public bool AvailableFirst
    {
        get => Settings.AvailableFirst;
        set => Change(settings => settings.AvailableFirst = value);
    }

    public bool NotifyOnRecovery
    {
        get => Settings.NotifyOnRecovery;
        set => Change(settings => settings.NotifyOnRecovery = value);
    }

    public bool DynamicTrayIcon
    {
        get => Settings.DynamicTrayIcon;
        set => Change(settings => settings.DynamicTrayIcon = value);
    }

    public bool StartWithWindows
    {
        get => StartupRegistration.IsEnabled;
        set
        {
            try
            {
                if (value)
                {
                    StartupRegistration.Enable();
                }
                else
                {
                    StartupRegistration.Disable();
                }
            }
            catch (Exception)
            {
                // A locked-down registry leaves the switch showing the real state below.
            }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StartWithWindows)));
        }
    }

    public string SettingsLocation => Path.Combine("%LOCALAPPDATA%", "CodexBar", "settings.json");

    public string FooterText => Loc.F("SettingsFooter", AppVersion, SettingsLocation);

    public static string AppVersion
    {
        get
        {
            string? version = Assembly.GetEntryAssembly()?
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            return string.IsNullOrWhiteSpace(version) ? "dev" : version.Split('+')[0];
        }
    }

    private AppSettings Settings => _store.Settings;

    public void SetAccent(string hex)
    {
        Change(settings => settings.AccentColor = hex);
    }

    public void SetProviderColor(string provider, string hex)
    {
        Change(settings =>
        {
            if (provider == "claude")
            {
                settings.ClaudeColor = hex;
            }
            else
            {
                settings.CodexColor = hex;
            }
        });
    }

    /// <summary>Synchronizes selection marks and previews with the stored settings.</summary>
    public void Refresh()
    {
        AppSettings settings = Settings;
        AppearancePalette palette = _owner.Palette;
        foreach (OptionViewModel theme in Themes)
        {
            theme.IsSelected = theme.Value == settings.Theme;
            ThemeDefinition definition = ThemeCatalog.Find(theme.Value);
            theme.PreviewAccent = palette.Brush(palette.Accent.EnsureContrast(RgbColor.Parse(definition.Background), 3));
            theme.PreviewText = palette.Brush(RgbColor.Parse(definition.Text));
            theme.RaisePreviewChanged();
        }

        foreach (OptionViewModel accent in Accents)
        {
            accent.IsSelected = accent.Value.Equals(settings.AccentColor, StringComparison.OrdinalIgnoreCase);
        }

        Select(Charts, settings.ChartKind);
        Select(ColorModes, settings.ColorMode);
        Select(PercentModes, settings.ShowUsedPercent ? "used" : "available");
        Select(Views, settings.ViewMode);
        Select(Densities, settings.Density);
        Select(Languages, settings.Language);
        foreach (OptionViewModel option in Accents.Concat(Charts).Concat(ColorModes).Concat(PercentModes)
            .Concat(Views).Concat(Densities).Concat(Languages))
        {
            option.RaisePreviewChanged();
        }
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    private void Change(Action<AppSettings> change)
    {
        _store.Update(change);
        _owner.ReloadAppearance();
    }

    private static void Select(IEnumerable<OptionViewModel> options, string? value)
    {
        foreach (OptionViewModel option in options)
        {
            option.IsSelected = option.Value.Equals(value, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static OptionViewModel[] Choices<TEnum>(
        (TEnum Value, string LabelKey)[] choices,
        Action<string> apply)
        where TEnum : struct, Enum
    {
        return choices
            .Select(choice =>
            {
                string value = ThemeCatalog.FormatOption(choice.Value);
                return new OptionViewModel(value, () => Loc.T(choice.LabelKey), () => apply(value));
            })
            .ToArray();
    }

    private OptionViewModel CreateThemeOption(ThemeDefinition theme)
    {
        string labelKey = "Theme" + char.ToUpperInvariant(theme.Id[0]) + theme.Id[1..];
        return new OptionViewModel(theme.Id, () => Loc.T(labelKey), () => Change(settings => settings.Theme = theme.Id))
        {
            PreviewBackground = Frozen(RgbColor.Parse(theme.Background)),
            PreviewFrame = Frozen(RgbColor.Parse(theme.Border)),
            PreviewTrack = Frozen(RgbColor.Parse(theme.Track)),
        };
    }

    private static Brush Frozen(RgbColor color)
    {
        var brush = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }
}
