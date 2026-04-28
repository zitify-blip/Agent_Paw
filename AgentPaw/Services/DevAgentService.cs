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

    // 기본 저장 루트 — Documents/AgentPaw Dev
    public static string DefaultDevProjectsRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AgentPaw Dev");

    // 저장 루트 경로를 AppData에 영속화
    private static readonly string RootPrefPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AgentPaw", "dev_projects_root.txt");

    public static string LoadSavedRoot()
    {
        try
        {
            if (File.Exists(RootPrefPath))
            {
                var saved = File.ReadAllText(RootPrefPath).Trim();
                if (!string.IsNullOrEmpty(saved)) return saved;
            }
        }
        catch { }
        return DefaultDevProjectsRoot;
    }

    public static void SaveRoot(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RootPrefPath)!);
            File.WriteAllText(RootPrefPath, path);
        }
        catch { }
    }

    /// <summary>
    /// 새 세션 시작 시 Claude에게 주입하는 컨텍스트.
    /// 개발 루트 폴더 안에 프로젝트 하위 폴더를 만들어 작업하도록 안내한다.
    /// </summary>
    public static string BuildNewProjectContext(string devProjectsRoot) => $"""
[Dev Agent — 작업 컨텍스트]
현재 작업 루트: {devProjectsRoot}

규칙:
1. 새 프로젝트를 만들 때는 반드시 이 루트 안에 적절한 이름의 하위 디렉토리를 생성한다.
   예) todo-api, image-resizer, auth-service, data-pipeline 등 kebab-case 사용.
2. 루트 폴더에 직접 파일을 만들지 않는다.
3. 프로젝트 폴더 안에 README.md, 적절한 디렉토리 구조, 완성된 동작 코드를 작성한다.
4. 기술 스택은 요청에 명시되지 않으면 작업에 가장 적합한 것을 판단해 선택한다.

---

사용자 요청:
""";

    public async Task StreamAsync(
        string prompt,
        string workingDirectory,
        bool continueSession,
        string? newSessionContext,
        IProgress<DevStreamEvent> progress,
        CancellationToken cancellationToken)
    {
        var args = "-p --dangerously-skip-permissions --output-format stream-json";
        if (continueSession) args += " --continue";

        if (!Directory.Exists(workingDirectory))
            Directory.CreateDirectory(workingDirectory);

        // 새 세션일 때만 컨텍스트 앞에 붙인다
        var finalPrompt = (!continueSession && newSessionContext != null)
            ? newSessionContext + prompt
            : prompt;

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
            await process.StandardInput.WriteAsync(finalPrompt);
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
                        sb.Append($"\n\n> **[{toolName}]**");
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
            if (input.TryGetProperty("command", out var cmd))
                return Truncate(cmd.GetString() ?? "");
            if (input.TryGetProperty("file_path", out var fp))
                return fp.GetString() ?? "";
            return Truncate(input.ToString());
        }
        catch { return ""; }
    }

    private static string Truncate(string s, int max = 120)
        => s.Length <= max ? s : s[..max] + "…";
}

// DevProjectRecord is defined in AgentPaw.ViewModels (DevAgentViewModel.cs)
