using System.IO;

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
            var writeOps = lower is "write_file" or "append_file" or "delete_file" or "make_dir";
            if (writeOps && IsUnderTrash(args))
                return Fail(".trash/ 경로에는 쓰기/삭제가 불가하다 (복구 저장소 보호).");

            return lower switch
            {
                "write_file" => await WriteFileAsync(rootFull, args),
                "append_file" => await AppendFileAsync(rootFull, args),
                "read_file" => await ReadFileAsync(rootFull, args),
                "list_dir" => ListDir(rootFull, args),
                "delete_file" => DeleteFile(rootFull, args),
                "make_dir" => MakeDir(rootFull, args),
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
