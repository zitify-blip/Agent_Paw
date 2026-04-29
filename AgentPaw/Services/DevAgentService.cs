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

    // ─────────────────────────────────────────────────────────
    // 로컬 실행

    private volatile Process? _devProcess;
    public bool IsDevProcessRunning => _devProcess is { HasExited: false };

    /// <summary>
    /// 프로젝트 폴더에서 실행할 명령을 자동 감지한다.
    /// </summary>
    public static string? DetectRunCommand(string projectPath)
    {
        var pkg = Path.Combine(projectPath, "package.json");
        if (File.Exists(pkg))
        {
            try
            {
                using var d = JsonDocument.Parse(File.ReadAllText(pkg));
                if (d.RootElement.TryGetProperty("scripts", out var scripts))
                {
                    if (scripts.TryGetProperty("dev", out _)) return "npm run dev";
                    if (scripts.TryGetProperty("start", out _)) return "npm start";
                }
            }
            catch { }
            return "npm start";
        }
        if (Directory.GetFiles(projectPath, "*.csproj", SearchOption.TopDirectoryOnly).Length > 0)
            return "dotnet run";
        if (File.Exists(Path.Combine(projectPath, "manage.py"))) return "python manage.py runserver";
        if (File.Exists(Path.Combine(projectPath, "main.py")))   return "python main.py";
        if (File.Exists(Path.Combine(projectPath, "app.py")))    return "python app.py";
        if (File.Exists(Path.Combine(projectPath, "go.mod")))    return "go run .";
        if (File.Exists(Path.Combine(projectPath, "Cargo.toml"))) return "cargo run";
        if (File.Exists(Path.Combine(projectPath, "Makefile")))  return "make";
        return null;
    }

    public async Task RunProjectAsync(string workDir, IProgress<string> output, CancellationToken ct)
    {
        var cmd = DetectRunCommand(workDir);
        if (cmd == null)
        {
            output.Report("[오류] 실행 가능한 프로젝트를 감지하지 못했습니다.\n지원: package.json · *.csproj · main.py · go.mod · Cargo.toml");
            return;
        }

        var parts = cmd.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var psi = new ProcessStartInfo(parts[0], parts.Length > 1 ? parts[1] : "")
        {
            WorkingDirectory = workDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        var proc = new Process { StartInfo = psi };
        _devProcess = proc;
        output.Report($"▶ `{cmd}`\n\n");

        try
        {
            proc.Start();
            ct.Register(() => { try { proc.Kill(true); } catch { } });

            var outTask = Task.Run(async () =>
            {
                string? line;
                while ((line = await proc.StandardOutput.ReadLineAsync(CancellationToken.None)) != null)
                    output.Report(line + "\n");
            }, CancellationToken.None);

            var errTask = Task.Run(async () =>
            {
                string? line;
                while ((line = await proc.StandardError.ReadLineAsync(CancellationToken.None)) != null)
                    output.Report("[err] " + line + "\n");
            }, CancellationToken.None);

            await Task.WhenAll(outTask, errTask);
            await proc.WaitForExitAsync(CancellationToken.None);
            output.Report($"\n▶ 종료 (exit {proc.ExitCode})");
        }
        finally
        {
            _devProcess = null;
            try { if (!proc.HasExited) proc.Kill(true); } catch { }
            proc.Dispose();
        }
    }

    public void StopRunningProject()
    {
        try { _devProcess?.Kill(true); } catch { }
    }

    // ─────────────────────────────────────────────────────────
    // GitHub 배포

    public static async Task<string?> GetGitRemoteAsync(string workDir)
    {
        if (!Directory.Exists(Path.Combine(workDir, ".git"))) return null;
        try
        {
            var psi = new ProcessStartInfo("git", "remote get-url origin")
            {
                WorkingDirectory = workDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
            };
            using var proc = Process.Start(psi)!;
            var result = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();
            return proc.ExitCode == 0 ? result.Trim() : null;
        }
        catch { return null; }
    }

    public async Task GitPushAsync(string workDir, string? remoteUrl, IProgress<string> output, CancellationToken ct)
    {
        async Task<(int code, string stdout, string stderr)> Git(string args)
        {
            var psi = new ProcessStartInfo("git", args)
            {
                WorkingDirectory = workDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            using var proc = Process.Start(psi)!;
            var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
            var stderr = await proc.StandardError.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);
            return (proc.ExitCode, stdout.Trim(), stderr.Trim());
        }

        // 1. git init
        if (!Directory.Exists(Path.Combine(workDir, ".git")))
        {
            output.Report("git init...\n");
            var (code, _, err) = await Git("init");
            if (code != 0) { output.Report($"[오류] git init: {err}"); return; }
            output.Report("완료\n\n");
        }

        // 2. remote 설정
        if (!string.IsNullOrWhiteSpace(remoteUrl))
        {
            var (chk, _, _) = await Git("remote get-url origin");
            var setCmd = chk == 0
                ? $"remote set-url origin \"{remoteUrl}\""
                : $"remote add origin \"{remoteUrl}\"";
            var (setCode, _, setErr) = await Git(setCmd);
            if (setCode != 0) { output.Report($"[오류] remote: {setErr}"); return; }
            output.Report($"remote: {remoteUrl}\n\n");
        }

        // 3. remote 확인
        var (remChk, remOut, _) = await Git("remote get-url origin");
        if (remChk != 0 || string.IsNullOrEmpty(remOut))
        {
            output.Report("[오류] GitHub remote가 설정되어 있지 않습니다.\nRemote URL을 입력 후 다시 시도하세요.");
            return;
        }
        output.Report($"remote: {remOut}\n\n");

        // 4. git add .
        output.Report("git add .\n");
        var (addCode, _, addErr) = await Git("add .");
        if (addCode != 0) { output.Report($"[오류] add: {addErr}"); return; }

        // 5. git commit
        var stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        output.Report("git commit...\n");
        var (cmtCode, cmtOut, cmtErr) = await Git($"commit -m \"update: {stamp}\"");
        if (cmtCode != 0)
        {
            var combined = (cmtOut + cmtErr).ToLowerInvariant();
            if (combined.Contains("nothing to commit") || combined.Contains("nothing added"))
                output.Report("변경사항 없음 — 이미 최신 상태입니다.\n\n");
            else
            { output.Report($"[오류] commit: {cmtErr}"); return; }
        }
        else output.Report($"{cmtOut}\n\n");

        // 6. git push
        output.Report("git push...\n");
        var (pushCode, pushOut, pushErr) = await Git("push -u origin HEAD");
        if (pushCode != 0) { output.Report($"[오류] push: {pushErr}"); return; }
        output.Report($"GitHub 배포 완료!\n{pushOut}");
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

            if (type == "user")
            {
                // tool_result 이벤트 감지 → 도구 실행 완료 신호
                if (!root.TryGetProperty("message", out var userMsg)) return null;
                if (!userMsg.TryGetProperty("content", out var userContent)) return null;
                foreach (var item in userContent.EnumerateArray())
                {
                    if (item.TryGetProperty("type", out var t) && t.GetString() == "tool_result")
                        return new DevStreamEvent("tool_done", null, null, null);
                }
                return null;
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
