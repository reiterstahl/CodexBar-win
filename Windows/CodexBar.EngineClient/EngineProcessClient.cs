using System.Diagnostics;
using System.Text;

namespace CodexBar.EngineClient;

public sealed class EngineProcessClient
{
    private const int MaximumOutputCharacters = 1_000_000;
    private readonly string _enginePath;
    private readonly TimeSpan _timeout;

    public EngineProcessClient(string? enginePath = null, TimeSpan? timeout = null)
    {
        _enginePath = enginePath ?? EngineLocator.Resolve();
        _timeout = timeout ?? TimeSpan.FromSeconds(45);
    }

    public async Task<EngineSnapshot> FetchAsync(CancellationToken cancellationToken = default)
    {
        using var process = new Process
        {
            StartInfo = CreateStartInfo(),
            EnableRaisingEvents = true,
        };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);

        try
        {
            if (!process.Start())
            {
                throw new EngineClientException("The usage engine could not be started.");
            }

            Task<string> standardOutput = ReadBoundedAsync(
                process.StandardOutput,
                MaximumOutputCharacters,
                timeout.Token);
            Task standardError = process.StandardError.BaseStream.CopyToAsync(Stream.Null, timeout.Token);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);

            string output = await standardOutput.ConfigureAwait(false);
            await standardError.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                throw new EngineClientException($"The usage engine exited with code {process.ExitCode}.");
            }

            return EngineSnapshotParser.Parse(output);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryTerminate(process);
            throw new EngineClientException($"The usage engine timed out after {_timeout.TotalSeconds:0} seconds.");
        }
        catch (OperationCanceledException)
        {
            TryTerminate(process);
            throw;
        }
        catch (EngineClientException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new EngineClientException("The usage engine failed to run.", exception);
        }
    }

    private ProcessStartInfo CreateStartInfo()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _enginePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(_enginePath) ?? AppContext.BaseDirectory,
        };
        startInfo.ArgumentList.Add("--pretty");
        return startInfo;
    }

    private static async Task<string> ReadBoundedAsync(
        StreamReader reader,
        int maximumCharacters,
        CancellationToken cancellationToken)
    {
        var output = new StringBuilder(capacity: Math.Min(maximumCharacters, 16_384));
        var buffer = new char[8_192];
        bool oversized = false;

        while (true)
        {
            int count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                break;
            }

            int remaining = maximumCharacters - output.Length;
            if (remaining > 0)
            {
                output.Append(buffer, 0, Math.Min(remaining, count));
            }

            oversized |= count > remaining;
        }

        if (oversized)
        {
            throw new EngineClientException("The usage engine returned an oversized response.");
        }

        return output.ToString();
    }

    private static void TryTerminate(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }
}
