using Microsoft.Win32;

namespace CodexBar.Windows.Tray;

/// <summary>
/// Per-user "start with Windows" entry. Velopack keeps the executable under a stable
/// <c>current</c> directory across updates, so the stored path survives upgrades.
/// </summary>
public static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CodexBar";

    public static bool IsEnabled
    {
        get
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
                return key?.GetValue(ValueName) is string command &&
                    command.Equals(Command, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    private static string Command => $"\"{Environment.ProcessPath}\"";

    public static void Enable()
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        key.SetValue(ValueName, Command, RegistryValueKind.String);
    }

    public static void Disable()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch (Exception)
        {
            // Uninstall hooks must never fail because of a missing or locked Run key.
        }
    }
}
