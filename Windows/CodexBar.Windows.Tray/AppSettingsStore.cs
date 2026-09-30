using System.IO;
using System.Text.Json;
using CodexBar.EngineClient;

namespace CodexBar.Windows.Tray;

public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _path;

    public AppSettingsStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodexBar",
            "settings.json");
        Settings = Load();
    }

    public AppSettings Settings { get; }

    public string DisplayName(ProviderProfile profile)
    {
        return Settings.AccountNames.GetValueOrDefault(profile.Key) is { Length: > 0 } name
            ? name
            : profile.Label;
    }

    public void SetDisplayName(ProviderProfile profile, string value)
    {
        string name = value.Trim();
        if (string.IsNullOrWhiteSpace(name) || name == profile.Label)
        {
            Settings.AccountNames.Remove(profile.Key);
        }
        else
        {
            Settings.AccountNames[profile.Key] = name;
        }
        Save();
    }

    public void Update(Action<AppSettings> change)
    {
        change(Settings);
        Settings.Normalize();
        Save();
    }

    public void SetAlwaysOnTop(bool value)
    {
        Settings.AlwaysOnTop = value;
        Save();
    }

    public void SetWindowPosition(double left, double top)
    {
        Settings.WindowLeft = left;
        Settings.WindowTop = top;
        Save();
    }

    public void SetUiScale(double value)
    {
        Settings.UiScale = value;
        Save();
    }

    private AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return CreateDefaults();
            }

            string json = File.ReadAllText(_path);
            AppSettings settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions)
                ?? new AppSettings();
            settings.Normalize();
            return settings;
        }
        catch (IOException)
        {
            return CreateDefaults();
        }
        catch (UnauthorizedAccessException)
        {
            return CreateDefaults();
        }
        catch (JsonException)
        {
            return CreateDefaults();
        }
    }

    private static AppSettings CreateDefaults()
    {
        var settings = new AppSettings();
        settings.Normalize();
        return settings;
    }

    private void Save()
    {
        try
        {
            string? directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string temporaryPath = $"{_path}.tmp";
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(Settings, SerializerOptions));
            File.Move(temporaryPath, _path, overwrite: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

public sealed class AppSettings
{
    public Dictionary<string, string> AccountNames { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Legacy flag kept in sync with <see cref="ViewMode"/> for older builds.</summary>
    public bool CompactView { get; set; }

    public string? ViewMode { get; set; }

    public bool AlwaysOnTop { get; set; }

    public double UiScale { get; set; } = 1.0;

    /// <summary>Main window opacity, from 0.4 (mostly see-through) to 1.0 (solid).</summary>
    public double WindowOpacity { get; set; } = 1.0;

    public double? WindowLeft { get; set; }

    public double? WindowTop { get; set; }

    public string? Theme { get; set; }

    public string? AccentColor { get; set; }

    public string? ChartKind { get; set; }

    public string? ColorMode { get; set; }

    public string? CodexColor { get; set; }

    public string? ClaudeColor { get; set; }

    public string? Density { get; set; }

    public bool ShowUsedPercent { get; set; }

    public bool ShowPace { get; set; } = true;

    public bool AvailableFirst { get; set; } = true;

    public bool NotifyOnRecovery { get; set; } = true;

    public bool DynamicTrayIcon { get; set; } = true;

    /// <summary>"auto" (follow Windows), "es" or "en".</summary>
    public string? Language { get; set; }

    public void Normalize()
    {
        AccountNames = new Dictionary<string, string>(
            AccountNames ?? new Dictionary<string, string>(),
            StringComparer.OrdinalIgnoreCase);
        if (!double.IsFinite(UiScale) ||
            UiScale < MainWindowViewModel.MinimumUiScale ||
            UiScale > MainWindowViewModel.MaximumUiScale)
        {
            UiScale = 1.0;
        }

        Tray.ViewMode view = ThemeCatalog.ParseOption(
            ViewMode,
            CompactView ? Tray.ViewMode.Summary : Tray.ViewMode.Cards);
        ViewMode = ThemeCatalog.FormatOption(view);
        CompactView = view == Tray.ViewMode.Summary;
        Theme = ThemeCatalog.Find(Theme).Id;
        AccentColor = NormalizeColor(AccentColor, ThemeCatalog.DefaultAccent);
        CodexColor = NormalizeColor(CodexColor, ThemeCatalog.DefaultCodexColor);
        ClaudeColor = NormalizeColor(ClaudeColor, ThemeCatalog.DefaultClaudeColor);
        ChartKind = ThemeCatalog.FormatOption(ThemeCatalog.ParseOption(ChartKind, Tray.ChartKind.Bar));
        ColorMode = ThemeCatalog.FormatOption(ThemeCatalog.ParseOption(ColorMode, ChartColorMode.Accent));
        Density = ThemeCatalog.FormatOption(ThemeCatalog.ParseOption(Density, Tray.Density.Comfortable));
        Language = Language is Loc.Spanish or Loc.English ? Language : Loc.Automatic;
        WindowOpacity = double.IsFinite(WindowOpacity)
            ? Math.Clamp(Math.Round(WindowOpacity, 2), MainWindowViewModel.MinimumWindowOpacity, 1.0)
            : 1.0;
    }

    public void ResetAppearance()
    {
        Theme = null;
        AccentColor = null;
        CodexColor = null;
        ClaudeColor = null;
        ChartKind = null;
        ColorMode = null;
        Density = null;
        ViewMode = null;
        CompactView = false;
        UiScale = 1.0;
        WindowOpacity = 1.0;
        ShowUsedPercent = false;
        ShowPace = true;
        AvailableFirst = true;
        NotifyOnRecovery = true;
        DynamicTrayIcon = true;
    }

    private static string NormalizeColor(string? value, string fallback)
    {
        return RgbColor.TryParse(value, out RgbColor color) ? color.ToHex() : fallback;
    }
}
