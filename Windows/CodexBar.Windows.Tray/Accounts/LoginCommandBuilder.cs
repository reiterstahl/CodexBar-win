namespace CodexBar.Windows.Tray;

/// <summary>
/// PowerShell blocks that sign a provider CLI in to one isolated profile directory. They contain
/// only paths and CLI commands, never credentials. Framework-independent so tests can check them.
/// </summary>
public static class LoginCommandBuilder
{
    public static string Build(string provider, string directory, bool createDirectory)
    {
        string path = PowerShellLiteral(directory);
        var lines = new List<string>();
        if (createDirectory)
        {
            lines.Add($"New-Item -ItemType Directory -Force -Path {path} | Out-Null");
        }

        if (provider == "claude")
        {
            lines.Add($"$env:CLAUDE_CONFIG_DIR = {path}");
            lines.Add($"Write-Host {PowerShellLiteral(Loc.T("ClaudeLoginHint"))}");
            lines.Add("claude auth login");
            lines.Add("claude auth status");
            lines.Add("Remove-Item Env:CLAUDE_CONFIG_DIR");
        }
        else
        {
            lines.Add($"$env:CODEX_HOME = {path}");
            lines.Add($"Write-Host {PowerShellLiteral(Loc.T("CodexLoginHint"))}");
            lines.Add("codex login --device-auth");
            lines.Add("codex login status");
            lines.Add("Remove-Item Env:CODEX_HOME");
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static string PowerShellLiteral(string value)
    {
        return $"'{value.Replace("'", "''")}'";
    }

    /// <summary>Shows a path relative to %USERPROFILE% when possible, for friendlier messages.</summary>
    public static string DisplayPath(string directory, string userProfile)
    {
        return !string.IsNullOrEmpty(userProfile) &&
            directory.StartsWith(userProfile.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)
            ? "%USERPROFILE%" + directory[userProfile.TrimEnd('\\', '/').Length..]
            : directory;
    }
}
