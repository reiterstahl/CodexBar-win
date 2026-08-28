using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using CodexBar.EngineClient;

namespace CodexBar.Windows.Tray;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    public const double MinimumUiScale = 0.8;
    public const double MaximumUiScale = 1.6;

    private readonly AppSettingsStore _settingsStore;
    private bool _isAlwaysOnTop;
    private bool _isCompact;
    private bool _isRefreshing;
    private bool _isSettingsOpen;
    private string _status = "Starting…";
    private double _uiScale;

    public MainWindowViewModel(AppSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
        _isAlwaysOnTop = settingsStore.Settings.AlwaysOnTop;
        _isCompact = settingsStore.Settings.CompactView;
        _uiScale = settingsStore.Settings.UiScale;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ProviderCardViewModel> Providers { get; } = [];

    public string Status
    {
        get => _status;
        private set => SetField(ref _status, value);
    }

    public bool CanRefresh => !_isRefreshing;

    public bool IsAlwaysOnTop
    {
        get => _isAlwaysOnTop;
        set
        {
            if (!SetField(ref _isAlwaysOnTop, value))
            {
                return;
            }
            _settingsStore.SetAlwaysOnTop(value);
        }
    }

    public bool IsCompact
    {
        get => _isCompact;
        private set
        {
            if (!SetField(ref _isCompact, value))
            {
                return;
            }
            OnPropertyChanged(nameof(CompactButtonLabel));
            _settingsStore.SetCompactView(value);
        }
    }

    public string CompactButtonLabel => IsCompact ? "Cards" : "Summary";

    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        private set => SetField(ref _isSettingsOpen, value);
    }

    public double UiScale
    {
        get => _uiScale;
        private set
        {
            double scale = Math.Clamp(
                Math.Round(value, 1),
                MinimumUiScale,
                MaximumUiScale);
            if (!SetField(ref _uiScale, scale))
            {
                return;
            }

            OnPropertyChanged(nameof(UiScaleLabel));
            OnPropertyChanged(nameof(CanDecreaseUiScale));
            OnPropertyChanged(nameof(CanIncreaseUiScale));
            _settingsStore.SetUiScale(scale);
        }
    }

    public string UiScaleLabel => $"{UiScale:P0}";

    public bool CanDecreaseUiScale => UiScale > MinimumUiScale;

    public bool CanIncreaseUiScale => UiScale < MaximumUiScale;

    public void ToggleCompact()
    {
        bool compact = !IsCompact;
        if (compact)
        {
            IsSettingsOpen = false;
        }
        IsCompact = compact;
    }

    public void ToggleSettings()
    {
        IsSettingsOpen = !IsSettingsOpen;
    }

    public void DecreaseUiScale()
    {
        UiScale -= 0.1;
    }

    public void IncreaseUiScale()
    {
        UiScale += 0.1;
    }

    public void ResetUiScale()
    {
        UiScale = 1.0;
    }

    public void BeginRefresh()
    {
        _isRefreshing = true;
        Status = "Refreshing…";
        OnPropertyChanged(nameof(CanRefresh));
    }

    public void EndRefresh()
    {
        _isRefreshing = false;
        OnPropertyChanged(nameof(CanRefresh));
    }

    public void Apply(IReadOnlyList<ProviderProfileResult> results)
    {
        Providers.Clear();

        foreach (ProviderProfileResult result in results)
        {
            string displayName = _settingsStore.DisplayName(result.Profile);
            Providers.Add(result.Snapshot is not null
                ? ProviderCardViewModel.FromSnapshot(
                    result.Profile,
                    displayName,
                    result.Snapshot,
                    RenameProfile)
                : ProviderCardViewModel.FromFailure(
                    result.Profile,
                    displayName,
                    result.Failure,
                    RenameProfile));
        }

        DateTimeOffset generatedAt = results.Count == 0
            ? DateTimeOffset.Now
            : results.Max(result => result.GeneratedAt);
        Status = $"Updated {generatedAt.ToLocalTime():t}";
    }

    public void ApplyError(string message)
    {
        Status = message;
    }

    public void ShowStatus(string message)
    {
        Status = message;
    }

    public void RefreshTimeLabels()
    {
        foreach (ProviderCardViewModel provider in Providers)
        {
            provider.RefreshTimeLabels();
        }
    }

    private void RenameProfile(ProviderProfile profile, string name)
    {
        _settingsStore.SetDisplayName(profile, name);
    }

    private bool SetField<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class ProviderCardViewModel : INotifyPropertyChanged
{
    private readonly Action<ProviderProfile, string> _rename;
    private string _displayName;

    private ProviderCardViewModel(
        ProviderProfile profile,
        string displayName,
        string identity,
        string errorMessage,
        bool requiresLogin,
        IReadOnlyList<RateWindowViewModel> windows,
        Action<ProviderProfile, string> rename)
    {
        Profile = profile;
        _displayName = displayName;
        Identity = identity;
        ErrorMessage = errorMessage;
        RequiresLogin = requiresLogin;
        Windows = windows;
        _rename = rename;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ProviderProfile Profile { get; }

    public string DisplayName
    {
        get => _displayName;
        set
        {
            string name = string.IsNullOrWhiteSpace(value) ? Profile.Label : value.Trim();
            if (_displayName == name)
            {
                return;
            }

            _displayName = name;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
            _rename(Profile, name);
        }
    }

    public string Identity { get; }

    public string ErrorMessage { get; }

    public bool HasError => ErrorMessage.Length > 0;

    public bool RequiresLogin { get; }

    public IReadOnlyList<RateWindowViewModel> Windows { get; }

    public RateWindowViewModel? SessionWindow =>
        Windows.FirstOrDefault(window => window.IsSession);

    public string LoginCommand => Profile.Provider switch
    {
        "codex" => string.Join(
            Environment.NewLine,
            $"$env:CODEX_HOME = {PowerShellLiteral(Profile.ConfigDirectory)}",
            "codex login --device-auth",
            "codex login status",
            "Remove-Item Env:CODEX_HOME"),
        "claude" => string.Join(
            Environment.NewLine,
            $"$env:CLAUDE_CONFIG_DIR = {PowerShellLiteral(Profile.ConfigDirectory)}",
            "Write-Host 'When Claude asks for the code, paste with right-click or Shift+Insert. The code may remain invisible.'",
            "claude auth login",
            "claude auth status",
            "Remove-Item Env:CLAUDE_CONFIG_DIR"),
        _ => string.Empty,
    };

    public string CompactSummary
    {
        get
        {
            if (ErrorMessage.Length > 0)
            {
                return ErrorMessage;
            }

            RateWindowViewModel[] orderedWindows = Windows
                .OrderByDescending(window => window.IsSession)
                .ToArray();
            return string.Join(
                "  •  ",
                orderedWindows.Select((window, index) => index == 0
                    ? $"{window.Label}: {window.CountdownLabel} · " +
                        $"{window.ResetDateLabel} · {window.RemainingLabel}"
                    : window.ResetDateLabel.Length > 0
                        ? $"{window.Label}: {window.ResetDateLabel} · {window.RemainingLabel}"
                        : $"{window.Label}: {window.CountdownLabel} · {window.RemainingLabel}"));
        }
    }

    public void RefreshTimeLabels()
    {
        foreach (RateWindowViewModel window in Windows)
        {
            window.RefreshTimeLabel();
        }
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CompactSummary)));
    }

    private static string PowerShellLiteral(string value)
    {
        return $"'{value.Replace("'", "''")}'";
    }

    public static ProviderCardViewModel FromSnapshot(
        ProviderProfile profile,
        string displayName,
        ProviderSnapshot snapshot,
        Action<ProviderProfile, string> rename)
    {
        string identity = ShortIdentity(snapshot.Identity);
        IEnumerable<RateWindow> visibleWindows = snapshot.Windows.Where(window =>
            window.Id.Equals("session", StringComparison.OrdinalIgnoreCase) ||
            window.Id.Equals("weekly", StringComparison.OrdinalIgnoreCase));
        return new ProviderCardViewModel(
            profile,
            displayName,
            identity,
            string.Empty,
            false,
            visibleWindows.Select(RateWindowViewModel.FromSnapshot).ToArray(),
            rename);
    }

    public static ProviderCardViewModel FromFailure(
        ProviderProfile profile,
        string displayName,
        ProviderFailure? failure,
        Action<ProviderProfile, string> rename)
    {
        string message = failure?.Message ?? "Provider data is unavailable.";
        bool requiresLogin = failure?.Code is
            "credentials_not_found" or
            "credentials_invalid" or
            "credentials_expired" or
            "unauthorized";
        return new ProviderCardViewModel(
            profile,
            displayName,
            string.Empty,
            message,
            requiresLogin,
            [],
            rename);
    }

    private static string ShortIdentity(ProviderIdentity? identity)
    {
        string? email = identity?.AccountEmail?.Trim();
        if (!string.IsNullOrEmpty(email))
        {
            int separator = email.IndexOf('@');
            return separator > 0 ? email[..separator] : email;
        }

        return identity?.Plan?.Trim() ?? string.Empty;
    }
}

public sealed class RateWindowViewModel : INotifyPropertyChanged
{
    private static readonly CultureInfo SpanishCulture =
        CultureInfo.GetCultureInfo("es-CR");

    private RateWindowViewModel(
        string id,
        string label,
        double usedPercent,
        string remainingLabel,
        DateTimeOffset? resetsAt)
    {
        Id = id;
        Label = label;
        UsedPercent = usedPercent;
        RemainingLabel = remainingLabel;
        ResetsAt = resetsAt;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; }

    public string Label { get; }

    public double UsedPercent { get; }

    public string UsedPercentLabel => string.Create(
        CultureInfo.CurrentCulture,
        $"{UsedPercent:0.#}%");

    public string RemainingLabel { get; }

    public DateTimeOffset? ResetsAt { get; }

    public bool IsSession => Id.Equals("session", StringComparison.OrdinalIgnoreCase);

    public string CountdownLabel => FormatCountdown(ResetsAt, DateTimeOffset.Now);

    public string CompactCountdownLabel => FormatCompactCountdown(
        ResetsAt,
        DateTimeOffset.Now);

    public string ResetDateLabel => ResetsAt is null
        ? string.Empty
        : ResetsAt.Value.ToLocalTime().ToString(
            "dddd d 'de' MMMM, h:mm tt",
            SpanishCulture);

    public static RateWindowViewModel FromSnapshot(RateWindow window)
    {
        string remaining = string.Create(
            CultureInfo.CurrentCulture,
            $"{window.RemainingPercent:0.#}% disponible");
        return new RateWindowViewModel(
            window.Id,
            window.Label,
            window.UsedPercent,
            remaining,
            window.ResetsAt);
    }

    public void RefreshTimeLabel()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CountdownLabel)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CompactCountdownLabel)));
    }

    internal static string FormatCompactCountdown(
        DateTimeOffset? resetsAt,
        DateTimeOffset now)
    {
        if (resetsAt is null)
        {
            return "En --:--";
        }

        TimeSpan remaining = resetsAt.Value - now;
        if (remaining <= TimeSpan.Zero)
        {
            return "En 00:00";
        }

        int totalMinutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
        int hours = totalMinutes / 60;
        int minutes = totalMinutes % 60;
        return $"En {hours:00}:{minutes:00}";
    }

    internal static string FormatCountdown(
        DateTimeOffset? resetsAt,
        DateTimeOffset now)
    {
        if (resetsAt is null)
        {
            return "Renovación no disponible";
        }

        TimeSpan remaining = resetsAt.Value - now;
        if (remaining <= TimeSpan.Zero)
        {
            return "Renovando ahora";
        }

        int totalMinutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
        if (totalMinutes <= 60)
        {
            return $"Renueva en {totalMinutes} min";
        }

        int totalHours = totalMinutes / 60;
        int minutes = totalMinutes % 60;
        if (totalHours < 24)
        {
            return minutes == 0
                ? $"Renueva en {totalHours} h"
                : $"Renueva en {totalHours} h {minutes} min";
        }

        int days = totalHours / 24;
        int hours = totalHours % 24;
        return hours == 0
            ? $"Renueva en {days} d"
            : $"Renueva en {days} d {hours} h";
    }
}
