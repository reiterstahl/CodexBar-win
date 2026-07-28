using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using CodexBar.EngineClient;

namespace CodexBar.Windows.Tray;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private bool _isRefreshing;
    private string _status = "Starting…";

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ProviderCardViewModel> Providers { get; } = [];

    public string Status
    {
        get => _status;
        private set => SetField(ref _status, value);
    }

    public bool CanRefresh => !_isRefreshing;

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
            Providers.Add(result.Snapshot is not null
                ? ProviderCardViewModel.FromSnapshot(result.Profile, result.Snapshot)
                : ProviderCardViewModel.FromFailure(result.Profile, result.Failure));
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

    private void SetField(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record ProviderCardViewModel(
    string DisplayName,
    string Identity,
    string ErrorMessage,
    IReadOnlyList<RateWindowViewModel> Windows)
{
    public static ProviderCardViewModel FromSnapshot(
        ProviderProfile profile,
        ProviderSnapshot snapshot)
    {
        string identity = snapshot.Identity?.AccountEmail
            ?? snapshot.Identity?.Plan
            ?? string.Empty;
        return new ProviderCardViewModel(
            profile.Label,
            identity,
            string.Empty,
            snapshot.Windows.Select(RateWindowViewModel.FromSnapshot).ToArray());
    }

    public static ProviderCardViewModel FromFailure(
        ProviderProfile profile,
        ProviderFailure? failure)
    {
        string message = failure?.Message ?? "Provider data is unavailable.";
        return new ProviderCardViewModel(profile.Label, string.Empty, message, []);
    }
}

public sealed record RateWindowViewModel(
    string Label,
    double UsedPercent,
    string RemainingLabel,
    string ResetLabel)
{
    public static RateWindowViewModel FromSnapshot(RateWindow window)
    {
        string remaining = string.Create(
            CultureInfo.CurrentCulture,
            $"{window.RemainingPercent:0.#}% left");
        string reset = window.ResetsAt is null
            ? string.Empty
            : $"Resets {window.ResetsAt.Value.ToLocalTime():g}";
        return new RateWindowViewModel(window.Label, window.UsedPercent, remaining, reset);
    }
}
