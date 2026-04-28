using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace AgentPaw.Services;

public record DevStreamEvent(string Type, string? Text, string? ToolName, string? ToolInput);

public class DevAgentService
{
    private volatile Process? _activeProcess;

    public bool IsRunning => _activeProcess != null;

    public async Task StreamAsync(
        string prompt,
        string workingDirectory,
        bool continueSession,
        IProgress<DevStreamEvent> progress,
        CancellationToken cancellationToken)
    {
        var args = "-p --dangerously-skip-permissions --output-format stream-json";
        if (continueSession) args += " --continue";

        if (!Directory.Exists(workingDirectory))
            Directory.CreateDirectory(workingDirectory);

        var psi = new ProcessStartInfo
        {
            FileName = "claude",
            Arguments = args,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardInputEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        var process = new Process { StartInfo = psi };
        _activeProcess = process;

        try
        {
            process.Start();
            await process.StandardInput.WriteAsync(prompt);
            process.StandardInput.Close();

            string? line;
            while ((line = await process.StandardOutput.ReadLineAsync(cancellationToken)) != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var evt = ParseEvent(line);
                if (evt != null) progress.Report(evt);
            }

            try { await process.WaitForExitAsync(cancellationToken); } catch { }
        }
        finally
        {
            _activeProcess = null;
            try { if (!process.HasExited) process.Kill(true); } catch { }
            process.Dispose();
        }
    }

    public void Cancel()
    {
        try { _activeProcess?.Kill(true); } catch { }
    }

    private static DevStreamEvent? ParseEvent(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeEl)) return null;
            var type = typeEl.GetString();

            if (type == "assistant")
            {
                if (!root.TryGetProperty("message", out var msg)) return null;
                if (!msg.TryGetProperty("content", out var content)) return null;

                var sb = new StringBuilder();
                foreach (var item in content.EnumerateArray())
                {
                    if (!item.TryGetProperty("type", out var itemTypeEl)) continue;
                    var itemType = itemTypeEl.GetString();

                    if (itemType == "text" && item.TryGetProperty("text", out var textEl))
                    {
                        sb.Append(textEl.GetString());
                    }
                    else if (itemType == "tool_use")
                    {
                        var toolName = item.TryGetProperty("name", out var n) ? n.GetString() ?? "tool" : "tool";
                        var toolInput = item.TryGetProperty("input", out var inp) ? SummarizeInput(inp) : "";
                        sb.Append($"\n\n> **[실행: {toolName}]**");
                        if (!string.IsNullOrEmpty(toolInput))
                            sb.Append($"\n> `{toolInput}`");
                    }
                }

                var text = sb.ToString();
                return text.Length > 0 ? new DevStreamEvent("assistant", text, null, null) : null;
            }

            if (type == "result")
                return new DevStreamEvent("result", null, null, null);

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string SummarizeInput(JsonElement input)
    {
        try
        {
            // Bash command
            if (input.TryGetProperty("command", out var cmd))
                return Truncate(cmd.GetString() ?? "");
            // Write file
            if (input.TryGetProperty("file_path", out var fp))
                return fp.GetString() ?? "";
            // Generic
            var raw = input.ToString();
            return Truncate(raw);
        }
        catch { return ""; }
    }

    private static string Truncate(string s, int max = 120)
        => s.Length <= max ? s : s[..max] + "…";
}
