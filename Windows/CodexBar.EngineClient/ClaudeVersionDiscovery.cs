using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CodexBar.EngineClient;

/// <summary>Finds the installed native Claude Code version without invoking a shell or reading credentials.</summary>
public static class ClaudeVersionDiscovery
{
    public static string? ParseVersion(string? output)
    {
        if (string.IsNullOrWhiteSpace(output)) return null;
        Match match = Regex.Match(output, @"^\s*(\d+\.\d+\.\d+)(?:\s|$)", RegexOptions.CultureInvariant);
        return match.Success ? match.Groups[1].Value : null;
    }

    public static IEnumerable<string> CandidatePaths(string? path, string? userProfile)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string entry in (path ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string directory = entry.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(directory)) continue;
            string candidate = Path.Combine(directory, "claude.exe");
            if (seen.Add(candidate)) yield return candidate;
        }
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            string native = Path.Combine(userProfile, ".local", "bin", "claude.exe");
            if (seen.Add(native)) yield return native;
        }
    }

    public static async Task<string?> ResolveAsync(CancellationToken cancellationToken = default)
    {
        string? configured = ParseVersion(Environment.GetEnvironmentVariable("CLAUDE_CODE_VERSION"));
        if (configured is not null) return configured;

        foreach (string executable in CandidatePaths(
                     Environment.GetEnvironmentVariable("PATH"),
                     Environment.GetEnvironmentVariable("USERPROFILE")))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(executable)) continue;
            string? version = await ReadVersionAsync(executable, cancellationToken).ConfigureAwait(false);
            if (version is not null) return version;
        }
        return null;
    }

    private static async Task<string?> ReadVersionAsync(string executable, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                ArgumentList = { "--version" },
            },
        };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            if (!process.Start()) return null;
            Task<string> output = ReadVersionOutputAsync(process.StandardOutput, timeout.Token);
            Task errors = process.StandardError.BaseStream.CopyToAsync(Stream.Null, timeout.Token);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            string text = await output.ConfigureAwait(false);
            await errors.ConfigureAwait(false);
            return process.ExitCode == 0 ? ParseVersion(text) : null;
        }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }
        catch (System.ComponentModel.Win32Exception) { return null; }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private static async Task<string> ReadVersionOutputAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var buffer = new char[256];
        int length = 0;
        while (length < buffer.Length)
        {
            int count = await reader.ReadAsync(buffer.AsMemory(length), cancellationToken).ConfigureAwait(false);
            if (count == 0) break;
            length += count;
            if (Array.IndexOf(buffer, '\n', 0, length) >= 0) break;
        }
        return new string(buffer, 0, length);
    }
}
