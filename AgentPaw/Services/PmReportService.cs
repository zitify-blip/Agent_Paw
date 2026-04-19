using System.IO;
using System.Text;
using AgentPaw.Orchestrator;

namespace AgentPaw.Services;

/// <summary>
/// §9.4 산출물 취합·저장·보고 PB 구현.
/// PM이 pm_report 블록으로 종료 보고를 내리면 역할별 산출물을
/// outputs/&lt;yyyyMMdd-HHmmss&gt;-&lt;run_id&gt;/ 디렉토리에 서브폴더(pm/·plan/·design/·dev/·qa/·da/·dba/·aa/·sa/)로 수집한다.
/// </summary>
public class PmReportService
{
    private readonly GitService _git;

    public PmReportService(GitService git)
    {
        _git = git;
    }

    private static readonly Dictionary<string, string> RoleToFolder = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PM"] = "pm",
        ["기획"] = "plan",
        ["PLANNER"] = "plan",
        ["PLAN"] = "plan",
        ["디자인"] = "design",
        ["DESIGN"] = "design",
        ["DESIGNER"] = "design",
        ["개발"] = "dev",
        ["DEV"] = "dev",
        ["DEVELOPER"] = "dev",
        ["SOFTWARE"] = "dev",
        ["QA"] = "qa",
        ["DA"] = "da",
        ["DBA"] = "dba",
        ["AA"] = "aa",
        ["SA"] = "sa",
        ["NOVEL"] = "novel",
        ["VIDEO"] = "video"
    };

    public PmReportResult Aggregate(PmReportContext ctx)
    {
        var stamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        var outputsRoot = Path.Combine(ctx.WorkspaceRoot, "outputs", $"{stamp}-{ctx.RunId}");
        Directory.CreateDirectory(outputsRoot);

        // 역할별 산출물 복사 (write_file로 생성된 파일)
        foreach (var turn in ctx.Turns)
        {
            var roleFolder = ResolveRoleFolder(turn.PersonaName, turn.PersonaLabel);
            var roleDir = Path.Combine(outputsRoot, roleFolder);
            Directory.CreateDirectory(roleDir);

            foreach (var relPath in turn.WrittenFiles.Distinct())
            {
                try
                {
                    var srcAbs = Path.Combine(ctx.WorkspaceRoot, relPath);
                    if (!File.Exists(srcAbs)) continue;

                    var destAbs = Path.Combine(roleDir, relPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destAbs)!);
                    File.Copy(srcAbs, destAbs, overwrite: true);
                }
                catch { /* 개별 파일 실패는 무시하고 나머지 진행 */ }
            }

            // 역할 응답 본문을 role.md로도 저장 (handoff·도구 호출 추적용)
            if (!string.IsNullOrWhiteSpace(turn.Content))
            {
                var noteFile = Path.Combine(roleDir, $"{roleFolder}_response.md");
                var noteSb = new StringBuilder();
                noteSb.AppendLine($"# {turn.PersonaLabel} ({turn.PersonaName})");
                noteSb.AppendLine();
                noteSb.AppendLine($"- 턴 순서: {turn.TurnIndex}");
                noteSb.AppendLine($"- 사용 모델: {turn.ModelUsed}");
                noteSb.AppendLine();
                noteSb.AppendLine("## 응답 본문");
                noteSb.AppendLine();
                noteSb.AppendLine(turn.Content);
                File.WriteAllText(noteFile, noteSb.ToString(), Encoding.UTF8);
            }
        }

        // PM 종합 보고서 — pm/REPORT.md
        var pmDir = Path.Combine(outputsRoot, "pm");
        Directory.CreateDirectory(pmDir);
        var reportPath = Path.Combine(pmDir, "REPORT.md");
        var sb = new StringBuilder();
        sb.AppendLine("# 프로젝트 종합 보고서");
        sb.AppendLine();
        sb.AppendLine($"- Run ID: {ctx.RunId}");
        sb.AppendLine($"- 생성 시각: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine($"- 참여 역할: {string.Join(", ", ctx.Turns.Select(t => t.PersonaName).Distinct())}");
        sb.AppendLine();
        sb.AppendLine("## 사용자 원본 요청");
        sb.AppendLine();
        sb.AppendLine(ctx.OriginalUserMessage);
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(ctx.ReportSummary))
        {
            sb.AppendLine("## 요약");
            sb.AppendLine();
            sb.AppendLine(ctx.ReportSummary);
            sb.AppendLine();
        }
        if (!string.IsNullOrWhiteSpace(ctx.ReportBody))
        {
            sb.AppendLine("## 상세 보고");
            sb.AppendLine();
            sb.AppendLine(ctx.ReportBody);
            sb.AppendLine();
        }
        sb.AppendLine("## 역할별 산출물");
        sb.AppendLine();
        foreach (var turn in ctx.Turns)
        {
            var roleFolder = ResolveRoleFolder(turn.PersonaName, turn.PersonaLabel);
            sb.AppendLine($"### {turn.PersonaLabel} (`{roleFolder}/`)");
            sb.AppendLine();
            if (turn.WrittenFiles.Count == 0)
            {
                sb.AppendLine("- (생성된 파일 없음)");
            }
            else
            {
                foreach (var f in turn.WrittenFiles.Distinct())
                    sb.AppendLine($"- `{f}`");
            }
            sb.AppendLine();
        }
        File.WriteAllText(reportPath, sb.ToString(), Encoding.UTF8);

        // Git 커밋
        string commitSha = string.Empty;
        try
        {
            _git.InitRepo(ctx.WorkspaceRoot);
            commitSha = _git.CommitAll(ctx.WorkspaceRoot, $"PM 보고: {ctx.RunId}");
        }
        catch
        {
            // 변경 없음·LibGit2 오류는 무시
        }

        return new PmReportResult
        {
            OutputsFolder = outputsRoot,
            ReportPath = reportPath,
            CommitSha = commitSha
        };
    }

    private static string ResolveRoleFolder(string name, string label)
    {
        if (RoleToFolder.TryGetValue(name, out var folder)) return folder;
        if (RoleToFolder.TryGetValue(label, out folder)) return folder;
        // 안전한 소문자 폴더명으로 폴백
        var safe = new string((name ?? "role").Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray())
            .ToLowerInvariant();
        return string.IsNullOrWhiteSpace(safe) ? "role" : safe;
    }
}

public class PmReportContext
{
    public string WorkspaceRoot { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public string OriginalUserMessage { get; set; } = string.Empty;
    public string ReportSummary { get; set; } = string.Empty;
    public string ReportBody { get; set; } = string.Empty;
    public List<TurnOutputRecord> Turns { get; set; } = [];
}

public class TurnOutputRecord
{
    public int TurnIndex { get; set; }
    public string PersonaName { get; set; } = string.Empty;
    public string PersonaLabel { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ModelUsed { get; set; } = string.Empty;
    public List<string> WrittenFiles { get; set; } = [];
}

public class PmReportResult
{
    public string OutputsFolder { get; set; } = string.Empty;
    public string ReportPath { get; set; } = string.Empty;
    public string CommitSha { get; set; } = string.Empty;
}
