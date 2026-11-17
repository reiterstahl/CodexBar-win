namespace CodexBar.Windows.Tray;

/// <summary>Independent date-display choices for each account and quota window.</summary>
public sealed class ResetDatePreferences
{
    public Dictionary<string, HashSet<string>> ExactWindows { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public bool IsExact(string profile, string window) =>
        ExactWindows.TryGetValue(profile, out HashSet<string>? windows) && windows.Contains(window);

    public void SetExact(string profile, string window, bool exact)
    {
        if (!ExactWindows.TryGetValue(profile, out HashSet<string>? windows))
        {
            if (!exact) return;
            windows = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ExactWindows[profile] = windows;
        }
        if (exact) windows.Add(window);
        else windows.Remove(window);
        if (windows.Count == 0) ExactWindows.Remove(profile);
    }

    public void Normalize()
    {
        ExactWindows = (ExactWindows ?? [])
            .Where(pair => pair.Value is not null)
            .ToDictionary(pair => pair.Key,
                pair => new HashSet<string>(pair.Value, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);
    }
}
