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

    public void SetCompactView(bool value)
    {
        Settings.CompactView = value;
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
                return new AppSettings();
            }

            string json = File.ReadAllText(_path);
            AppSettings settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions)
                ?? new AppSettings();
            settings.AccountNames = new Dictionary<string, string>(
                settings.AccountNames ?? new Dictionary<string, string>(),
                StringComparer.OrdinalIgnoreCase);
            if (!double.IsFinite(settings.UiScale) ||
                settings.UiScale < MainWindowViewModel.MinimumUiScale ||
                settings.UiScale > MainWindowViewModel.MaximumUiScale)
            {
                settings.UiScale = 1.0;
            }
            return settings;
        }
        catch (IOException)
        {
            return new AppSettings();
        }
        catch (UnauthorizedAccessException)
        {
            return new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
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

    public bool CompactView { get; set; }

    public bool AlwaysOnTop { get; set; }

    public double UiScale { get; set; } = 1.0;

    public double? WindowLeft { get; set; }

    public double? WindowTop { get; set; }
}
