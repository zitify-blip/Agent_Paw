using System.Diagnostics;
using System.IO;
using System.Text;

namespace AgentPaw.Services;

public class ToolExecutorService
{
    private const long MaxFileBytes = 5 * 1024 * 1024;
    private const int MaxResultChars = 32_000;
    private const int MaxListEntries = 500;

    public async Task<ToolExecutionResult> ExecuteAsync(
        string workspaceRoot, string toolName, Dictionary<string, object?> args)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
            return Fail("작업 폴더(workspace root)가 설정되어 있지 않다.");

        try
        {
            Directory.CreateDirectory(workspaceRoot);
            var rootFull = NormalizeDir(Path.GetFullPath(workspaceRoot));

            var lower = toolName.ToLowerInvariant();
            var writeOps = lower is "write_file" or "append_file" or "edit_file" or "delete_file" or "make_dir";
            if (writeOps && IsUnderTrash(args))
                return Fail(".trash/ 경로에는 쓰기/삭제가 불가하다 (복구 저장소 보호).");

            return lower switch
            {
                "write_file"   => await WriteFileAsync(rootFull, args),
                "append_file"  => await AppendFileAsync(rootFull, args),
                "edit_file"    => await EditFileAsync(rootFull, args),
                "read_file"    => await ReadFileAsync(rootFull, args),
                "list_dir"     => ListDir(rootFull, args),
                "search_files" => SearchFiles(rootFull, args),
                "delete_file"  => DeleteFile(rootFull, args),
                "make_dir"     => MakeDir(rootFull, args),
                "run_command"  => await RunCommandAsync(rootFull, args),
                _ => Fail($"알 수 없는 도구: {toolName}")
            };
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }

    private static async Task<ToolExecutionResult> WriteFileAsync(string root, Dictionary<string, object?> args)
    {
        var path = RequireString(args, "path");
        var content = GetString(args, "content") ?? string.Empty;
        var full = ResolveInside(root, path);

        var bytes = System.Text.Encoding.UTF8.GetByteCount(content);
        if (bytes > MaxFileBytes)
            return Fail($"내용이 너무 크다 ({bytes} bytes, 최대 {MaxFileBytes})");

        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(full, content, new System.Text.UTF8Encoding(false));
        return Ok($"ok — {RelOf(root, full)} 에 {bytes} bytes 기록");
    }

    private static async Task<ToolExecutionResult> AppendFileAsync(string root, Dictionary<string, object?> args)
    {
        var path = RequireString(args, "path");
        var content = GetString(args, "content") ?? string.Empty;
        var full = ResolveInside(root, path);

        var existing = File.Exists(full) ? new FileInfo(full).Length : 0;
        var bytes = System.Text.Encoding.UTF8.GetByteCount(content);
        if (existing + bytes > MaxFileBytes)
            return Fail($"append 후 파일 크기 초과 ({existing + bytes} > {MaxFileBytes})");

        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        await File.AppendAllTextAsync(full, content, new System.Text.UTF8Encoding(false));
        return Ok($"ok — {RelOf(root, full)} 에 {bytes} bytes 추가");
    }

    private static async Task<ToolExecutionResult> ReadFileAsync(string root, Dictionary<string, object?> args)
    {
        var path = RequireString(args, "path");
        var full = ResolveInside(root, path);

        if (!File.Exists(full))
            return Fail($"파일 없음: {RelOf(root, full)}");

        var info = new FileInfo(full);
        if (info.Length > MaxFileBytes)
            return Fail($"파일이 너무 크다 ({info.Length} bytes)");

        // 바이너리 감지 (UTF8로 디코드 실패 또는 NUL 바이트 포함)
        var bytes = await File.ReadAllBytesAsync(full);
        if (Array.IndexOf(bytes, (byte)0) >= 0)
            return Fail($"바이너리 파일로 보인다 ({RelOf(root, full)}). 텍스트 파일만 읽을 수 있다.");

        string content;
        try
        {
            content = new System.Text.UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (System.Text.DecoderFallbackException)
        {
            return Fail($"UTF-8로 디코드 불가 ({RelOf(root, full)}). 텍스트 파일만 읽을 수 있다.");
        }

        var truncated = content.Length > MaxResultChars;
        var body = truncated ? content[..MaxResultChars] + "\n...[truncated]" : content;

        return Ok($"[{RelOf(root, full)}, {info.Length} bytes{(truncated ? ", truncated" : "")}]\n{body}");
    }

    private static ToolExecutionResult ListDir(string root, Dictionary<string, object?> args)
    {
        var rel = GetString(args, "path") ?? ".";
        var full = ResolveInside(root, rel);

        if (!Directory.Exists(full))
            return Fail($"폴더 없음: {RelOf(root, full)}");

        var entries = new List<string>();
        foreach (var d in Directory.EnumerateDirectories(full))
        {
            entries.Add($"[DIR ] {Path.GetFileName(d)}/");
            if (entries.Count >= MaxListEntries) break;
        }
        foreach (var f in Directory.EnumerateFiles(full))
        {
            var fi = new FileInfo(f);
            entries.Add($"[FILE] {fi.Name} ({fi.Length} bytes)");
            if (entries.Count >= MaxListEntries) break;
        }

        var body = entries.Count == 0 ? "(빈 폴더)" : string.Join("\n", entries);
        return Ok($"[{RelOf(root, full) ?? "/"}]\n{body}");
    }

    private static ToolExecutionResult DeleteFile(string root, Dictionary<string, object?> args)
    {
        var path = RequireString(args, "path");
        var full = ResolveInside(root, path);

        if (!File.Exists(full))
            return Fail($"파일 없음: {RelOf(root, full)}");

        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        var trashDir = Path.Combine(root, ".trash", stamp);
        Directory.CreateDirectory(trashDir);
        var dest = Path.Combine(trashDir, Path.GetFileName(full));
        File.Move(full, dest, overwrite: true);

        return Ok($"ok — {RelOf(root, full)} 를 .trash/{stamp}/ 로 이동 (복구 가능)");
    }

    private static ToolExecutionResult MakeDir(string root, Dictionary<string, object?> args)
    {
        var path = RequireString(args, "path");
        var full = ResolveInside(root, path);
        Directory.CreateDirectory(full);
        return Ok($"ok — {RelOf(root, full)} 생성");
    }

    // edit_file: old_text를 new_text로 정확히 1곳 교체.
    // 0곳이면 미발견 오류, 2곳 이상이면 모호 오류를 반환하여 의도치 않은 다중 치환을 방지한다.
    private static async Task<ToolExecutionResult> EditFileAsync(string root, Dictionary<string, object?> args)
    {
        var path    = RequireString(args, "path");
        var oldText = RequireString(args, "old_text");
        var newText = GetString(args, "new_text") ?? string.Empty;
        var full    = ResolveInside(root, path);

        if (!File.Exists(full))
            return Fail($"파일 없음: {RelOf(root, full)}");

        var info = new FileInfo(full);
        if (info.Length > MaxFileBytes)
            return Fail($"파일이 너무 크다 ({info.Length} bytes)");

        var content = await File.ReadAllTextAsync(full, new System.Text.UTF8Encoding(false));
        var count   = CountOccurrences(content, oldText);

        if (count == 0)
            return Fail("old_text를 파일에서 찾지 못했다. 공백·줄바꿈을 포함하여 정확히 일치해야 한다.");

        if (count > 1)
            return Fail($"old_text가 파일 내 {count}곳에서 발견됐다. 더 많은 주변 컨텍스트를 포함하여 유일하게 특정한다.");

        var newContent = content.Replace(oldText, newText, StringComparison.Ordinal);
        await File.WriteAllTextAsync(full, newContent, new System.Text.UTF8Encoding(false));
        return Ok($"ok — {RelOf(root, full)} 편집 완료");
    }

    // search_files: 텍스트 패턴(대소문자 무시)으로 파일 검색.
    // include 인자로 파일 패턴 필터 가능 (예: "*.cs"). 최대 300줄 반환.
    private static ToolExecutionResult SearchFiles(string root, Dictionary<string, object?> args)
    {
        var pattern     = RequireString(args, "pattern");
        var searchRel   = GetString(args, "path") ?? ".";
        var fileGlob    = GetString(args, "include") ?? "*";
        var full        = ResolveInside(root, searchRel);

        if (!Directory.Exists(full) && !File.Exists(full))
            return Fail($"경로 없음: {searchRel}");

        var files = File.Exists(full)
            ? (IEnumerable<string>)[full]
            : Directory.EnumerateFiles(full, fileGlob, SearchOption.AllDirectories)
                       .Where(f => !f.Replace('\\', '/').Contains("/.git/")
                                && !f.Replace('\\', '/').Contains("/.trash/")
                                && !f.Replace('\\', '/').Contains("/bin/")
                                && !f.Replace('\\', '/').Contains("/obj/")
                                && !f.Replace('\\', '/').Contains("/node_modules/"));

        var results = new List<string>();
        const int MaxLines = 300;

        foreach (var file in files)
        {
            if (results.Count >= MaxLines) { results.Add("...[이하 결과 생략]"); break; }
            try
            {
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    if (!lines[i].Contains(pattern, StringComparison.OrdinalIgnoreCase)) continue;
                    results.Add($"{RelOf(root, file)}:{i + 1}: {lines[i].Trim()}");
                    if (results.Count >= MaxLines) { results.Add("...[이하 결과 생략]"); break; }
                }
            }
            catch { /* 바이너리 등 읽기 불가 파일은 건너뜀 */ }
        }

        return results.Count == 0
            ? Ok($"'{pattern}' 패턴과 일치하는 결과 없음")
            : Ok(string.Join("\n", results));
    }

    // run_command: workspaceRoot를 CWD로 하여 셸 명령 실행. 타임아웃 60초.
    private static async Task<ToolExecutionResult> RunCommandAsync(string root, Dictionary<string, object?> args)
    {
        var command = RequireString(args, "command");

        // 명백히 파괴적인 패턴만 차단한다
        var norm = command.ToLowerInvariant();
        string[] blocked = ["rm -rf /", "format c:", "del /f /s /q c:\\", ":(){:|:&};:"];
        foreach (var b in blocked)
            if (norm.Contains(b)) return Fail("위험한 패턴이 포함된 명령어다. 실행을 거부했다.");

        var psi = new ProcessStartInfo
        {
            FileName               = "cmd.exe",
            Arguments              = "/c " + command,
            WorkingDirectory       = root,
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding  = Encoding.UTF8
        };

        Process proc;
        try   { proc = Process.Start(psi) ?? throw new InvalidOperationException("프로세스 시작 실패"); }
        catch (Exception ex) { return Fail($"프로세스 실행 오류: {ex.Message}"); }

        using (proc)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var outTask = proc.StandardOutput.ReadToEndAsync(cts.Token);
            var errTask = proc.StandardError.ReadToEndAsync(cts.Token);

            try   { await proc.WaitForExitAsync(cts.Token); }
            catch (OperationCanceledException)
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                return Fail("명령 실행 시간 초과 (60초). 프로세스를 강제 종료했다.");
            }

            var stdout = (await outTask).TrimEnd();
            var stderr = (await errTask).TrimEnd();

            var sb = new StringBuilder();
            sb.AppendLine($"[exit code: {proc.ExitCode}]");
            if (!string.IsNullOrEmpty(stdout))
                sb.AppendLine(stdout.Length > 8000 ? stdout[..8000] + "\n...[truncated]" : stdout);
            if (!string.IsNullOrEmpty(stderr))
            {
                sb.AppendLine("--- stderr ---");
                sb.AppendLine(stderr.Length > 4000 ? stderr[..4000] + "\n...[truncated]" : stderr);
            }

            var output = sb.ToString().TrimEnd();
            return proc.ExitCode == 0
                ? Ok(output)
                : new ToolExecutionResult { Success = false, Message = output };
        }
    }

    private static int CountOccurrences(string text, string pattern)
    {
        var count = 0;
        var idx   = 0;
        while ((idx = text.IndexOf(pattern, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += pattern.Length;
        }
        return count;
    }

    private static string ResolveInside(string rootFull, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative))
            throw new ArgumentException("path가 비어 있다.");

        if (Path.IsPathRooted(relative))
            throw new UnauthorizedAccessException("절대 경로는 허용되지 않는다. 작업 폴더 기준 상대 경로를 사용한다.");

        var combined = Path.GetFullPath(Path.Combine(rootFull, relative));
        var rootNorm = NormalizeDir(rootFull);
        var combinedNorm = combined.EndsWith(Path.DirectorySeparatorChar) ? combined : combined + Path.DirectorySeparatorChar;

        if (!combinedNorm.StartsWith(rootNorm, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(combined, rootFull.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException($"경로가 작업 폴더를 벗어났다: {relative}");

        return combined;
    }

    private static string NormalizeDir(string p)
        => p.EndsWith(Path.DirectorySeparatorChar) ? p : p + Path.DirectorySeparatorChar;

    private static bool IsUnderTrash(Dictionary<string, object?> args)
    {
        var p = GetString(args, "path");
        if (string.IsNullOrWhiteSpace(p)) return false;
        var norm = p.Replace('\\', '/').TrimStart('/');
        return norm.Equals(".trash", StringComparison.OrdinalIgnoreCase)
            || norm.StartsWith(".trash/", StringComparison.OrdinalIgnoreCase);
    }

    private static string RelOf(string root, string full)
    {
        var rel = Path.GetRelativePath(root, full);
        return string.IsNullOrEmpty(rel) || rel == "." ? "" : rel.Replace('\\', '/');
    }

    private static string RequireString(Dictionary<string, object?> args, string key)
    {
        var v = GetString(args, key);
        if (string.IsNullOrWhiteSpace(v))
            throw new ArgumentException($"필수 인자 누락: {key}");
        return v;
    }

    private static string? GetString(Dictionary<string, object?> args, string key)
    {
        if (!args.TryGetValue(key, out var v) || v == null) return null;
        return v is System.Text.Json.JsonElement je
            ? (je.ValueKind == System.Text.Json.JsonValueKind.String ? je.GetString() : je.GetRawText())
            : v.ToString();
    }

    private static ToolExecutionResult Ok(string message) => new() { Success = true, Message = message };
    private static ToolExecutionResult Fail(string message) => new() { Success = false, Message = message };
}

public class ToolExecutionResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
