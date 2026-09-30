using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using CodexBar.EngineClient;
using Brush = System.Windows.Media.Brush;

namespace CodexBar.Windows.Tray;

public sealed record QuotaRecoveredEventArgs(string DisplayName, string Detail);

public sealed record TrayIconState(
    double SessionRemaining,
    double WeeklyRemaining,
    bool HasExhaustedAccount,
    bool HasRecoveredAccount);

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    public const double MinimumUiScale = 0.8;
    public const double MaximumUiScale = 1.6;

    private readonly AppSettingsStore _settingsStore;
    private readonly Dictionary<string, bool> _accountAvailability =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _recoveryAlerts =
        new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<ProviderCardViewModel> _cards = [];
    private bool _hasQuotaBaseline;
    private bool _isAlwaysOnTop;
    private bool _isRefreshing;
    private bool _recoveryPulseOn = true;
    private string? _transientStatus;
    private DateTimeOffset? _lastUpdated;
    private double _uiScale;
    private string? _updateVersion;
    private AppearancePalette _palette;
    private AppearanceOptions _options;

    public MainWindowViewModel(AppSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
        _isAlwaysOnTop = settingsStore.Settings.AlwaysOnTop;
        _uiScale = settingsStore.Settings.UiScale;
        _palette = AppearancePalette.Create(settingsStore.Settings);
        _options = ReadOptions(settingsStore.Settings);
        Loc.Instance.SetLanguage(settingsStore.Settings.Language);
        Customization = new CustomizationViewModel(settingsStore, this);
        SelectViewCommand = new ViewCommands(this);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler<QuotaRecoveredEventArgs>? QuotaRecovered;

    public event EventHandler? AppearanceChanged;

    public ObservableCollection<ProviderCardViewModel> Providers { get; } = [];

    public CustomizationViewModel Customization { get; }

    public ViewCommands SelectViewCommand { get; }

    public AppSettings Settings => _settingsStore.Settings;

    public AppearancePalette Palette => _palette;

    public AppearanceOptions Options => _options;

    public ViewMode ViewMode => _options.View;

    public bool IsCardsView => _options.View == ViewMode.Cards;

    public bool IsSummaryView => _options.View == ViewMode.Summary;

    public bool IsMiniView => _options.View == ViewMode.Mini;

    public double BaseWidth => _options.View switch
    {
        ViewMode.Summary => 760,
        ViewMode.Mini => 440,
        _ => 520,
    };

    public string RefreshButtonLabel => Loc.T(_isRefreshing ? "Refreshing" : "Refresh");

    public string Status
    {
        get
        {
            if (_isRefreshing)
            {
                return Loc.T("Refreshing");
            }

            if (_transientStatus is not null)
            {
                return _transientStatus;
            }

            if (_lastUpdated is not DateTimeOffset updated)
            {
                return Loc.T("StatusStarting");
            }

            int minutes = (int)Math.Floor((DateTimeOffset.Now - updated).TotalMinutes);
            string age = minutes < 1 ? Loc.T("AgeJustNow") : Loc.F("AgeMinutes", minutes);
            int available = _cards.Count(card => card.HasQuota);
            return Loc.F("StatusUpdated", age, available, _cards.Count);
        }
    }

    public string FooterText
    {
        get
        {
            ProviderCardViewModel? next = NextRecovery();
            if (next?.ExhaustedWindow?.ResetsAt is DateTimeOffset resetsAt)
            {
                return Loc.F(
                    "FooterNext",
                    next.DisplayName,
                    UsageMath.FormatDuration(resetsAt - DateTimeOffset.Now));
            }

            return Loc.T(_cards.Count > 0 && _cards.All(card => card.HasQuota)
                ? "FooterAllAvailable"
                : "FooterAuto");
        }
    }

    public Brush FooterDotBrush => NextRecovery() is null
        ? _palette.Brush(_palette.AccentFill)
        : _palette.Brush(_palette.Status.Warn);

    public bool HasUpdate => _updateVersion is not null;

    public string UpdateText => _updateVersion is null
        ? string.Empty
        : Loc.F("UpdateReady", _updateVersion);

    public bool CanRefresh => !_isRefreshing;

    public bool IsRefreshing => _isRefreshing;

    public bool HasRecoveryAlerts => _recoveryAlerts.Count > 0;

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

    public double UiScale
    {
        get => _uiScale;
        set
        {
            double scale = Math.Clamp(
                Math.Round(value, 1),
                MinimumUiScale,
                MaximumUiScale);
            if (!SetField(ref _uiScale, scale))
            {
                return;
            }

            OnPropertyChanged(nameof(UiScalePercent));
            _settingsStore.SetUiScale(scale);
        }
    }

    public double UiScalePercent
    {
        get => Math.Round(UiScale * 100);
        set => UiScale = value / 100;
    }

    public void SelectView(ViewMode view)
    {
        if (view == _options.View)
        {
            return;
        }

        _settingsStore.Update(settings => settings.ViewMode = ThemeCatalog.FormatOption(view));
        ReloadAppearance();
    }

    /// <summary>Re-reads appearance settings and restyles every card in place.</summary>
    public void ReloadAppearance()
    {
        _palette = AppearancePalette.Create(_settingsStore.Settings);
        _options = ReadOptions(_settingsStore.Settings);
        Loc.Instance.SetLanguage(_settingsStore.Settings.Language);
        if (_uiScale != _settingsStore.Settings.UiScale)
        {
            _uiScale = _settingsStore.Settings.UiScale;
            OnPropertyChanged(nameof(UiScale));
            OnPropertyChanged(nameof(UiScalePercent));
        }

        foreach (ProviderCardViewModel card in _cards)
        {
            card.ApplyStyle(_palette, _options);
        }
        PublishCards();
        OnPropertyChanged(nameof(Palette));
        OnPropertyChanged(nameof(Options));
        OnPropertyChanged(nameof(ViewMode));
        OnPropertyChanged(nameof(IsCardsView));
        OnPropertyChanged(nameof(IsSummaryView));
        OnPropertyChanged(nameof(IsMiniView));
        OnPropertyChanged(nameof(BaseWidth));
        OnPropertyChanged(nameof(FooterDotBrush));
        OnPropertyChanged(nameof(FooterText));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(UpdateText));
        OnPropertyChanged(nameof(RefreshButtonLabel));
        Customization.Refresh();
        AppearanceChanged?.Invoke(this, EventArgs.Empty);
    }

    public void BeginRefresh()
    {
        _isRefreshing = true;
        _transientStatus = null;
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(CanRefresh));
        OnPropertyChanged(nameof(IsRefreshing));
        OnPropertyChanged(nameof(RefreshButtonLabel));
    }

    public void EndRefresh()
    {
        _isRefreshing = false;
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(CanRefresh));
        OnPropertyChanged(nameof(IsRefreshing));
        OnPropertyChanged(nameof(RefreshButtonLabel));
    }

    public void Apply(IReadOnlyList<ProviderProfileResult> results)
    {
        var cards = new List<ProviderCardViewModel>(results.Count);
        var observedProfileKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var recovered = new List<ProviderCardViewModel>();

        foreach (ProviderProfileResult result in results)
        {
            string profileKey = result.Profile.Key;
            observedProfileKeys.Add(profileKey);
            bool isAvailable = result.Snapshot is not null &&
                HasUsableQuota(result.Snapshot);
            bool becameAvailable = false;

            if (result.Snapshot is not null)
            {
                if (_hasQuotaBaseline &&
                    isAvailable &&
                    _accountAvailability.TryGetValue(profileKey, out bool wasAvailable) &&
                    !wasAvailable)
                {
                    _recoveryAlerts.Add(profileKey);
                    _recoveryPulseOn = true;
                    becameAvailable = true;
                }

                if (!isAvailable)
                {
                    _recoveryAlerts.Remove(profileKey);
                }
                _accountAvailability[profileKey] = isAvailable;
            }
            else
            {
                _recoveryAlerts.Remove(profileKey);
            }

            string displayName = _settingsStore.DisplayName(result.Profile);
            bool recoveryAlertActive = _recoveryAlerts.Contains(profileKey);
            ProviderCardViewModel card = result.Snapshot is not null
                ? ProviderCardViewModel.FromSnapshot(
                    result.Profile,
                    displayName,
                    result.Snapshot,
                    isExhausted: !isAvailable,
                    recoveryAlertActive,
                    recoveryPulseVisible: recoveryAlertActive && _recoveryPulseOn,
                    RenameProfile)
                : ProviderCardViewModel.FromFailure(
                    result.Profile,
                    displayName,
                    result.Failure,
                    RenameProfile);
            card.ApplyStyle(_palette, _options);
            cards.Add(card);
            if (becameAvailable)
            {
                recovered.Add(card);
            }
        }

        foreach (string staleProfileKey in _accountAvailability.Keys
            .Where(key => !observedProfileKeys.Contains(key))
            .ToArray())
        {
            _accountAvailability.Remove(staleProfileKey);
            _recoveryAlerts.Remove(staleProfileKey);
        }

        _cards = cards;
        PublishCards();

        _hasQuotaBaseline = true;
        _transientStatus = null;
        _lastUpdated = results.Count == 0
            ? DateTimeOffset.Now
            : results.Max(result => result.GeneratedAt);
        OnPropertyChanged(nameof(HasRecoveryAlerts));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(FooterText));
        OnPropertyChanged(nameof(FooterDotBrush));

        foreach (ProviderCardViewModel card in recovered)
        {
            RateWindowViewModel? session = card.SessionWindow;
            string detail = session is null
                ? Loc.T("RecoveredGeneric")
                : Loc.F("RecoveredSession", Math.Round(session.RemainingPercent));
            QuotaRecovered?.Invoke(this, new QuotaRecoveredEventArgs(card.DisplayName, detail));
        }
    }

    public TrayIconState? CurrentTrayState()
    {
        ProviderCardViewModel[] withData = _cards.Where(card => card.Windows.Count > 0).ToArray();
        if (withData.Length == 0)
        {
            return null;
        }

        ProviderCardViewModel limiting = withData
            .OrderBy(card => card.SessionWindow?.RemainingPercent ?? 100)
            .ThenBy(card => card.WeeklyWindow?.RemainingPercent ?? 100)
            .First();
        return new TrayIconState(
            limiting.SessionWindow?.RemainingPercent ?? 100,
            limiting.WeeklyWindow?.RemainingPercent ?? 100,
            withData.Any(card => card.IsExhausted),
            HasRecoveryAlerts);
    }

    public void SetUpdateReady(string version)
    {
        _updateVersion = version;
        OnPropertyChanged(nameof(HasUpdate));
        OnPropertyChanged(nameof(UpdateText));
    }

    public void ApplyError(string message)
    {
        ShowStatus(message);
    }

    public void ShowStatus(string message)
    {
        _transientStatus = message;
        OnPropertyChanged(nameof(Status));
    }

    public void RefreshTimeLabels()
    {
        foreach (ProviderCardViewModel provider in _cards)
        {
            provider.RefreshTimeLabels();
        }
        _transientStatus = null;
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(FooterText));
    }

    public void ToggleRecoveryPulse()
    {
        if (!HasRecoveryAlerts)
        {
            return;
        }

        _recoveryPulseOn = !_recoveryPulseOn;
        foreach (ProviderCardViewModel provider in _cards)
        {
            provider.SetRecoveryPulse(
                _recoveryAlerts.Contains(provider.Profile.Key) && _recoveryPulseOn);
        }
    }

    public void DismissRecoveryAlert(ProviderCardViewModel provider)
    {
        if (!_recoveryAlerts.Remove(provider.Profile.Key))
        {
            return;
        }

        provider.SetRecoveryAlert(active: false, pulseVisible: false);
        OnPropertyChanged(nameof(HasRecoveryAlerts));
        AppearanceChanged?.Invoke(this, EventArgs.Empty);
    }

    private ProviderCardViewModel? NextRecovery()
    {
        return _cards
            .Where(card => card.ExhaustedWindow?.ResetsAt is not null)
            .OrderBy(card => card.ExhaustedWindow!.ResetsAt)
            .FirstOrDefault();
    }

    private void PublishCards()
    {
        IEnumerable<ProviderCardViewModel> ordered = Settings.AvailableFirst
            ? _cards.OrderBy(card => card.HasError ? 2 : card.IsExhausted ? 1 : 0)
            : _cards;
        Providers.Clear();
        foreach (ProviderCardViewModel card in ordered)
        {
            Providers.Add(card);
        }
    }

    private void RenameProfile(ProviderProfile profile, string name)
    {
        _settingsStore.SetDisplayName(profile, name);
    }

    private static AppearanceOptions ReadOptions(AppSettings settings)
    {
        return new AppearanceOptions(
            ThemeCatalog.ParseOption(settings.ViewMode, ViewMode.Cards),
            ThemeCatalog.ParseOption(settings.ChartKind, ChartKind.Bar),
            ThemeCatalog.ParseOption(settings.ColorMode, ChartColorMode.Accent),
            ThemeCatalog.ParseOption(settings.Density, Density.Comfortable),
            settings.ShowUsedPercent,
            settings.ShowPace);
    }

    private static bool HasUsableQuota(ProviderSnapshot snapshot)
    {
        RateWindow[] primaryWindows = snapshot.Windows
            .Where(window =>
                window.Id.Equals("session", StringComparison.OrdinalIgnoreCase) ||
                window.Id.Equals("weekly", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return primaryWindows.Length > 0 &&
            primaryWindows.All(window => window.RemainingPercent > 0);
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

    public sealed class ViewCommands(MainWindowViewModel owner)
    {
        public RelayCommand Cards { get; } = new(() => owner.SelectView(ViewMode.Cards));

        public RelayCommand Summary { get; } = new(() => owner.SelectView(ViewMode.Summary));

        public RelayCommand Mini { get; } = new(() => owner.SelectView(ViewMode.Mini));
    }
}

public sealed class ProviderCardViewModel : INotifyPropertyChanged
{
    private readonly Action<ProviderProfile, string> _rename;
    private readonly string _failureMessage;
    private string _displayName;
    private bool _isRecoveryAlertActive;
    private bool _isRecoveryPulseVisible;
    private AppearancePalette? _palette;
    private AppearanceOptions? _options;

    private ProviderCardViewModel(
        ProviderProfile profile,
        string displayName,
        string subtitle,
        string errorMessage,
        bool requiresLogin,
        bool isExhausted,
        bool recoveryAlertActive,
        bool recoveryPulseVisible,
        IReadOnlyList<RateWindowViewModel> windows,
        Action<ProviderProfile, string> rename)
    {
        Profile = profile;
        _displayName = displayName;
        Subtitle = subtitle;
        _failureMessage = errorMessage;
        RequiresLogin = requiresLogin;
        IsExhausted = isExhausted;
        _isRecoveryAlertActive = recoveryAlertActive;
        _isRecoveryPulseVisible = recoveryPulseVisible;
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

    public string Subtitle { get; }

    public string ErrorMessage => RequiresLogin
        ? Loc.F("SessionExpired", ProviderName(Profile))
        : _failureMessage;

    public bool HasError => ErrorMessage.Length > 0;

    public bool RequiresLogin { get; }

    public bool IsExhausted { get; }

    public bool HasQuota => !HasError && !IsExhausted && Windows.Count > 0;

    public bool IsRecoveryAlertActive
    {
        get => _isRecoveryAlertActive;
        private set
        {
            if (_isRecoveryAlertActive == value)
            {
                return;
            }
            _isRecoveryAlertActive = value;
            RaiseAll();
        }
    }

    public bool IsRecoveryPulseVisible
    {
        get => _isRecoveryPulseVisible;
        private set
        {
            if (_isRecoveryPulseVisible == value)
            {
                return;
            }
            _isRecoveryPulseVisible = value;
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(nameof(IsRecoveryPulseVisible)));
        }
    }

    public IReadOnlyList<RateWindowViewModel> Windows { get; }

    public IReadOnlyList<RateWindowViewModel> VisibleWindows => View == ViewMode.Mini
        ? Windows.Where(window => window.IsSession).DefaultIfEmpty(Windows.FirstOrDefault())
            .OfType<RateWindowViewModel>().ToArray()
        : Windows;

    public RateWindowViewModel? SessionWindow =>
        Windows.FirstOrDefault(window => window.IsSession);

    public RateWindowViewModel? WeeklyWindow =>
        Windows.FirstOrDefault(window => window.Id.Equals("weekly", StringComparison.OrdinalIgnoreCase));

    public RateWindowViewModel? ExhaustedWindow => Windows
        .Where(window => window.IsExhausted)
        .OrderBy(window => window.ResetsAt ?? DateTimeOffset.MaxValue)
        .FirstOrDefault();

    public bool IsCardsView => View == ViewMode.Cards;

    public bool IsNameReadOnly => View != ViewMode.Cards;

    public bool ShowSubtitle => View == ViewMode.Cards && Subtitle.Length > 0;

    public Dock HeaderDock => View == ViewMode.Summary ? Dock.Left : Dock.Top;

    public double HeaderWidth => View == ViewMode.Summary ? 170 : double.NaN;

    public Thickness HeaderMargin => View switch
    {
        ViewMode.Summary => new Thickness(0, 0, 18, 0),
        ViewMode.Mini => new Thickness(0, 0, 0, 10),
        _ => new Thickness(0, 0, 0, IsCompact ? 10 : 14),
    };

    public Thickness CardPadding => View switch
    {
        ViewMode.Summary => new Thickness(14, 10, 14, 10),
        ViewMode.Mini => new Thickness(14, 12, 14, 12),
        _ => IsCompact ? new Thickness(12, 10, 12, 10) : new Thickness(16, 14, 16, 14),
    };

    public Thickness CardMargin => View == ViewMode.Mini
        ? new Thickness(4)
        : new Thickness(0, 0, 0, IsCompact ? 6 : 8);

    public double NameFontSize => View == ViewMode.Cards ? 14 : 13;

    public int WindowColumns => View == ViewMode.Mini ? 1 : 2;

    public Thickness WindowsMargin => new(-WindowGap / 2, 0, -WindowGap / 2, 0);

    public Brush? ProviderBrush => _palette is null
        ? null
        : _palette.Brush(_palette.ProviderColor(Profile.Provider).EnsureContrast(CardBackground, 3));

    public string PillText
    {
        get
        {
            if (IsRecoveryAlertActive)
            {
                return Loc.T("PillAvailableAgain");
            }

            if (ExhaustedWindow is RateWindowViewModel exhausted)
            {
                return exhausted.ResetsAt is DateTimeOffset resetsAt
                    ? Loc.F("BackIn", UsageMath.FormatDuration(resetsAt - DateTimeOffset.Now))
                    : Loc.T("LimitReached");
            }

            if (RequiresLogin)
            {
                return Loc.T("NoSession");
            }

            return HasError ? Loc.T("NoData") : string.Empty;
        }
    }

    public bool ShowPill => View == ViewMode.Cards && PillText.Length > 0;

    public Brush? PillForeground => _palette is null ? null : _palette.Brush(PillColor);

    public Brush? PillBackground => _palette is null
        ? null
        : _palette.Brush(CardBackground.Mix(PillColor, 0.16));

    public string LoginCommand =>
        LoginCommandBuilder.Build(Profile.Provider, Profile.ConfigDirectory, createDirectory: false);

    private ViewMode View => _options?.View ?? ViewMode.Cards;

    private bool IsCompact => _options?.Density == Density.Compact;

    private double WindowGap => View switch
    {
        ViewMode.Summary => 20,
        ViewMode.Mini => 0,
        _ => IsCompact ? 14 : 20,
    };

    private RgbColor CardBackground
    {
        get
        {
            if (_palette is null)
            {
                return RgbColor.Black;
            }

            if (IsRecoveryAlertActive)
            {
                return _palette.RecoverySurface;
            }

            return IsExhausted ? _palette.ExhaustedSurface : _palette.Surface;
        }
    }

    private RgbColor PillColor
    {
        get
        {
            if (_palette is null)
            {
                return RgbColor.White;
            }

            if (IsRecoveryAlertActive)
            {
                return _palette.Status.Ok.EnsureContrast(CardBackground, 4.5);
            }

            if (ExhaustedWindow is RateWindowViewModel exhausted)
            {
                RgbColor color = exhausted.IsResetSoon ? _palette.Status.Ok : _palette.Status.Warn;
                return color.EnsureContrast(CardBackground, 4.5);
            }

            return RequiresLogin
                ? _palette.Text
                : _palette.Status.Critical.EnsureContrast(CardBackground, 4.5);
        }
    }

    public void ApplyStyle(AppearancePalette palette, AppearanceOptions options)
    {
        _palette = palette;
        _options = options;
        foreach (RateWindowViewModel window in Windows)
        {
            window.ApplyStyle(new WindowStyleContext(
                palette,
                options,
                CardBackground,
                palette.ProviderColor(Profile.Provider),
                WindowGap));
        }
        RaiseAll();
    }

    public void RefreshTimeLabels()
    {
        foreach (RateWindowViewModel window in Windows)
        {
            window.RefreshTimeLabel();
        }
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PillText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PillForeground)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PillBackground)));
    }

    public void SetRecoveryPulse(bool visible)
    {
        IsRecoveryPulseVisible = IsRecoveryAlertActive && visible;
    }

    public void SetRecoveryAlert(bool active, bool pulseVisible)
    {
        IsRecoveryAlertActive = active;
        IsRecoveryPulseVisible = active && pulseVisible;
        if (_palette is not null && _options is not null)
        {
            ApplyStyle(_palette, _options);
        }
    }

    private void RaiseAll()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    public static ProviderCardViewModel FromSnapshot(
        ProviderProfile profile,
        string displayName,
        ProviderSnapshot snapshot,
        bool isExhausted,
        bool recoveryAlertActive,
        bool recoveryPulseVisible,
        Action<ProviderProfile, string> rename)
    {
        IEnumerable<RateWindow> visibleWindows = snapshot.Windows.Where(window =>
            window.Id.Equals("session", StringComparison.OrdinalIgnoreCase) ||
            window.Id.Equals("weekly", StringComparison.OrdinalIgnoreCase));
        return new ProviderCardViewModel(
            profile,
            displayName,
            BuildSubtitle(profile, snapshot.Identity),
            string.Empty,
            false,
            isExhausted,
            recoveryAlertActive,
            recoveryPulseVisible,
            visibleWindows.Select(RateWindowViewModel.FromSnapshot).ToArray(),
            rename);
    }

    public static ProviderCardViewModel FromFailure(
        ProviderProfile profile,
        string displayName,
        ProviderFailure? failure,
        Action<ProviderProfile, string> rename)
    {
        bool requiresLogin = failure?.Code is
            "credentials_not_found" or
            "credentials_invalid" or
            "credentials_expired" or
            "unauthorized";
        // Signed-out cards build their message from the current language on demand.
        string message = requiresLogin
            ? string.Empty
            : failure?.Message ?? Loc.T("NoProviderData");
        return new ProviderCardViewModel(
            profile,
            displayName,
            ProviderName(profile),
            message,
            requiresLogin,
            false,
            false,
            false,
            [],
            rename);
    }

    private static string ProviderName(ProviderProfile profile)
    {
        return profile.Provider == "claude" ? "Claude Code" : "Codex";
    }

    private static string BuildSubtitle(ProviderProfile profile, ProviderIdentity? identity)
    {
        string? email = identity?.AccountEmail?.Trim();
        if (!string.IsNullOrEmpty(email) && email.IndexOf('@') is int separator && separator > 0)
        {
            email = email[..separator];
        }

        string? plan = identity?.Plan?.Trim();
        if (!string.IsNullOrEmpty(plan))
        {
            plan = char.ToUpper(plan[0], CultureInfo.CurrentCulture) + plan[1..];
        }

        return string.Join(
            " · ",
            new[] { ProviderName(profile), plan, email }.Where(part => !string.IsNullOrEmpty(part)));
    }
}

public sealed record WindowStyleContext(
    AppearancePalette Palette,
    AppearanceOptions Options,
    RgbColor CardBackground,
    RgbColor ProviderColor,
    double Gap);

public sealed class RateWindowViewModel : INotifyPropertyChanged
{
    private readonly string _label;
    private WindowStyleContext? _style;

    private RateWindowViewModel(
        string id,
        string label,
        double usedPercent,
        int? windowMinutes,
        DateTimeOffset? resetsAt)
    {
        Id = id;
        _label = label;
        UsedPercent = Math.Clamp(usedPercent, 0, 100);
        WindowMinutes = windowMinutes ?? UsageMath.DefaultWindowMinutes(id);
        ResetsAt = resetsAt;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; }

    public double UsedPercent { get; }

    public double RemainingPercent => 100 - UsedPercent;

    public int? WindowMinutes { get; }

    public bool IsExhausted => RemainingPercent <= 0;

    public DateTimeOffset? ResetsAt { get; }

    public bool IsSession => Id.Equals("session", StringComparison.OrdinalIgnoreCase);

    public string Label
    {
        get
        {
            string name = Id.ToLowerInvariant() switch
            {
                "session" => Loc.T("WindowSession"),
                "weekly" => Loc.T("WindowWeekly"),
                _ => _label,
            };
            return View == ViewMode.Mini && IsSession ? $"{name} · 5 h" : name;
        }
    }

    public double DisplayPercent => ShowUsed ? UsedPercent : RemainingPercent;

    public string PercentLabel => string.Create(
        CultureInfo.CurrentCulture,
        $"{Math.Round(DisplayPercent):0}%");

    public string PercentCaption => Loc.T(ShowUsed ? "CaptionUsed" : "CaptionFree");

    public MeterKind MeterKind => Chart switch
    {
        ChartKind.Ring => MeterKind.Ring,
        ChartKind.Gauge => MeterKind.Gauge,
        ChartKind.Blocks => MeterKind.Blocks,
        _ => MeterKind.Bar,
    };

    public bool IsLinear => Chart is ChartKind.Bar or ChartKind.Blocks;

    public bool IsRadial => Chart is ChartKind.Ring or ChartKind.Gauge;

    public bool IsNumbers => Chart == ChartKind.Numbers;

    public Dock RadialDock => View == ViewMode.Mini ? Dock.Top : Dock.Left;

    public Thickness RadialMargin => View == ViewMode.Mini
        ? new Thickness(0, 0, 0, 8)
        : new Thickness(0, 0, 12, 0);

    public System.Windows.HorizontalAlignment RadialAlignment => View == ViewMode.Mini
        ? System.Windows.HorizontalAlignment.Left
        : System.Windows.HorizontalAlignment.Center;

    public double RadialWidth => Chart == ChartKind.Gauge ? Sizes.Gauge : Sizes.Ring;

    public double RadialHeight => Chart == ChartKind.Gauge
        ? (Sizes.Gauge / 2) + (RadialThickness / 2)
        : Sizes.Ring;

    public double RadialThickness => Chart == ChartKind.Gauge
        ? Math.Round(Sizes.Gauge * 0.14)
        : Math.Round(Sizes.Ring * 0.12);

    public VerticalAlignment RadialLabelAlignment => Chart == ChartKind.Gauge
        ? VerticalAlignment.Bottom
        : VerticalAlignment.Center;

    public double RadialFontSize => Sizes.RadialFont;

    public double LinearThickness => Sizes.Bar;

    public double LinearHeight => Sizes.Bar + (Chart == ChartKind.Blocks ? 6 : 8);

    public int Segments => Sizes.Blocks;

    public double BigFontSize => Sizes.Big;

    public double Marker
    {
        get
        {
            if (!ShowMarker)
            {
                return double.NaN;
            }

            PaceEstimate pace = Pace;
            return ShowUsed ? pace.ElapsedFraction * 100 : (1 - pace.ElapsedFraction) * 100;
        }
    }

    public Brush? FillBrush => _style?.Palette.Brush(BaseColor.EnsureContrast(_style.CardBackground, 3));

    public Brush? PercentBrush => _style?.Palette.Brush(BaseColor.EnsureContrast(_style.CardBackground, 4.5));

    public Brush? TrackBrush => _style?.Palette.Brush(_style.Palette.Track);

    public Brush? MarkerBrush => _style?.Palette.Brush(_style.Palette.Text);

    public Brush? MutedBrush => _style?.Palette.Brush(_style.Palette.Muted);

    public Thickness CellMargin => new((_style?.Gap ?? 0) / 2, 0, (_style?.Gap ?? 0) / 2, 0);

    public string CountdownLabel
    {
        get
        {
            if (ResetsAt is not DateTimeOffset resetsAt)
            {
                return Loc.T(IsExhausted ? "LimitReached" : "ResetUnavailable");
            }

            TimeSpan remaining = resetsAt - DateTimeOffset.Now;
            if (remaining <= TimeSpan.Zero)
            {
                return Loc.T("ResettingNow");
            }

            string duration = UsageMath.FormatDuration(remaining);
            if (IsExhausted)
            {
                return Loc.F("BackIn", duration);
            }

            return Loc.F(View == ViewMode.Cards ? "ResetsInLong" : "ResetsInShort", duration);
        }
    }

    public Brush? CountdownBrush
    {
        get
        {
            if (_style is null)
            {
                return null;
            }

            AppearancePalette palette = _style.Palette;
            if (!IsExhausted)
            {
                return palette.Brush(palette.Muted);
            }

            RgbColor color = IsResetSoon ? palette.Status.Ok : palette.Status.Warn;
            return palette.Brush(color.EnsureContrast(_style.CardBackground, 4.5));
        }
    }

    public FontWeight CountdownWeight => IsExhausted ? FontWeights.Bold : FontWeights.Medium;

    public bool IsResetSoon => ResetsAt is not null &&
        ResetsAt.Value - DateTimeOffset.Now <= TimeSpan.FromMinutes(30);

    public bool ShowDate => View == ViewMode.Cards &&
        _style?.Options.Density != Density.Compact &&
        ResetsAt is not null;

    public string ShortResetLabel
    {
        get
        {
            if (ResetsAt is not DateTimeOffset resetsAt)
            {
                return string.Empty;
            }

            DateTime local = resetsAt.ToLocalTime().DateTime;
            DateTime today = DateTime.Today;
            string day = local.Date == today
                ? Loc.T("Today")
                : local.Date == today.AddDays(1)
                    ? Loc.T("Tomorrow")
                    : local.ToString(Loc.T("DateShortDay"), Loc.Instance.Culture).Replace(".", string.Empty);
            return Loc.F("ResetShort", day, local.ToString("h:mm tt", Loc.Instance.Culture));
        }
    }

    public string ResetDateLabel => ResetsAt is null
        ? string.Empty
        : ResetsAt.Value.ToLocalTime().ToString(Loc.T("DateLong"), Loc.Instance.Culture);

    public string Tooltip => ResetDateLabel.Length == 0
        ? Loc.F("ResetTooltipUnavailable", Label)
        : Loc.F("ResetTooltip", Label, ResetDateLabel);

    public bool ShowPace => View == ViewMode.Cards &&
        _style?.Options.ShowPace == true &&
        Pace.Kind != PaceKind.Unknown;

    public string PaceLabel => Pace.Kind switch
    {
        PaceKind.Exhausted => Loc.T("LimitReached"),
        PaceKind.RunsOut => Loc.F("PaceRunsOut", UsageMath.FormatApproximate(Pace.MinutesToEmpty)),
        _ => Loc.T("PaceEnough"),
    };

    public Brush? PaceBrush
    {
        get
        {
            if (_style is null)
            {
                return null;
            }

            StatusColors status = _style.Palette.Status;
            return _style.Palette.Brush(Pace.Kind switch
            {
                PaceKind.Exhausted => status.Critical,
                PaceKind.RunsOut => status.Warn,
                _ => status.Ok,
            });
        }
    }

    private PaceEstimate Pace => UsageMath.EstimatePace(
        UsedPercent,
        WindowMinutes,
        ResetsAt,
        DateTimeOffset.Now);

    private bool ShowMarker => _style?.Options.ShowPace == true &&
        Chart == ChartKind.Bar &&
        View != ViewMode.Mini &&
        !IsExhausted &&
        Pace.Kind != PaceKind.Unknown;

    private ViewMode View => _style?.Options.View ?? ViewMode.Cards;

    private ChartKind Chart => _style?.Options.Chart ?? ChartKind.Bar;

    private bool ShowUsed => _style?.Options.ShowUsed == true;

    private MeterSizes Sizes => MeterSizes.For(View, _style?.Options.Density ?? Density.Comfortable);

    private RgbColor BaseColor
    {
        get
        {
            if (_style is null)
            {
                return RgbColor.White;
            }

            StatusColors status = _style.Palette.Status;
            if (IsExhausted)
            {
                return status.Critical;
            }

            return _style.Options.ColorMode switch
            {
                ChartColorMode.Level => RemainingPercent < 15
                    ? status.Critical
                    : RemainingPercent < 40 ? status.Warn : status.Ok,
                ChartColorMode.Provider => _style.ProviderColor,
                _ => _style.Palette.Accent,
            };
        }
    }

    public static RateWindowViewModel FromSnapshot(RateWindow window)
    {
        return new RateWindowViewModel(
            window.Id,
            window.Label,
            window.UsedPercent,
            window.WindowMinutes,
            window.ResetsAt);
    }

    public void ApplyStyle(WindowStyleContext style)
    {
        _style = style;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    public void RefreshTimeLabel()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    private readonly record struct MeterSizes(
        double Ring,
        double RadialFont,
        double Gauge,
        double Bar,
        int Blocks,
        double Big)
    {
        public static MeterSizes For(ViewMode view, Density density)
        {
            return view switch
            {
                ViewMode.Summary => new MeterSizes(40, 11, 64, 6, 12, 22),
                ViewMode.Mini => new MeterSizes(64, 14, 96, 7, 12, 30),
                _ => density == Density.Compact
                    ? new MeterSizes(52, 12, 76, 6, 16, 26)
                    : new MeterSizes(60, 13, 84, 8, 20, 32),
            };
        }
    }
}
