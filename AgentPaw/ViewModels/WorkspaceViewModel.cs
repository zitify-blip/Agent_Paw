using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Wpf.Ui.Controls;
using AgentPaw.Data;
using AgentPaw.Models;
using AgentPaw.Orchestrator;
using AgentPaw.Services;

namespace AgentPaw.ViewModels;

public partial class WorkspaceViewModel : ObservableObject
{
    private readonly OrchestratorService _orchestrator;
    private readonly ConfigLoaderService _configLoader;
    private readonly IDbContextFactory<AgentPawDbContext> _dbFactory;
    private readonly AuthService _authService;
    private readonly GoogleDocsService _googleDocs;

    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private string _projectId = string.Empty;

    [ObservableProperty]
    private string _projectName = string.Empty;

    [ObservableProperty]
    private string _workspacePath = string.Empty;

    [ObservableProperty]
    private string _inputMessage = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// PM이 User에게 재질의(pm_intervention)를 할 수 있는지 여부.
    /// false면 PM은 스스로 판단하여 결정하고 절대 User에게 묻지 않는다.
    /// </summary>
    [ObservableProperty]
    private bool _askUserEnabled = true;

    [ObservableProperty]
    private string? _errorMessage;

    public ObservableCollection<ChatMessage> Messages { get; } = [];
    public ObservableCollection<Persona> Personas { get; } = [];
    public ObservableCollection<ChatAttachment> Attachments { get; } = [];
    public ObservableCollection<Persona> MentionCandidates { get; } = [];

    // === 팀 모드 ===
    public ObservableCollection<TeamPersonaItem> TeamPickerItems { get; } = [];

    [ObservableProperty]
    private string _teamMode = "panel"; // panel | debate | chain

    public bool HasActiveTeam => TeamPickerItems.Count(x => x.IsSelected) >= 2;

    public string TeamModeLabel => TeamMode switch
    {
        "debate" => "토론",
        "chain" => "체인",
        _ => "패널"
    };

    public ControlAppearance TeamModePanelAppearance =>
        TeamMode == "panel" ? ControlAppearance.Primary : ControlAppearance.Secondary;
    public ControlAppearance TeamModeDebateAppearance =>
        TeamMode == "debate" ? ControlAppearance.Primary : ControlAppearance.Secondary;
    public ControlAppearance TeamModeChainAppearance =>
        TeamMode == "chain" ? ControlAppearance.Primary : ControlAppearance.Secondary;

    public bool HasAttachments => Attachments.Count > 0;

    [ObservableProperty]
    private bool _isMentionPopupOpen;

    [ObservableProperty]
    private int _selectedMentionIndex;

    // === Google Docs 내보내기 ===
    [ObservableProperty]
    private bool _isGoogleDocsPopupOpen;

    [ObservableProperty]
    private string _googleDocUrlInput = string.Empty;

    [ObservableProperty]
    private string? _googleDocsStatusMessage;

    [ObservableProperty]
    private bool _isGoogleDocsExporting;

    [ObservableProperty]
    private bool _isDetailOpen;

    [ObservableProperty]
    private ChatMessage? _detailMessage;

    public WorkspaceViewModel(
        OrchestratorService orchestrator,
        ConfigLoaderService configLoader,
        IDbContextFactory<AgentPawDbContext> dbFactory,
        AuthService authService,
        GoogleDocsService googleDocs)
    {
        _orchestrator = orchestrator;
        _configLoader = configLoader;
        _dbFactory = dbFactory;
        _authService = authService;
        _googleDocs = googleDocs;

        Attachments.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasAttachments));

        TeamPickerItems.CollectionChanged += (_, _) =>
        {
            foreach (var item in TeamPickerItems)
                item.PropertyChanged -= OnTeamItemChanged;
            foreach (var item in TeamPickerItems)
                item.PropertyChanged += OnTeamItemChanged;
            RefreshTeamState();
        };
    }

    private void OnTeamItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TeamPersonaItem.IsSelected))
            RefreshTeamState();
    }

    private void RefreshTeamState()
    {
        OnPropertyChanged(nameof(HasActiveTeam));
    }

    partial void OnTeamModeChanged(string value)
    {
        OnPropertyChanged(nameof(TeamModeLabel));
        OnPropertyChanged(nameof(TeamModePanelAppearance));
        OnPropertyChanged(nameof(TeamModeDebateAppearance));
        OnPropertyChanged(nameof(TeamModeChainAppearance));
    }

    [RelayCommand]
    private void ClearTeam()
    {
        foreach (var item in TeamPickerItems)
            item.IsSelected = false;
        RefreshTeamState();
    }

    public async Task AddAttachmentsAsync(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path)) continue;
            if (!path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) continue;
            if (Attachments.Any(a => string.Equals(a.Path, path, StringComparison.OrdinalIgnoreCase))) continue;

            try
            {
                var content = await System.IO.File.ReadAllTextAsync(path);
                Attachments.Add(new ChatAttachment
                {
                    Path = path,
                    Name = System.IO.Path.GetFileName(path),
                    Content = content
                });
            }
            catch (Exception ex)
            {
                ErrorMessage = $"첨부 실패: {System.IO.Path.GetFileName(path)} — {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private void RemoveAttachment(ChatAttachment attachment)
    {
        if (attachment == null) return;
        Attachments.Remove(attachment);
    }

    // === @mention ===

    public void OpenMentionPopup(string filter = "")
    {
        UpdateMentionFilter(filter);
        IsMentionPopupOpen = MentionCandidates.Count > 0;
    }

    public void UpdateMentionFilter(string filter)
    {
        MentionCandidates.Clear();
        var matched = string.IsNullOrEmpty(filter)
            ? Personas
            : Personas.Where(p =>
                (p.Label?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)
                || (p.Name?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false));
        foreach (var p in matched) MentionCandidates.Add(p);

        if (MentionCandidates.Count == 0)
            IsMentionPopupOpen = false;
        else if (SelectedMentionIndex >= MentionCandidates.Count)
            SelectedMentionIndex = 0;
    }

    public void CloseMentionPopup()
    {
        IsMentionPopupOpen = false;
        MentionCandidates.Clear();
        SelectedMentionIndex = 0;
    }

    public void MoveMentionSelection(int delta)
    {
        if (MentionCandidates.Count == 0) return;
        var next = SelectedMentionIndex + delta;
        if (next < 0) next = MentionCandidates.Count - 1;
        else if (next >= MentionCandidates.Count) next = 0;
        SelectedMentionIndex = next;
    }

    public Persona? GetSelectedMentionPersona()
    {
        if (SelectedMentionIndex < 0 || SelectedMentionIndex >= MentionCandidates.Count) return null;
        return MentionCandidates[SelectedMentionIndex];
    }

    // AskUserEnabled 토글은 프로젝트 레코드에 즉시 persist 한다.
    partial void OnAskUserEnabledChanged(bool value)
    {
        if (string.IsNullOrWhiteSpace(ProjectId)) return;
        _ = PersistAskUserEnabledAsync(ProjectId, value);
    }

    private async Task PersistAskUserEnabledAsync(string projectId, bool value)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var project = await db.Projects.FindAsync(projectId);
            if (project == null || project.AskUserEnabled == value) return;
            project.AskUserEnabled = value;
            project.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }
        catch { /* 설정 저장 실패는 경고하지 않는다 — 다음 대화에 다시 시도 가능 */ }
    }

    // 메시지 본문에서 마지막 @<label|name>을 찾아 일치하는 Persona를 반환한다
    public Persona? ResolveMention(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var matches = System.Text.RegularExpressions.Regex.Matches(text, @"@([^\s@]+)");
        if (matches.Count == 0) return null;

        // 가장 마지막 멘션을 우선 매칭 — 여러 개면 마지막이 발화자의 최종 타겟으로 간주한다
        for (int i = matches.Count - 1; i >= 0; i--)
        {
            var token = matches[i].Groups[1].Value;
            var matched = Personas.FirstOrDefault(p =>
                string.Equals(p.Label, token, StringComparison.OrdinalIgnoreCase)
                || string.Equals(p.Name, token, StringComparison.OrdinalIgnoreCase));
            if (matched != null) return matched;
        }
        return null;
    }

    public async Task LoadWorkspaceAsync(string projectId, string projectName)
    {
        ProjectId = projectId;
        ProjectName = projectName;
        Messages.Clear();
        Personas.Clear();
        TeamPickerItems.Clear();
        ErrorMessage = null;

        // 작업 폴더 경로 resolve + 재질의 정책 로드
        await using (var db = await _dbFactory.CreateDbContextAsync())
        {
            var project = await db.Projects.FindAsync(projectId);
            WorkspacePath = !string.IsNullOrWhiteSpace(project?.GitRepoPath)
                ? project!.GitRepoPath
                : ProjectSettingsViewModel.DefaultWorkspacePath(projectId);
            AskUserEnabled = project?.AskUserEnabled ?? true;
            GoogleDocUrlInput = project?.GoogleDocId ?? string.Empty;
        }

        // 페르소나 로드 — Avatar가 비어있거나 렌더 불가한 SVG면 강아지 PNG로 폴백
        var personas = await _configLoader.ListPersonasAsync(projectId);
        foreach (var p in personas)
        {
            if (string.IsNullOrWhiteSpace(p.Avatar)
                || p.Avatar.StartsWith("data:image/svg", StringComparison.OrdinalIgnoreCase))
            {
                p.Avatar = EngineAvatarService.ResolveAvatarForPersona(p.Name, p.Keywords, p.IsPm);
            }
            Personas.Add(p);
            TeamPickerItems.Add(new TeamPersonaItem { Persona = p });
        }

        // 이벤트 히스토리 로드
        await LoadHistoryAsync();
    }

    [RelayCommand]
    private void OpenWorkspaceFolder()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(WorkspacePath)) return;
            System.IO.Directory.CreateDirectory(WorkspacePath);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = WorkspacePath,
                UseShellExecute = true
            });
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    private async Task LoadHistoryAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var events = await db.EventLogs
            .AsNoTracking()
            .Where(e => e.ProjectId == ProjectId && !e.IsDeleted)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync();

        // 이벤트마다 선형 탐색하지 않도록 PersonaId → Persona 인덱스를 1회만 구축한다
        var personaById = Personas
            .Where(p => !string.IsNullOrEmpty(p.PersonaId))
            .GroupBy(p => p.PersonaId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var personaByName = Personas
            .Where(p => !string.IsNullOrEmpty(p.Name))
            .GroupBy(p => p.Name!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var evt in events)
        {
            try
            {
                if (evt.EventType == "USER_MESSAGE")
                {
                    var payload = JsonSerializer.Deserialize<JsonElement>(evt.Payload);
                    var message = payload.GetProperty("message").GetString() ?? string.Empty;
                    Messages.Add(new ChatMessage
                    {
                        Role = "user",
                        Content = message,
                        Timestamp = evt.CreatedAt
                    });
                }
                else if (evt.EventType == "PM_REPORT" || evt.EventType == "PM_INTERVENTION")
                {
                    var payload = JsonSerializer.Deserialize<JsonElement>(evt.Payload);
                    var runId = payload.TryGetProperty("runId", out var r) ? r.GetString() : null;
                    var outputsFolder = payload.TryGetProperty("outputsFolder", out var of) ? of.GetString() : null;
                    var commitSha = payload.TryGetProperty("commitSha", out var cs) ? cs.GetString() : null;
                    var reportPath = payload.TryGetProperty("reportPath", out var rp) ? rp.GetString() : null;
                    var sb = new System.Text.StringBuilder();
                    if (evt.EventType == "PM_REPORT")
                    {
                        sb.AppendLine($"✅ 프로젝트 종료 보고 — Run `{runId}`");
                        if (!string.IsNullOrWhiteSpace(outputsFolder)) sb.AppendLine($"📁 산출물 폴더: {outputsFolder}");
                        if (!string.IsNullOrWhiteSpace(commitSha)) sb.AppendLine($"🔖 Git 커밋: {commitSha[..Math.Min(8, commitSha.Length)]}");
                    }
                    else
                    {
                        sb.AppendLine($"⚠ User 개입 요청 — Run `{runId}`");
                    }
                    Messages.Add(new ChatMessage
                    {
                        Role = evt.EventType == "PM_REPORT" ? "pm_report" : "pm_intervention",
                        Content = sb.ToString().TrimEnd(),
                        Timestamp = evt.CreatedAt,
                        IsEndReport = evt.EventType == "PM_REPORT",
                        IsUserIntervention = evt.EventType == "PM_INTERVENTION",
                        RunId = runId,
                        OutputsFolder = outputsFolder,
                        CommitSha = commitSha,
                        ReportPath = reportPath
                    });
                }
                else if (evt.EventType == "AI_RESPONSE" || evt.EventType == "PM_RESPONSE")
                {
                    var payload = JsonSerializer.Deserialize<JsonElement>(evt.Payload);
                    var content = payload.GetProperty("content").GetString() ?? string.Empty;
                    var personaId = payload.TryGetProperty("personaId", out var pid) ? pid.GetString() : null;
                    Persona? persona = null;
                    if (!string.IsNullOrEmpty(personaId)) personaById.TryGetValue(personaId, out persona);
                    var isPm = evt.EventType == "PM_RESPONSE"
                        || (payload.TryGetProperty("isPm", out var ip) && ip.ValueKind == JsonValueKind.True);

                    var sb = new System.Text.StringBuilder();
                    if (!string.IsNullOrWhiteSpace(content)) sb.Append(content);

                    if (payload.TryGetProperty("toolCalls", out var tc) && tc.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var t in tc.EnumerateArray())
                        {
                            var name = t.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                            var args = t.TryGetProperty("args", out var a) ? a.GetString() ?? "" : "";
                            var success = t.TryGetProperty("success", out var s) && s.GetBoolean();
                            var result = t.TryGetProperty("result", out var r) ? r.GetString() ?? "" : "";
                            if (sb.Length > 0) sb.Append("\n\n");
                            sb.Append($"🔧 {(success ? "✓" : "✗")} {name}({args}) → {result}");
                        }
                    }

                    string? handoffTargetLabel = null;
                    string? handoffReq = null;
                    if (payload.TryGetProperty("handoffTo", out var ht) && ht.ValueKind == JsonValueKind.String)
                    {
                        var handoffTo = ht.GetString();
                        handoffReq = payload.TryGetProperty("handoffRequest", out var hr) ? hr.GetString() : null;
                        if (!string.IsNullOrEmpty(handoffTo))
                        {
                            handoffTargetLabel = personaByName.TryGetValue(handoffTo, out var ht2)
                                ? ht2.Label ?? handoffTo
                                : handoffTo;
                        }
                    }

                    int? turnIdx = null;
                    if (payload.TryGetProperty("turnIndex", out var ti) && ti.ValueKind == JsonValueKind.Number)
                        turnIdx = ti.GetInt32();

                    string? discussionId = payload.TryGetProperty("discussionId", out var did) && did.ValueKind == JsonValueKind.String
                        ? did.GetString() : null;
                    int? roundIdx = null;
                    if (payload.TryGetProperty("roundIndex", out var ri) && ri.ValueKind == JsonValueKind.Number)
                        roundIdx = ri.GetInt32();
                    string? stanceVal = payload.TryGetProperty("stance", out var stv) && stv.ValueKind == JsonValueKind.String
                        ? stv.GetString() : null;
                    bool isDiscussionSpeaker = payload.TryGetProperty("isDiscussionSpeaker", out var ids) && ids.ValueKind == JsonValueKind.True;
                    bool isDiscussionOpener = payload.TryGetProperty("isDiscussionOpener", out var ido) && ido.ValueKind == JsonValueKind.True;
                    bool isDiscussionSummary = payload.TryGetProperty("isDiscussionSummary", out var idsum) && idsum.ValueKind == JsonValueKind.True;
                    string? discussionTopic = payload.TryGetProperty("discussionTopic", out var dtop) && dtop.ValueKind == JsonValueKind.String
                        ? dtop.GetString() : null;

                    var writtenFiles = new List<string>();
                    if (payload.TryGetProperty("writtenFiles", out var wf) && wf.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var f in wf.EnumerateArray())
                            if (f.ValueKind == JsonValueKind.String) writtenFiles.Add(f.GetString()!);
                    }
                    if (writtenFiles.Count > 0)
                    {
                        sb.Append("\n\n📎 생성·수정 파일:");
                        foreach (var f in writtenFiles) sb.Append($"\n  - {f}");
                    }

                    var loadedMsg = new ChatMessage
                    {
                        Role = "assistant",
                        Content = sb.ToString().TrimEnd(),
                        PersonaLabel = persona?.Label,
                        PersonaAvatar = persona?.Avatar,
                        PersonaIcon = persona?.Icon,
                        PersonaColor = persona?.Color,
                        ModelUsed = evt.ModelUsed,
                        Timestamp = evt.CreatedAt,
                        IsPm = isPm,
                        WrittenFiles = writtenFiles,
                        TurnIndex = turnIdx,
                        HandoffToLabel = handoffTargetLabel,
                        HandoffRequest = handoffReq,
                        DiscussionId = discussionId,
                        RoundIndex = roundIdx,
                        Stance = stanceVal,
                        IsDiscussionSpeaker = isDiscussionSpeaker,
                        IsDiscussionOpener = isDiscussionOpener,
                        IsDiscussionSummary = isDiscussionSummary,
                        DiscussionTopic = discussionTopic
                    };
                    // 로드 시에도 토론 마커를 앞에 붙여 재방문 시 일관된 렌더를 보장
                    loadedMsg.Content = ApplyDiscussionMarker(loadedMsg.Content, loadedMsg);
                    Messages.Add(loadedMsg);
                }
            }
            catch { /* skip malformed events */ }
        }
    }

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        var message = InputMessage?.Trim() ?? string.Empty;
        var attachments = Attachments.ToList();
        if ((string.IsNullOrEmpty(message) && attachments.Count == 0) || _authService.CurrentUserId == null) return;

        InputMessage = string.Empty;
        Attachments.Clear();
        ErrorMessage = null;
        _cts = new CancellationTokenSource();
        IsLoading = true;

        // 사용자 메시지 표시 — 첨부파일은 파일명 목록만 노출
        var displayMessage = attachments.Count == 0
            ? message
            : (string.IsNullOrEmpty(message) ? string.Empty : message + "\n\n")
              + "📎 첨부: " + string.Join(", ", attachments.Select(a => a.Name));

        Messages.Add(new ChatMessage
        {
            Role = "user",
            Content = displayMessage,
            Timestamp = DateTimeOffset.UtcNow
        });

        // AI에게 전달할 메시지에는 첨부파일 전체 내용을 포함한다
        var payloadMessage = message;
        if (attachments.Count > 0)
        {
            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(message))
            {
                sb.Append(message);
                sb.Append("\n\n");
            }
            sb.AppendLine("[첨부 파일]");
            foreach (var a in attachments)
            {
                sb.AppendLine();
                sb.AppendLine($"--- {a.Name} ---");
                sb.AppendLine("```markdown");
                sb.AppendLine(a.Content);
                sb.AppendLine("```");
            }
            payloadMessage = sb.ToString().TrimEnd();
        }

        try
        {
            // 팀 모드가 활성이면 선택된 페르소나 목록으로 팀 파이프라인을 실행한다.
            // 비활성이면 @mention 라우팅(PM 허브 우회)을 사용한다.
            var teamIds = TeamPickerItems.Where(x => x.IsSelected).Select(x => x.Persona.PersonaId).ToList();
            var isTeamMode = teamIds.Count >= 2;
            var mentioned = isTeamMode ? null : ResolveMention(message);

            var priorConversation = BuildPriorConversation();

            var input = new OrchestratorInput
            {
                ProjectId = ProjectId,
                UserId = _authService.CurrentUserId,
                Message = payloadMessage,
                ForcePersonaId = isTeamMode ? null : mentioned?.PersonaId,
                TeamPersonaIds = isTeamMode ? teamIds : null,
                TeamMode = isTeamMode ? TeamMode : null,
                AskUserEnabled = AskUserEnabled,
                PriorConversation = priorConversation.Count > 0 ? priorConversation : null
            };

            var progress = new Progress<AgentTurn>(turn =>
            {
                var existingIdx = !string.IsNullOrEmpty(turn.StreamKey)
                    ? IndexOfStreamKey(turn.StreamKey)
                    : -1;

                if (turn.IsStreamingPreview)
                {
                    // 프리뷰: 기존 메시지가 있으면 Content만 갱신, 없으면 최초 청크로 새 메시지 생성
                    if (existingIdx >= 0)
                    {
                        Messages[existingIdx].Content = turn.Content;
                    }
                    else
                    {
                        Messages.Add(BuildAssistantMessage(turn, previewOnly: true));
                    }
                    return;
                }

                // 최종 턴: 프리뷰 메시지를 완성본으로 치환한다
                var finalized = BuildAssistantMessage(turn, previewOnly: false);
                if (existingIdx >= 0)
                    Messages[existingIdx] = finalized;
                else
                    Messages.Add(finalized);
            });

            var output = await _orchestrator.RunPipelineAsync(input, progress, _cts.Token);

            if (output.NeedsConfirmation)
            {
                Messages.Add(new ChatMessage
                {
                    Role = "system",
                    Content = output.Content,
                    Timestamp = DateTimeOffset.UtcNow
                });
            }
            else if (output.IsEndReport)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"✅ 프로젝트 종료 보고 — Run `{output.RunId}`");
                if (!string.IsNullOrWhiteSpace(output.OutputsFolder))
                    sb.AppendLine($"📁 산출물 폴더: {output.OutputsFolder}");
                if (!string.IsNullOrWhiteSpace(output.ReportPath))
                    sb.AppendLine($"📄 보고서: {output.ReportPath}");
                if (!string.IsNullOrWhiteSpace(output.CommitSha))
                    sb.AppendLine($"🔖 Git 커밋: {output.CommitSha[..Math.Min(8, output.CommitSha.Length)]}");
                Messages.Add(new ChatMessage
                {
                    Role = "pm_report",
                    Content = sb.ToString().TrimEnd(),
                    Timestamp = DateTimeOffset.UtcNow,
                    IsEndReport = true,
                    RunId = output.RunId,
                    OutputsFolder = output.OutputsFolder,
                    ReportPath = output.ReportPath,
                    CommitSha = output.CommitSha
                });
            }
            else if (output.IsUserIntervention)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("⚠ User 개입 요청");
                if (!string.IsNullOrWhiteSpace(output.InterventionReason))
                    sb.AppendLine($"사유: {output.InterventionReason}");
                if (!string.IsNullOrWhiteSpace(output.InterventionQuestion))
                    sb.AppendLine($"질문: {output.InterventionQuestion}");
                Messages.Add(new ChatMessage
                {
                    Role = "pm_intervention",
                    Content = sb.ToString().TrimEnd(),
                    Timestamp = DateTimeOffset.UtcNow,
                    IsUserIntervention = true,
                    RunId = output.RunId,
                    InterventionReason = output.InterventionReason,
                    InterventionQuestion = output.InterventionQuestion
                });
            }
        }
        catch (OperationCanceledException)
        {
            Messages.Add(new ChatMessage
            {
                Role = "system",
                Content = "⏹ 토론이 중단되었습니다. PM이 진행 내용을 정리합니다…",
                Timestamp = DateTimeOffset.UtcNow
            });

            var pm = Personas.FirstOrDefault(p => p.IsPm);
            if (pm != null && _authService.CurrentUserId != null)
            {
                try
                {
                    using var summaryCts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
                    var priorConversation = BuildPriorConversation();
                    var summaryInput = new OrchestratorInput
                    {
                        ProjectId = ProjectId,
                        UserId = _authService.CurrentUserId,
                        Message = "대화가 중단되었습니다. 지금까지 논의된 내용을 간결하게 정리하고, 현재 진행 상태와 미결 사항을 요약해주세요.",
                        ForcePersonaId = pm.PersonaId,
                        AskUserEnabled = false,
                        PriorConversation = priorConversation.Count > 0 ? priorConversation : null
                    };
                    var summaryProgress = new Progress<AgentTurn>(turn =>
                    {
                        var existingIdx = !string.IsNullOrEmpty(turn.StreamKey)
                            ? IndexOfStreamKey(turn.StreamKey)
                            : -1;
                        if (turn.IsStreamingPreview)
                        {
                            if (existingIdx >= 0)
                                Messages[existingIdx].Content = turn.Content;
                            else
                                Messages.Add(BuildAssistantMessage(turn, previewOnly: true));
                        }
                        else
                        {
                            var finalized = BuildAssistantMessage(turn, previewOnly: false);
                            if (existingIdx >= 0)
                                Messages[existingIdx] = finalized;
                            else
                                Messages.Add(finalized);
                        }
                    });
                    await _orchestrator.RunPipelineAsync(summaryInput, summaryProgress, summaryCts.Token);
                }
                catch { /* 요약 실패 무시 */ }
            }
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessage
            {
                Role = "error",
                Content = ex.Message,
                Timestamp = DateTimeOffset.UtcNow
            });
        }
        finally
        {
            IsLoading = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private void CancelDiscussion()
    {
        _cts?.Cancel();
    }

    [RelayCommand]
    private void ShowMessageDetail(ChatMessage msg)
    {
        DetailMessage = msg;
        IsDetailOpen = true;
    }

    [RelayCommand]
    private void CloseMessageDetail()
    {
        IsDetailOpen = false;
        DetailMessage = null;
    }

    [RelayCommand]
    private void CopyMessage(string? content)
    {
        if (!string.IsNullOrEmpty(content))
            System.Windows.Clipboard.SetText(content);
    }

    // 전체 대화를 markdown 문자열로 직렬화해 Clipboard로 복사한다.
    [RelayCommand]
    private void CopyConversation()
    {
        if (Messages.Count == 0)
        {
            ErrorMessage = "복사할 대화가 없다.";
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# {ProjectName} — 대화 사본");
        sb.AppendLine($"- 내보낸 시각: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine();

        foreach (var m in Messages)
        {
            string header = m.Role switch
            {
                "user" => "## 🧑 사용자",
                "assistant" => m.TurnIndex.HasValue
                    ? $"## 🐾 {m.PersonaLabel ?? "AI"} — 단계 {m.TurnIndex.Value + 1}"
                    : $"## 🐾 {m.PersonaLabel ?? "AI"}",
                "system" => "## ⚙ 시스템",
                "error" => "## ❌ 오류",
                "pm_report" => "## ✅ PM 종료 보고",
                "pm_intervention" => "## ⚠ PM 개입 요청",
                _ => $"## {m.Role}"
            };
            sb.AppendLine(header);
            sb.AppendLine($"_{m.Timestamp:yyyy-MM-dd HH:mm:ss}_");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(m.Content))
            {
                sb.AppendLine(m.Content);
                sb.AppendLine();
            }
            if (!string.IsNullOrWhiteSpace(m.HandoffToLabel))
            {
                sb.AppendLine($"↳ **{m.HandoffToLabel}** 에게 요청: {m.HandoffRequest}");
                sb.AppendLine();
            }
            if (!string.IsNullOrWhiteSpace(m.ModelUsed))
            {
                sb.AppendLine($"_모델: {m.ModelUsed}_");
                sb.AppendLine();
            }
            sb.AppendLine("---");
            sb.AppendLine();
        }

        try
        {
            System.Windows.Clipboard.SetText(sb.ToString());
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"클립보드 복사 실패: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenGoogleDocsPopup()
    {
        GoogleDocsStatusMessage = null;
        IsGoogleDocsPopupOpen = true;
    }

    [RelayCommand]
    private async Task ExportToGoogleDocsAsync()
    {
        var urlOrId = GoogleDocUrlInput?.Trim() ?? string.Empty;
        var docId = GoogleDocsService.ExtractDocId(urlOrId);
        if (string.IsNullOrWhiteSpace(docId))
        {
            GoogleDocsStatusMessage = "문서 URL 또는 ID를 입력하세요.";
            return;
        }
        if (Messages.Count == 0)
        {
            GoogleDocsStatusMessage = "내보낼 대화가 없습니다.";
            return;
        }

        IsGoogleDocsExporting = true;
        GoogleDocsStatusMessage = null;

        try
        {
            var accessToken = await _authService.GetGoogleAccessTokenAsync();
            if (string.IsNullOrEmpty(accessToken))
            {
                GoogleDocsStatusMessage = "Google 인증 토큰을 가져올 수 없습니다. 로그아웃 후 다시 로그인하세요.";
                return;
            }

            var content = BuildConversationText();
            var (success, error) = await _googleDocs.ExportAsync(docId, accessToken, content);
            if (!success)
            {
                GoogleDocsStatusMessage = error;
                return;
            }

            // 성공 시 doc ID 저장
            await SaveGoogleDocIdAsync(docId);
            GoogleDocsStatusMessage = "내보내기 완료!";
        }
        catch (Exception ex)
        {
            GoogleDocsStatusMessage = $"오류: {ex.Message}";
        }
        finally
        {
            IsGoogleDocsExporting = false;
        }
    }

    private async Task SaveGoogleDocIdAsync(string docId)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            var project = await db.Projects.FindAsync(ProjectId);
            if (project != null)
            {
                project.GoogleDocId = docId;
                project.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync();
            }
        }
        catch { }
    }

    private string BuildConversationText()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# {ProjectName} — 대화 사본");
        sb.AppendLine($"내보낸 시각: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine();

        foreach (var m in Messages)
        {
            string header = m.Role switch
            {
                "user" => "## 사용자",
                "assistant" => $"## {m.PersonaLabel ?? "AI"}",
                "system" => "## 시스템",
                "error" => "## 오류",
                "pm_report" => "## PM 종료 보고",
                "pm_intervention" => "## PM 개입 요청",
                _ => $"## {m.Role}"
            };
            sb.AppendLine(header);
            sb.AppendLine($"{m.Timestamp:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(m.Content))
            {
                sb.AppendLine(m.Content);
                sb.AppendLine();
            }
            sb.AppendLine("---");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    // PM 보고서 REPORT.md를 SaveFileDialog로 다운로드한다.
    [RelayCommand]
    private void DownloadPmReport(ChatMessage msg)
    {
        if (msg == null) return;

        var sourcePath = msg.ReportPath;
        if (string.IsNullOrWhiteSpace(sourcePath) && !string.IsNullOrWhiteSpace(msg.OutputsFolder))
            sourcePath = System.IO.Path.Combine(msg.OutputsFolder!, "pm", "REPORT.md");

        if (string.IsNullOrWhiteSpace(sourcePath) || !System.IO.File.Exists(sourcePath))
        {
            ErrorMessage = "보고서 파일을 찾을 수 없다.";
            return;
        }

        try
        {
            var runTag = !string.IsNullOrWhiteSpace(msg.RunId)
                ? msg.RunId
                : DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var defaultName = $"REPORT_{runTag}.md";
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "PM 보고서 저장",
                Filter = "Markdown (*.md)|*.md|All files (*.*)|*.*",
                FileName = defaultName,
                DefaultExt = ".md"
            };
            if (dlg.ShowDialog() != true) return;
            System.IO.File.Copy(sourcePath, dlg.FileName, overwrite: true);
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"보고서 저장 실패: {ex.Message}";
        }
    }

    private List<ConversationTurn> BuildPriorConversation()
    {
        // 방금 추가된 user 메시지(마지막)는 제외하고 이전 대화만 수집
        var result = new List<ConversationTurn>();
        var source = Messages.Take(Messages.Count - 1)
            .Where(m => m.Role is "user" or "assistant" or "pm_report" or "pm_intervention")
            .ToList();

        foreach (var msg in source)
        {
            var role = msg.Role == "user" ? "user" : "assistant";
            var body = string.IsNullOrWhiteSpace(msg.Content) ? "(응답 없음)" : msg.Content;
            // assistant 메시지에는 페르소나 이름을 prefix해서 누가 말한 것인지 구분 가능하게 함
            var content = (role == "assistant" && !string.IsNullOrWhiteSpace(msg.PersonaLabel))
                ? $"[{msg.PersonaLabel}]\n{body}"
                : body;

            // Claude API는 user/assistant 교대를 강제하므로 연속 동일 Role은 하나로 합침
            if (result.Count > 0 && result[^1].Role == role)
                result[^1].Content = result[^1].Content + "\n\n" + content;
            else
                result.Add(new ConversationTurn { Role = role, Content = content });
        }

        // Claude API 요구: 반드시 user 턴으로 시작
        while (result.Count > 0 && result[0].Role != "user")
            result.RemoveAt(0);

        // 최대 20턴(10회 교환)으로 제한
        if (result.Count > 20)
            result = result.Skip(result.Count - 20).ToList();

        // Skip 후 다시 user로 시작하도록 보정
        while (result.Count > 0 && result[0].Role != "user")
            result.RemoveAt(0);

        return result;
    }

    private int IndexOfStreamKey(string streamKey)
    {
        for (int i = Messages.Count - 1; i >= 0; i--)
        {
            if (Messages[i].StreamKey == streamKey) return i;
        }
        return -1;
    }

    private ChatMessage BuildAssistantMessage(AgentTurn turn, bool previewOnly)
    {
        var p = Personas.FirstOrDefault(x => x.PersonaId == turn.PersonaId);

        string content;
        if (previewOnly)
        {
            content = PrependDiscussionMarker(turn.Content, turn);
        }
        else
        {
            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrWhiteSpace(turn.Content))
                sb.Append(turn.Content);

            if (turn.ToolCalls.Count > 0)
            {
                if (sb.Length > 0) sb.Append("\n\n");
                foreach (var t in turn.ToolCalls)
                {
                    var mark = t.Success ? "✓" : "✗";
                    sb.Append($"🔧 {mark} {t.Name}({t.ArgsSummary}) → {t.Result}\n");
                }
            }

            if (turn.WrittenFiles.Count > 0)
            {
                sb.Append("\n\n📎 생성·수정 파일:");
                foreach (var f in turn.WrittenFiles.Distinct())
                    sb.Append($"\n  - {f}");
            }

            content = PrependDiscussionMarker(sb.ToString().TrimEnd(), turn);
        }

        return new ChatMessage
        {
            Role = "assistant",
            Content = content,
            PersonaLabel = turn.PersonaLabel,
            PersonaAvatar = !string.IsNullOrWhiteSpace(p?.Avatar) ? p!.Avatar : turn.PersonaAvatar,
            PersonaIcon = p?.Icon,
            PersonaColor = p?.Color,
            ModelUsed = previewOnly ? null : turn.ModelUsed,
            Timestamp = DateTimeOffset.UtcNow,
            IsPm = turn.IsPm,
            IsPmGreeting = turn.IsPmGreeting,
            IsEndReport = turn.IsEndReport,
            IsUserIntervention = turn.IsUserIntervention,
            WrittenFiles = previewOnly ? [] : turn.WrittenFiles.Distinct().ToList(),
            TurnIndex = previewOnly ? null : turn.TurnIndex,
            HandoffToLabel = previewOnly ? null : turn.HandoffToLabel,
            HandoffRequest = previewOnly ? null : turn.HandoffRequest,
            DiscussionId = turn.DiscussionId,
            RoundIndex = turn.RoundIndex,
            Stance = turn.Stance,
            IsDiscussionSpeaker = turn.IsDiscussionSpeaker,
            IsDiscussionOpener = turn.IsDiscussionOpener,
            IsDiscussionSummary = turn.IsDiscussionSummary,
            DiscussionTopic = turn.DiscussionTopic,
            StreamKey = turn.StreamKey,
            IsStreaming = previewOnly
        };
    }

    // 토론 참여자·개시·정리 턴에 시각적 구분선을 달아 일반 대화와 혼동되지 않도록 한다
    private static string PrependDiscussionMarker(string content, AgentTurn turn)
        => BuildDiscussionMarker(
            content, turn.IsDiscussionOpener, turn.IsDiscussionSummary,
            turn.IsDiscussionSpeaker, turn.RoundIndex, turn.Stance, turn.DiscussionTopic);

    private static string ApplyDiscussionMarker(string content, ChatMessage msg)
        => BuildDiscussionMarker(
            content, msg.IsDiscussionOpener, msg.IsDiscussionSummary,
            msg.IsDiscussionSpeaker, msg.RoundIndex, msg.Stance, msg.DiscussionTopic);

    private static string BuildDiscussionMarker(
        string content, bool isOpener, bool isSummary, bool isSpeaker,
        int? roundIndex, string? stance, string? topic)
    {
        if (isOpener)
        {
            var topicTag = string.IsNullOrWhiteSpace(topic) ? "" : $" — {topic}";
            var header = $"💬 **다자 토론 개시**{topicTag}";
            return string.IsNullOrWhiteSpace(content) ? header : header + "\n\n" + content;
        }
        if (isSummary)
        {
            return string.IsNullOrWhiteSpace(content)
                ? "💬 **다자 토론 정리**"
                : "💬 **다자 토론 정리**\n\n" + content;
        }
        if (isSpeaker)
        {
            var round = (roundIndex ?? 0) + 1;
            var stanceLabel = stance switch
            {
                "agree" => "동의",
                "object" => "반대",
                "extend" => "보완",
                _ => stance ?? ""
            };
            var header = string.IsNullOrWhiteSpace(stanceLabel)
                ? $"💬 라운드 {round}"
                : $"💬 라운드 {round} · {stanceLabel}";
            return string.IsNullOrWhiteSpace(content) ? header : header + "\n\n" + content;
        }
        return content;
    }
}

public partial class ChatMessage : ObservableObject
{
    public string Role { get; set; } = string.Empty; // user, assistant, system, error, pm_report, pm_intervention

    // 스트리밍 중 실시간 갱신되는 본문 — ObservableProperty로 바인딩 재평가를 유발한다
    [ObservableProperty]
    private string _content = string.Empty;

    public string? PersonaLabel { get; set; }
    public string? PersonaAvatar { get; set; }
    public string? PersonaIcon { get; set; }
    public string? PersonaColor { get; set; }
    public string? ModelUsed { get; set; }
    public DateTimeOffset Timestamp { get; set; }

    // PM 허브 상태
    public bool IsPm { get; set; }
    public bool IsPmGreeting { get; set; }
    public bool IsEndReport { get; set; }
    public bool IsUserIntervention { get; set; }
    public List<string> WrittenFiles { get; set; } = [];
    public string? OutputsFolder { get; set; }
    public string? ReportPath { get; set; }
    public string? CommitSha { get; set; }
    public string? InterventionReason { get; set; }
    public string? InterventionQuestion { get; set; }
    public string? RunId { get; set; }

    // 에이전트 단계 모니터링
    public int? TurnIndex { get; set; }
    public string? HandoffToLabel { get; set; }
    public string? HandoffRequest { get; set; }
    public bool HasHandoff => !string.IsNullOrWhiteSpace(HandoffToLabel);
    public bool HasTurnIndex => TurnIndex.HasValue;
    public string TurnBadge => TurnIndex.HasValue ? $"단계 {TurnIndex.Value + 1}" : string.Empty;
    public bool HasReportPath => !string.IsNullOrWhiteSpace(ReportPath) || !string.IsNullOrWhiteSpace(OutputsFolder);

    // 다자 토론(round-table)
    public string? DiscussionId { get; set; }
    public int? RoundIndex { get; set; }
    public string? Stance { get; set; }
    public bool IsDiscussionSpeaker { get; set; }
    public bool IsDiscussionOpener { get; set; }
    public bool IsDiscussionSummary { get; set; }
    public string? DiscussionTopic { get; set; }
    public bool IsDiscussion => IsDiscussionSpeaker || IsDiscussionOpener || IsDiscussionSummary;

    // 스트리밍 턴 식별자 — 프리뷰 업데이트와 최종 치환을 매칭한다
    public string? StreamKey { get; set; }

    // 스트리밍 중 여부 — UI에서 "응답 생성 중..." 표시 및 렌더러 스위칭에 사용
    [ObservableProperty]
    private bool _isStreaming;

    // === 요약 / 상세 보기 ===
    private const int SummaryThreshold = 300;

    public bool IsLong => (Content?.Length ?? 0) > SummaryThreshold;

    public string SummaryContent
    {
        get
        {
            if (!IsLong) return Content ?? string.Empty;
            var text = Content![..SummaryThreshold];
            var lastBreak = text.LastIndexOfAny(['\n', '.', ' ']);
            return (lastBreak > 200 ? text[..lastBreak] : text) + "…";
        }
    }

    // 스트리밍 완료 + 짧은 내용 → 전체 마크다운
    public bool ShowMarkdown => !IsStreaming && !IsLong;

    // 스트리밍 완료 + 긴 내용 → 요약 텍스트 + 상세히 보기 버튼
    public bool ShowSummaryText => !IsStreaming && IsLong;

    partial void OnContentChanged(string value)
    {
        OnPropertyChanged(nameof(IsLong));
        OnPropertyChanged(nameof(SummaryContent));
        OnPropertyChanged(nameof(ShowMarkdown));
        OnPropertyChanged(nameof(ShowSummaryText));
    }

    partial void OnIsStreamingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowMarkdown));
        OnPropertyChanged(nameof(ShowSummaryText));
    }
}

public class ChatAttachment
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public partial class TeamPersonaItem : ObservableObject
{
    public Persona Persona { get; set; } = null!;

    [ObservableProperty]
    private bool _isSelected;
}
