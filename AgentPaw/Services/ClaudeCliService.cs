using System.Diagnostics;
using AgentPaw.Services;

namespace AgentPaw.Services;

public class ClaudeCliService
{
    private readonly ApiKeyService _apiKeyService;
    private const string SettingKey = "CLAUDE_CLI_ENABLED";
    private const string CliCommand = "claude";
    private const int TimeoutMs = 180_000;

    private readonly List<Process> _activeProcesses = [];
    private readonly object _lock = new();

    public ClaudeCliService(ApiKeyService apiKeyService)
    {
        _apiKeyService = apiKeyService;
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "claude",
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            process.Start();
            using var cts = new CancellationTokenSource(5000);
            await process.WaitForExitAsync(cts.Token);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> IsEnabledAsync()
    {
        var val = await _apiKeyService.GetApiKeyAsync(SettingKey);
        return val == "true";
    }

    public async Task SetEnabledAsync(bool enabled)
    {
        await _apiKeyService.SetApiKeyAsync(SettingKey, enabled ? "true" : "false");
    }

    public async Task<string> CallAsync(string systemPrompt, string userPrompt)
    {
        var fullPrompt = !string.IsNullOrEmpty(systemPrompt)
            ? $"{systemPrompt}\n\n===\n{userPrompt}"
            : userPrompt;

        var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "claude",
            Arguments = "-p --output-format text",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardInputEncoding = System.Text.Encoding.UTF8,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        lock (_lock) _activeProcesses.Add(process);

        try
        {
            process.Start();

            // stdin으로 프롬프트 전달 (Windows cmd.exe argument splitting 우회)
            await process.StandardInput.WriteAsync(fullPrompt);
            process.StandardInput.Close();

            using var cts = new CancellationTokenSource(TimeoutMs);
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(true); } catch { }
                throw new InvalidOperationException("CLAUDE_CLI_FAILED: timeout (180s)");
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
                throw new InvalidOperationException($"CLAUDE_CLI_FAILED: exit {process.ExitCode} {stderr.Trim()}");

            return stdout.Trim();
        }
        finally
        {
            lock (_lock) _activeProcesses.Remove(process);
            process.Dispose();
        }
    }

    public void KillAll()
    {
        lock (_lock)
        {
            foreach (var p in _activeProcesses)
            {
                try { if (!p.HasExited) p.Kill(true); } catch { }
            }
            _activeProcesses.Clear();
        }
    }
}
