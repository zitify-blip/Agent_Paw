using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using AgentPaw.Data;
using AgentPaw.Models;
using AgentPaw.Services;

namespace AgentPaw.Orchestrator;

public class OrchestratorService
{
    private readonly IDbContextFactory<AgentPawDbContext> _dbFactory;
    private readonly ConfigLoaderService _configLoader;
    private readonly ClassifierService _classifier;
    private readonly ContextInjectorService _contextInjector;
    private readonly AiClientService _aiClient;
    private readonly SelfCriticService _selfCritic;
    private readonly ToolExecutorService _toolExecutor;
    private readonly PmReportService _pmReport;
    private readonly WikiService _wiki;

    private const int MaxIterations = 24;
    private const int MaxHandoffs = 12;
    // 토론 설정 기본값 — 프로젝트별 설정이 없을 때 폴백
    private const int DefaultMaxDiscussionRounds = 10;
    private const int DefaultMaxDiscussionParticipants = 4;

    public OrchestratorService(
        IDbContextFactory<AgentPawDbContext> dbFactory,
        ConfigLoaderService configLoader,
        ClassifierService classifier,
        ContextInjectorService contextInjector,
        AiClientService aiClient,
        SelfCriticService selfCritic,
        ToolExecutorService toolExecutor,
        PmReportService pmReport,
        WikiService wiki)
    {
        _dbFactory = dbFactory;
        _configLoader = configLoader;
        _classifier = classifier;
        _contextInjector = contextInjector;
        _aiClient = aiClient;
        _selfCritic = selfCritic;
        _toolExecutor = toolExecutor;
        _pmReport = pmReport;
        _wiki = wiki;
    }

    public async Task<OrchestratorOutput> RunPipelineAsync(
        OrchestratorInput input,
        IProgress<AgentTurn>? progress = null,
        CancellationToken ct = default)
    {
        if (input.TeamPersonaIds?.Count >= 2)
            return await RunTeamPipelineAsync(input, progress, ct);

        var personas = await _configLoader.ListPersonasAsync(input.ProjectId);
        if (personas.Count == 0)
            throw new InvalidOperationException("프로젝트에 페르소나가 없습니다.");

        // 멀티에이전트: PM + 비PM이 1명 이상이면 자유 토론 모드 (ForcePersonaId 지정 시 단일 에이전트 유지)
        if (input.ForcePersonaId == null
            && personas.Any(p => p.IsPm)
            && personas.Count(p => !p.IsPm) >= 1)
            return await RunFreeDiscussionPipelineAsync(input, personas, progress, ct);

        var workspaceRoot = await ResolveWorkspaceRootAsync(input.ProjectId);
        var pmPersona = personas.FirstOrDefault(p => p.IsPm);
        var askUserEnabled = await ResolveAskUserEnabledAsync(input);
        var (maxDiscussionRounds, maxDiscussionParticipants) = await ResolveDiscussionSettingsAsync(input.ProjectId);
        var runId = Guid.NewGuid().ToString("N")[..8];

        var classification = _classifier.Classify(input.Message, personas, input.ForcePersonaId);

        if (classification.NeedsConfirmation)
        {
            return new OrchestratorOutput
            {
                RunId = runId,
                PersonaId = classification.PersonaId,
                Confidence = classification.Confidence,
                NeedsConfirmation = true,
                Content = "어떤 에이전트에게 요청할까요?"
            };
        }

        var currentPersonaId = classification.PersonaId;
        var currentRequest = input.Message;
        string? fromPersonaLabel = null;
        var history = new List<AgentTurn>();
        var seenHandoffs = new HashSet<string>();
        int handoffCount = 0;
        string? pendingToolFeedback = null;
        string? pendingDiscussionFeedback = null;
        AgentTurn? lastTurn = null;
        bool endReport = false;
        bool userIntervention = false;
        string pmReportSummary = string.Empty;
        string pmReportBody = string.Empty;
        string interventionReason = string.Empty;
        string interventionQuestion = string.Empty;
        int iterationsUsed = 0;

        for (int iter = 0; iter < MaxIterations; iter++)
        {
            iterationsUsed = iter + 1;
            var config = await _configLoader.GetPersonaConfigAsync(currentPersonaId, input.ProjectId);
            var isCurrentPm = pmPersona != null && config.PersonaId == pmPersona.PersonaId;

            var addendum = BuildProtocolAddendum(personas, config.Name, workspaceRoot, isCurrentPm, pmPersona, askUserEnabled, maxDiscussionRounds, maxDiscussionParticipants);
            var systemPrompt = string.IsNullOrWhiteSpace(addendum)
                ? config.SystemPrompt
                : config.SystemPrompt + "\n\n" + addendum;

            string userPrompt;
            if (iter == 0)
            {
                userPrompt = await _contextInjector.InjectAsync(
                    currentRequest, input.ProjectId, config.Name, classification.Confidence);
            }
            else if (pendingToolFeedback != null)
            {
                userPrompt = BuildToolFeedbackPrompt(input.Message, history, pendingToolFeedback);
                pendingToolFeedback = null;
            }
            else if (pendingDiscussionFeedback != null)
            {
                userPrompt = BuildDiscussionReviewPrompt(input.Message, history, pendingDiscussionFeedback);
                pendingDiscussionFeedback = null;
            }
            else
            {
                userPrompt = BuildTeamContextPrompt(input.Message, history, fromPersonaLabel ?? "팀", currentRequest);
            }

            var streamKey = $"{runId}-{iter}";
            var streamBuffer = new StringBuilder();
            var lastEmitTicks = 0L;
            // 30fps (33ms) 시간 기반 throttle — 토큰이 쌓이는 대로 UI에 즉시 반영하되 재렌더 폭주는 막는다
            const long EmitIntervalTicks = TimeSpan.TicksPerMillisecond * 33;

            void EmitPreview(bool force)
            {
                if (progress == null) return;
                var nowTicks = DateTime.UtcNow.Ticks;
                if (!force && nowTicks - lastEmitTicks < EmitIntervalTicks) return;
                lastEmitTicks = nowTicks;

                progress.Report(new AgentTurn
                {
                    TurnIndex = iter,
                    PersonaId = config.PersonaId,
                    PersonaName = config.Name,
                    PersonaLabel = config.Label,
                    PersonaAvatar = config.Avatar,
                    IsPm = isCurrentPm,
                    Content = CleanStreamingPreview(streamBuffer.ToString()),
                    ModelUsed = string.Empty,
                    StreamKey = streamKey,
                    IsStreamingPreview = true,
                    IsPmGreeting = isCurrentPm && iter == 0
                });
            }

            // 첫 토큰이 도착하기 전에 빈 프리뷰 버블을 띄워 "생성 중" 상태를 즉시 노출한다
            EmitPreview(force: true);

            var response = await _aiClient.ChatWithFallbackStreamAsync(
                config.PrimaryModel,
                config.FallbackModel,
                systemPrompt,
                userPrompt,
                config.Temperature,
                config.MaxTokens,
                onDelta: chunk =>
                {
                    streamBuffer.Append(chunk);
                    EmitPreview(force: false);
                },
                history: iter == 0 ? input.PriorConversation : null,
                ct: ct);

            var toolParse = ToolCallParser.Parse(response.Content);
            var afterTools = toolParse.CleanedContent;

            // PM 전용 블록은 PM 턴에서만 해석한다
            PmBlockResult? pmBlock = null;
            DiscussionOpenResult? discussionOpen = null;
            DiscussionSummaryResult? discussionSummary = null;
            string cleanedForHandoff = afterTools;
            if (isCurrentPm)
            {
                pmBlock = PmBlockParser.Parse(afterTools);
                cleanedForHandoff = pmBlock.CleanedContent;

                discussionOpen = DiscussionBlockParser.ParseOpen(cleanedForHandoff);
                cleanedForHandoff = discussionOpen.CleanedContent;

                discussionSummary = DiscussionBlockParser.ParseSummary(cleanedForHandoff);
                cleanedForHandoff = discussionSummary.CleanedContent;
            }

            // wiki_save 블록은 모든 페르소나가 발신 가능하다 — 의사결정·명세·트러블슈팅을 위키로 승격한다
            var turnEventId = Guid.NewGuid().ToString();
            var wikiParse = WikiSaveParser.Parse(cleanedForHandoff);
            cleanedForHandoff = wikiParse.CleanedContent;
            foreach (var block in wikiParse.Saves)
            {
                try
                {
                    await _wiki.CreateWikiAsync(
                        input.ProjectId, block.Category, block.Title, block.Content, sourceEventId: turnEventId);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[WikiSave] FAILED: {ex.GetType().Name}: {ex.Message}");
                }
            }

            var handoff = HandoffParser.Parse(cleanedForHandoff);

            var toolRecords = new List<ToolCallRecord>();
            var writtenFiles = new List<string>();
            if (toolParse.HasCalls)
            {
                foreach (var call in toolParse.Calls)
                {
                    var exec = await _toolExecutor.ExecuteAsync(workspaceRoot, call.Name, call.Args);
                    toolRecords.Add(new ToolCallRecord
                    {
                        Name = call.Name,
                        ArgsSummary = SummarizeArgs(call.Args),
                        Success = exec.Success,
                        Result = exec.Message
                    });

                    if (exec.Success && IsFileWritingTool(call.Name))
                    {
                        var relPath = GetStringArg(call.Args, "path");
                        if (!string.IsNullOrWhiteSpace(relPath))
                            writtenFiles.Add(relPath);
                    }
                }
            }

            // PM이 토론을 개시했으면 handoff 블록을 무시한다(토론 서브루틴이 화자 순서를 제어)
            bool pmOpeningDiscussion = isCurrentPm && discussionOpen?.HasOpen == true;
            if (pmOpeningDiscussion)
            {
                handoff = new HandoffResult { HasHandoff = false, CleanedContent = cleanedForHandoff };
            }

            Persona? nextPersona = null;
            bool autoReturnToPm = false;
            if (!toolParse.HasCalls && handoff.HasHandoff)
            {
                nextPersona = personas.FirstOrDefault(p =>
                    string.Equals(p.Name, handoff.To, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p.Label, handoff.To, StringComparison.OrdinalIgnoreCase));

                if (nextPersona == null || nextPersona.PersonaId == currentPersonaId)
                {
                    handoff = new HandoffResult { HasHandoff = false, CleanedContent = handoff.CleanedContent };
                    nextPersona = null;
                }
                else
                {
                    var key = $"{nextPersona.PersonaId}::{handoff.Request.Trim()}";
                    if (!seenHandoffs.Add(key) || handoffCount >= MaxHandoffs)
                    {
                        handoff = new HandoffResult { HasHandoff = false, CleanedContent = handoff.CleanedContent };
                        nextPersona = null;
                    }
                }
            }

            // PM 허브 자동 복귀: 비PM 페르소나가 handoff·도구 호출을 끝내고도 pm_report/intervention이 없으면 PM에게 복귀
            if (!toolParse.HasCalls && nextPersona == null && !isCurrentPm && pmPersona != null
                && handoffCount < MaxHandoffs)
            {
                nextPersona = pmPersona;
                autoReturnToPm = true;
            }

            var turnContent = toolParse.HasCalls
                ? toolParse.CleanedContent
                : (isCurrentPm ? cleanedForHandoff : handoff.CleanedContent);
            bool pmHasSpecialBlock = isCurrentPm && (
                pmBlock?.HasReport == true
                || pmBlock?.HasIntervention == true
                || discussionOpen?.HasOpen == true
                || discussionSummary?.HasSummary == true);
            if (string.IsNullOrWhiteSpace(turnContent) && toolRecords.Count == 0 && nextPersona == null
                && !pmHasSpecialBlock)
            {
                turnContent = "(응답 없음)";
            }

            var turn = new AgentTurn
            {
                EventId = turnEventId,
                TurnIndex = iter,
                PersonaId = config.PersonaId,
                PersonaName = config.Name,
                PersonaLabel = config.Label,
                PersonaAvatar = config.Avatar,
                IsPm = isCurrentPm,
                Content = turnContent,
                ModelUsed = response.ModelUsed,
                HandoffToLabel = nextPersona?.Label,
                HandoffToName = nextPersona?.Name,
                HandoffRequest = (nextPersona != null && !autoReturnToPm) ? handoff.Request : null,
                ToolCalls = toolRecords,
                WrittenFiles = writtenFiles,
                IsEndReport = isCurrentPm && pmBlock?.HasReport == true,
                IsUserIntervention = isCurrentPm && pmBlock?.HasIntervention == true,
                IsPmGreeting = isCurrentPm && iter == 0,
                IsDiscussionOpener = pmOpeningDiscussion,
                IsDiscussionSummary = isCurrentPm && discussionSummary?.HasSummary == true,
                DiscussionTopic = pmOpeningDiscussion ? discussionOpen?.Topic : null,
                StreamKey = streamKey,
                IsStreamingPreview = false
            };

            history.Add(turn);
            lastTurn = turn;
            progress?.Report(turn);

            if (toolParse.HasCalls)
            {
                pendingToolFeedback = BuildToolResultsText(toolRecords);
                continue;
            }

            // PM이 종료 보고 또는 User 개입 요청을 발신했으면 즉시 루프 종료
            if (isCurrentPm && pmBlock != null)
            {
                if (pmBlock.HasReport)
                {
                    endReport = true;
                    pmReportSummary = pmBlock.ReportSummary;
                    pmReportBody = pmBlock.ReportBody;
                    break;
                }
                if (pmBlock.HasIntervention)
                {
                    userIntervention = true;
                    interventionReason = pmBlock.InterventionReason;
                    interventionQuestion = pmBlock.InterventionQuestion;
                    break;
                }
            }

            // PM이 다자 토론을 개시했으면 인라인으로 실행하고 전사를 PM 다음 턴의 입력으로 넘긴다
            if (pmOpeningDiscussion && discussionOpen != null && pmPersona != null)
            {
                var validated = ValidateDiscussionParticipants(discussionOpen.Participants, personas, pmPersona, maxDiscussionParticipants);
                if (validated.Count >= 2)
                {
                    var rounds = Math.Clamp(discussionOpen.Rounds, 1, maxDiscussionRounds);
                    var transcript = await RunDiscussionAsync(
                        input, personas, pmPersona, askUserEnabled,
                        discussionOpen.Topic, discussionOpen.StanceHint,
                        validated, rounds, history, progress, runId,
                        maxParticipants: maxDiscussionParticipants);
                    pendingDiscussionFeedback = transcript;
                    currentPersonaId = pmPersona.PersonaId;
                    fromPersonaLabel = "다자 토론";
                    // 토론 도중 iter 상한에 걸려 PM 검토 턴이 누락되더라도 OrchestratorOutput이 최신 발언을 반영하도록 갱신
                    if (history.Count > 0) lastTurn = history[^1];
                    continue;
                }
                // 참여자 2인 미만이면 토론 무효화 → PM에게 다시 위임 결정 요청
                pendingDiscussionFeedback = "⚠ 참여자가 2인 이상 유효하지 않아 토론이 개시되지 않았다. 다른 방법(handoff 등)으로 진행한다.";
                currentPersonaId = pmPersona.PersonaId;
                fromPersonaLabel = "시스템";
                continue;
            }

            if (nextPersona == null)
                break;

            if (!autoReturnToPm)
                handoffCount++;

            currentPersonaId = nextPersona.PersonaId;
            currentRequest = autoReturnToPm
                ? BuildAutoReturnRequest(config.Label, turnContent, writtenFiles)
                : handoff.Request;
            fromPersonaLabel = config.Label;
        }

        // 루프가 상한에 막힌 경우 시스템 메시지 추가
        if (iterationsUsed >= MaxIterations && lastTurn != null
            && (lastTurn.ToolCalls.Count > 0 || lastTurn.HandoffToName != null))
        {
            var limitTurn = new AgentTurn
            {
                TurnIndex = history.Count,
                PersonaId = lastTurn.PersonaId,
                PersonaLabel = "시스템",
                PersonaAvatar = string.Empty,
                Content = $"⚠ 반복 상한({MaxIterations}회)에 도달해 작업을 중단합니다. 필요하면 다시 요청하세요.",
                ModelUsed = string.Empty
            };
            history.Add(limitTurn);
            lastTurn = limitTurn;
            progress?.Report(limitTurn);
        }

        if (lastTurn == null)
            throw new InvalidOperationException("페르소나 응답을 생성하지 못했습니다.");

        // 종료 보고 시 산출물 취합·Git 커밋
        string? outputsFolder = null;
        string? reportPath = null;
        string? commitSha = null;
        if (endReport)
        {
            try
            {
                var ctx = new PmReportContext
                {
                    WorkspaceRoot = workspaceRoot,
                    RunId = runId,
                    OriginalUserMessage = input.Message,
                    ReportSummary = pmReportSummary,
                    ReportBody = pmReportBody,
                    Turns = history
                        .Where(t => t.PersonaId != "시스템" && !string.IsNullOrWhiteSpace(t.PersonaId))
                        .Select(t => new TurnOutputRecord
                        {
                            TurnIndex = t.TurnIndex,
                            PersonaName = t.PersonaName,
                            PersonaLabel = t.PersonaLabel,
                            Content = t.Content,
                            ModelUsed = t.ModelUsed,
                            WrittenFiles = t.WrittenFiles
                        })
                        .ToList()
                };
                var aggregated = _pmReport.Aggregate(ctx);
                outputsFolder = aggregated.OutputsFolder;
                reportPath = aggregated.ReportPath;
                commitSha = aggregated.CommitSha;
            }
            catch
            {
                // 취합 실패는 보고 자체를 막지 않는다. 추후 재시도 큐로 분리 가능.
            }
        }

        var eventId = await LogEventsAsync(input, history, runId, endReport, userIntervention, outputsFolder, commitSha, reportPath);

        return new OrchestratorOutput
        {
            EventId = eventId,
            RunId = runId,
            PersonaId = lastTurn.PersonaId,
            PersonaLabel = lastTurn.PersonaLabel,
            PersonaAvatar = lastTurn.PersonaAvatar,
            Content = lastTurn.Content,
            ModelUsed = lastTurn.ModelUsed,
            Verified = true,
            Confidence = classification.Confidence,
            NeedsConfirmation = false,
            Turns = history,
            IsEndReport = endReport,
            IsUserIntervention = userIntervention,
            OutputsFolder = outputsFolder,
            ReportPath = reportPath,
            CommitSha = commitSha,
            InterventionReason = userIntervention ? interventionReason : null,
            InterventionQuestion = userIntervention ? interventionQuestion : null
        };
    }

    // === 다자 토론(round-table) ===

    private static List<Persona> ValidateDiscussionParticipants(
        List<string> names, List<Persona> allPersonas, Persona pm,
        int maxParticipants = DefaultMaxDiscussionParticipants)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<Persona>();
        foreach (var raw in names)
        {
            var n = raw?.Trim();
            if (string.IsNullOrEmpty(n)) continue;
            var match = allPersonas.FirstOrDefault(p =>
                string.Equals(p.Name, n, StringComparison.OrdinalIgnoreCase)
                || string.Equals(p.Label, n, StringComparison.OrdinalIgnoreCase));
            if (match == null) continue;
            if (match.PersonaId == pm.PersonaId) continue;  // PM은 참여자에서 제외
            if (!seen.Add(match.PersonaId)) continue;
            result.Add(match);
            if (result.Count >= maxParticipants) break;
        }
        return result;
    }

    private async Task<string> RunDiscussionAsync(
        OrchestratorInput input,
        List<Persona> allPersonas,
        Persona pm,
        bool askUserEnabled,
        string topic,
        string stanceHint,
        List<Persona> participants,
        int rounds,
        List<AgentTurn> history,
        IProgress<AgentTurn>? progress,
        string runId,
        int maxParticipants = DefaultMaxDiscussionParticipants,
        bool isFreeMode = false,
        CancellationToken ct = default)
    {
        var discussionId = Guid.NewGuid().ToString("N")[..8];
        int speakerCounter = 0;
        // 합의(전원 agree)가 주 종료 조건. maxTurns는 무한 루프 방지 안전 캡.
        int maxTurns = rounds * maxParticipants * 3;

        // 각 참여자가 마지막으로 발언한 턴 인덱스 (-1 = 미발언)
        var lastSpoke = participants.ToDictionary(p => p.PersonaId, _ => -1);
        var currentSpeaker = participants[0];
        // 조기 종료용 슬라이딩 윈도우 — 참여자 수만큼 최근 stance 추적
        var stanceWindow = new Queue<string>();

        for (int turn = 0; turn < maxTurns; turn++)
        {
            var speaker = currentSpeaker;
            var roundIndex = turn / participants.Count;
            var config = await _configLoader.GetPersonaConfigAsync(speaker.PersonaId, input.ProjectId);
            var addendum = BuildDiscussionSpeakerAddendum(
                topic, stanceHint, participants, config.Name, roundIndex, rounds, isFreeMode);
            var systemPrompt = string.IsNullOrWhiteSpace(addendum)
                ? config.SystemPrompt
                : config.SystemPrompt + "\n\n" + addendum;

            var userPrompt = BuildDiscussionSpeakerPrompt(
                input.Message, topic, history, discussionId);

            var streamKey = $"{runId}-d-{discussionId}-t{turn}";
            var streamBuffer = new StringBuilder();
            long lastEmitTicks = 0L;
            const long EmitIntervalTicks = TimeSpan.TicksPerMillisecond * 33;

            void EmitPreview(bool force)
            {
                if (progress == null) return;
                var nowTicks = DateTime.UtcNow.Ticks;
                if (!force && nowTicks - lastEmitTicks < EmitIntervalTicks) return;
                lastEmitTicks = nowTicks;

                progress.Report(new AgentTurn
                {
                    TurnIndex = history.Count,
                    PersonaId = config.PersonaId,
                    PersonaName = config.Name,
                    PersonaLabel = config.Label,
                    PersonaAvatar = config.Avatar,
                    IsPm = false,
                    Content = CleanStreamingPreview(streamBuffer.ToString()),
                    ModelUsed = string.Empty,
                    StreamKey = streamKey,
                    IsStreamingPreview = true,
                    DiscussionId = discussionId,
                    RoundIndex = roundIndex,
                    SpeakerOrder = speakerCounter,
                    IsDiscussionSpeaker = true,
                    DiscussionTopic = topic
                });
            }

            // 토론 화자도 동일 — 첫 토큰 전 빈 버블을 먼저 띄운다
            EmitPreview(force: true);

            var response = await _aiClient.ChatWithFallbackStreamAsync(
                config.PrimaryModel,
                config.FallbackModel,
                systemPrompt,
                userPrompt,
                config.Temperature,
                config.MaxTokens,
                onDelta: chunk =>
                {
                    streamBuffer.Append(chunk);
                    EmitPreview(force: false);
                },
                history: turn == 0 ? input.PriorConversation : null,
                ct: ct);

            var stance = DiscussionBlockParser.ParseStance(response.Content);
            var afterStance = stance.CleanedContent;

            // 토론 중에도 wiki_save 블록을 발신할 수 있다 — 쟁점 정리나 합의 사항을 즉시 위키로 승격한다
            var dEventId = Guid.NewGuid().ToString();
            var dWikiParse = WikiSaveParser.Parse(afterStance);
            afterStance = dWikiParse.CleanedContent;
            foreach (var block in dWikiParse.Saves)
            {
                try
                {
                    await _wiki.CreateWikiAsync(
                        input.ProjectId, block.Category, block.Title, block.Content, sourceEventId: dEventId);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[WikiSave] FAILED: {ex.GetType().Name}: {ex.Message}");
                }
            }

            var content = afterStance;
            if (string.IsNullOrWhiteSpace(content)) content = "(응답 없음)";

            var agentTurn = new AgentTurn
            {
                EventId = dEventId,
                TurnIndex = history.Count,
                PersonaId = config.PersonaId,
                PersonaName = config.Name,
                PersonaLabel = config.Label,
                PersonaAvatar = config.Avatar,
                IsPm = false,
                Content = content,
                ModelUsed = response.ModelUsed,
                DiscussionId = discussionId,
                RoundIndex = roundIndex,
                SpeakerOrder = speakerCounter++,
                Stance = stance.HasStance ? stance.Position : "extend",
                IsDiscussionSpeaker = true,
                DiscussionTopic = topic,
                StreamKey = streamKey,
                IsStreamingPreview = false
            };
            history.Add(agentTurn);
            progress?.Report(agentTurn);
            lastSpoke[speaker.PersonaId] = turn;

            // 슬라이딩 윈도우 조기 종료: 최근 N 발언이 모두 agree면 합의 완료
            stanceWindow.Enqueue(agentTurn.Stance!);
            if (stanceWindow.Count > participants.Count) stanceWindow.Dequeue();
            if (stanceWindow.Count == participants.Count && stanceWindow.All(s => s == "agree"))
                break;

            // 다음 화자 결정: 현재 화자가 next_speaker 지명 → 없으면 가장 오래 발언 안 한 참여자
            Persona? next = null;
            if (stance.HasStance && !string.IsNullOrWhiteSpace(stance.NextSpeaker))
            {
                next = participants.FirstOrDefault(p =>
                    string.Equals(p.Name, stance.NextSpeaker, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p.Label, stance.NextSpeaker, StringComparison.OrdinalIgnoreCase));
            }
            if (next == null)
                next = participants.OrderBy(p => lastSpoke[p.PersonaId]).First();
            currentSpeaker = next;
        }

        return RenderDiscussionTranscript(history, discussionId, topic);
    }

    private static string BuildDiscussionSpeakerAddendum(
        string topic, string stanceHint, List<Persona> participants,
        string currentName, int round, int totalRounds, bool isFreeMode = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine(isFreeMode
            ? "[자유 토론 모드 — 너는 팀원이다. 사용자 요청에 직접 응답하면서 동료 발언에도 반응하라]"
            : "[다자 토론 모드 — 너는 라운드 테이블 참여자다]");
        sb.AppendLine(isFreeMode ? $"사용자 요청: {topic}" : $"토론 주제: {topic}");
        if (!string.IsNullOrWhiteSpace(stanceHint))
            sb.AppendLine($"PM의 발언 가이드: {stanceHint}");
        sb.AppendLine($"진행 상황: 라운드 {round + 1}/{totalRounds}");
        sb.AppendLine("참여자: " + string.Join(", ",
            participants.Select(p => $"{p.Name}({p.Label})" + (string.Equals(p.Name, currentName, StringComparison.OrdinalIgnoreCase) ? " ← 너" : ""))));
        sb.AppendLine();
        sb.AppendLine("발언 규칙:");
        sb.AppendLine("  - 네 전문 관점에서 주제에 대한 입장을 제시한다.");
        sb.AppendLine("  - 직전 발언자들의 주장을 명시적으로 동의·반박·보완한다. (누구의 어떤 주장에 대한 것인지 밝힌다.)");
        sb.AppendLine("  - 근거 없는 동조는 금지. 네 역할의 관점에서 구체적 근거를 댄다.");
        sb.AppendLine("  - handoff·tool·pm_report·pm_intervention 블록은 사용 금지. 본문 끝에 stance 블록만 남긴다.");
        sb.AppendLine();
        sb.AppendLine("응답 형식:");
        sb.AppendLine("  <본문: 너의 주장·근거·동료 발언에 대한 반응>");
        sb.AppendLine("```stance");
        sb.AppendLine("{\"position\": \"agree|object|extend\", \"argument\": \"<한 줄 요지>\", \"next_speaker\": \"<다음에 발언할 팀원 이름 — 생략 시 자동 선택>\"}");
        sb.AppendLine("```");
        sb.AppendLine("- agree: 직전 합의안에 이견 없음");
        sb.AppendLine("- object: 명시적 반대 (반대 근거는 본문에)");
        sb.AppendLine("- extend: 보완·추가 조건 제시");
        sb.AppendLine("- next_speaker: 핑퐁 가능 — 같은 팀원을 다시 지명하거나 연속 발언도 허용됨");
        return sb.ToString().TrimEnd();
    }

    private static string BuildDiscussionSpeakerPrompt(
        string originalUserMessage, string topic, List<AgentTurn> history, string discussionId)
    {
        // 프로젝트 이력은 이번 토론 외 발언만 — 토론 발언은 별도 섹션으로 분리해 중복 컨텍스트를 줄인다
        var priorTurns = history
            .Where(t => !string.IsNullOrWhiteSpace(t.PersonaLabel) && t.DiscussionId != discussionId)
            .ToList();
        var historyText = RenderHistory(priorTurns);
        var discussionSoFar = history
            .Where(t => t.DiscussionId == discussionId)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("[원본 사용자 요청]");
        sb.AppendLine(originalUserMessage);
        sb.AppendLine();
        sb.AppendLine("[토론 주제]");
        sb.AppendLine(topic);
        sb.AppendLine();
        sb.AppendLine("[프로젝트 대화 이력]");
        sb.AppendLine(string.IsNullOrWhiteSpace(historyText) ? "(이전 대화 없음)" : historyText);
        sb.AppendLine();
        sb.AppendLine("[이번 토론의 지금까지 발언]");
        if (discussionSoFar.Count == 0)
        {
            sb.AppendLine("(네가 첫 발언자다.)");
        }
        else
        {
            foreach (var t in discussionSoFar)
            {
                sb.AppendLine($"- [{t.PersonaLabel}] (R{(t.RoundIndex ?? 0) + 1}, stance={t.Stance ?? "?"})");
                sb.AppendLine((string.IsNullOrWhiteSpace(t.Content) ? "(내용 없음)" : t.Content));
                sb.AppendLine();
            }
        }
        sb.AppendLine("이제 네 차례다. 위 규칙에 따라 발언하라.");
        return sb.ToString().TrimEnd();
    }

    private static string BuildDiscussionReviewPrompt(
        string originalUserMessage, List<AgentTurn> history, string transcript)
    {
        var priorText = RenderHistory(history.Where(t => t.DiscussionId == null).ToList());
        return
            "[컨텍스트]\n" +
            "사용자 원본 요청:\n" +
            originalUserMessage + "\n\n" +
            "이전 대화:\n" +
            (string.IsNullOrWhiteSpace(priorText) ? "(없음)" : priorText) + "\n\n" +
            "[방금 네가 개시한 다자 토론 전사]\n" +
            transcript + "\n\n" +
            "이 결과를 검토해 discussion_summary 블록으로 합의·잔여 쟁점·다음 단계를 정리하고, " +
            "이어서 handoff / pm_report / pm_intervention 중 하나로 다음 행동을 지정한다. " +
            "단, 같은 주제로 토론을 즉시 재개시(discussion)하지 않는다.";
    }

    private static string RenderDiscussionTranscript(
        List<AgentTurn> history, string discussionId, string topic)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"토론 주제: {topic}");
        sb.AppendLine();
        int? lastRound = null;
        foreach (var t in history.Where(t => t.DiscussionId == discussionId))
        {
            if (t.RoundIndex != lastRound)
            {
                sb.AppendLine($"── 라운드 {(t.RoundIndex ?? 0) + 1} ──");
                lastRound = t.RoundIndex;
            }
            sb.AppendLine($"[{t.PersonaLabel}] (stance: {t.Stance ?? "?"})");
            sb.AppendLine(string.IsNullOrWhiteSpace(t.Content) ? "(내용 없음)" : t.Content);
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    // === 자유 토론 파이프라인 (PM + 비PM 혼합 프로젝트 기본 동작) ===

    private async Task<OrchestratorOutput> RunFreeDiscussionPipelineAsync(
        OrchestratorInput input,
        List<Persona> personas,
        IProgress<AgentTurn>? progress,
        CancellationToken ct = default)
    {
        var workspaceRoot = await ResolveWorkspaceRootAsync(input.ProjectId);
        var askUserEnabled = await ResolveAskUserEnabledAsync(input);
        var (maxDiscussionRounds, maxDiscussionParticipants) = await ResolveDiscussionSettingsAsync(input.ProjectId);
        var runId = Guid.NewGuid().ToString("N")[..8];

        var pmPersona = personas.First(p => p.IsPm);
        var speakers = personas.Where(p => !p.IsPm).ToList();

        var history = new List<AgentTurn>();

        // Phase 1: 비PM 팀원들이 자유 토론
        var transcript = await RunDiscussionAsync(
            input, personas, pmPersona, askUserEnabled,
            topic: input.Message,
            stanceHint: string.Empty,
            participants: speakers,
            rounds: maxDiscussionRounds,
            history, progress, runId,
            maxParticipants: maxDiscussionParticipants,
            isFreeMode: true,
            ct: ct);

        // Phase 2: PM 한 번 — 취합만
        var pmConfig = await _configLoader.GetPersonaConfigAsync(pmPersona.PersonaId, input.ProjectId);
        var pmSystemPrompt = pmConfig.SystemPrompt + "\n\n" + BuildPmAggregateAddendum();
        var pmUserPrompt = BuildPmAggregatePrompt(input.Message, transcript);

        var streamKey = $"{runId}-pm-agg";
        var streamBuffer = new StringBuilder();
        var lastEmitTicks = 0L;
        const long EmitIntervalTicks = TimeSpan.TicksPerMillisecond * 33;

        void EmitPreview(bool force)
        {
            if (progress == null) return;
            var nowTicks = DateTime.UtcNow.Ticks;
            if (!force && nowTicks - lastEmitTicks < EmitIntervalTicks) return;
            lastEmitTicks = nowTicks;
            progress.Report(new AgentTurn
            {
                TurnIndex = history.Count,
                PersonaId = pmConfig.PersonaId,
                PersonaName = pmConfig.Name,
                PersonaLabel = pmConfig.Label,
                PersonaAvatar = pmConfig.Avatar,
                IsPm = true,
                Content = CleanStreamingPreview(streamBuffer.ToString()),
                ModelUsed = string.Empty,
                StreamKey = streamKey,
                IsStreamingPreview = true
            });
        }

        EmitPreview(force: true);

        var pmResponse = await _aiClient.ChatWithFallbackStreamAsync(
            pmConfig.PrimaryModel, pmConfig.FallbackModel,
            pmSystemPrompt, pmUserPrompt,
            pmConfig.Temperature, pmConfig.MaxTokens,
            onDelta: chunk => { streamBuffer.Append(chunk); EmitPreview(false); },
            history: input.PriorConversation,
            ct: ct);

        var pmBlock = PmBlockParser.Parse(pmResponse.Content);
        var pmEventId = Guid.NewGuid().ToString();
        var wikiParse = WikiSaveParser.Parse(pmBlock.CleanedContent);
        var pmContent = wikiParse.CleanedContent;
        if (string.IsNullOrWhiteSpace(pmContent)) pmContent = "(응답 없음)";

        foreach (var block in wikiParse.Saves)
        {
            try { await _wiki.CreateWikiAsync(input.ProjectId, block.Category, block.Title, block.Content, sourceEventId: pmEventId); }
            catch (Exception ex) { Console.Error.WriteLine($"[WikiSave] FAILED: {ex.GetType().Name}: {ex.Message}"); }
        }

        bool endReport = pmBlock.HasReport;
        var pmTurn = new AgentTurn
        {
            EventId = pmEventId,
            TurnIndex = history.Count,
            PersonaId = pmConfig.PersonaId,
            PersonaName = pmConfig.Name,
            PersonaLabel = pmConfig.Label,
            PersonaAvatar = pmConfig.Avatar,
            IsPm = true,
            Content = pmContent,
            ModelUsed = pmResponse.ModelUsed,
            IsEndReport = endReport,
            StreamKey = streamKey,
            IsStreamingPreview = false
        };
        history.Add(pmTurn);
        progress?.Report(pmTurn);

        string? outputsFolder = null, reportPath = null, commitSha = null;
        if (endReport)
        {
            try
            {
                var ctx = new PmReportContext
                {
                    WorkspaceRoot = workspaceRoot,
                    RunId = runId,
                    OriginalUserMessage = input.Message,
                    ReportSummary = pmBlock.ReportSummary,
                    ReportBody = pmBlock.ReportBody,
                    Turns = history
                        .Where(t => !string.IsNullOrWhiteSpace(t.PersonaId))
                        .Select(t => new TurnOutputRecord
                        {
                            TurnIndex = t.TurnIndex,
                            PersonaName = t.PersonaName,
                            PersonaLabel = t.PersonaLabel,
                            Content = t.Content,
                            ModelUsed = t.ModelUsed,
                            WrittenFiles = t.WrittenFiles
                        })
                        .ToList()
                };
                var aggregated = _pmReport.Aggregate(ctx);
                outputsFolder = aggregated.OutputsFolder;
                reportPath = aggregated.ReportPath;
                commitSha = aggregated.CommitSha;
            }
            catch { }
        }

        var eventId = await LogEventsAsync(input, history, runId, endReport, false, outputsFolder, commitSha, reportPath);

        return new OrchestratorOutput
        {
            EventId = eventId,
            RunId = runId,
            PersonaId = pmTurn.PersonaId,
            PersonaLabel = pmTurn.PersonaLabel,
            PersonaAvatar = pmTurn.PersonaAvatar,
            Content = pmTurn.Content,
            ModelUsed = pmTurn.ModelUsed,
            Verified = true,
            Turns = history,
            IsEndReport = endReport,
            OutputsFolder = outputsFolder,
            ReportPath = reportPath,
            CommitSha = commitSha
        };
    }

    private static string BuildPmAggregateAddendum()
    {
        var sb = new StringBuilder();
        sb.AppendLine("[취합 모드]");
        sb.AppendLine("팀원들이 자유 토론을 마쳤다. 너는 결과를 취합·정리하는 역할만 한다.");
        sb.AppendLine("규칙:");
        sb.AppendLine("  - handoff·discussion 블록 사용 금지.");
        sb.AppendLine("  - 각 팀원의 주장을 중립적으로 요약한다.");
        sb.AppendLine("  - 합의 사항, 잔여 쟁점, 권고 사항을 구분해서 제시한다.");
        sb.AppendLine("  - 산출물이 있으면 pm_report 블록으로 종료. 추가 확인이 필요하면 pm_intervention 블록 사용.");
        return sb.ToString().TrimEnd();
    }

    private static string BuildPmAggregatePrompt(string userMessage, string transcript)
    {
        return
            "[사용자 요청]\n" +
            userMessage + "\n\n" +
            "[팀 토론 전사]\n" +
            transcript + "\n\n" +
            "위 토론 결과를 취합하여 사용자에게 응답하라.";
    }

    // === 팀 파이프라인 (멀티에이전트) ===

    private async Task<OrchestratorOutput> RunTeamPipelineAsync(
        OrchestratorInput input,
        IProgress<AgentTurn>? progress,
        CancellationToken ct = default)
    {
        var allPersonas = await _configLoader.ListPersonasAsync(input.ProjectId);
        var workspaceRoot = await ResolveWorkspaceRootAsync(input.ProjectId);
        var askUserEnabled = await ResolveAskUserEnabledAsync(input);
        var runId = Guid.NewGuid().ToString("N")[..8];
        var mode = (input.TeamMode ?? "panel").ToLowerInvariant();

        var teamPersonas = input.TeamPersonaIds!
            .Select(id => allPersonas.FirstOrDefault(p => p.PersonaId == id))
            .Where(p => p != null)
            .Cast<Persona>()
            .ToList();

        if (teamPersonas.Count < 2)
            throw new InvalidOperationException("팀 모드에는 최소 2개의 페르소나가 필요합니다.");

        var history = new List<AgentTurn>();

        switch (mode)
        {
            case "debate":
            {
                var topic = input.Message.Length > 120 ? input.Message[..120] + "…" : input.Message;
                var (dMaxRounds, dMaxParticipants) = await ResolveDiscussionSettingsAsync(input.ProjectId);
                await RunDiscussionAsync(
                    input, allPersonas, teamPersonas[0], askUserEnabled,
                    topic, string.Empty,
                    teamPersonas, dMaxRounds, history, progress, runId,
                    maxParticipants: dMaxParticipants,
                    ct: ct);
                break;
            }
            case "chain":
                await RunChainAsync(input, teamPersonas, workspaceRoot, history, progress, runId, ct);
                break;
            default:
                await RunPanelAsync(input, teamPersonas, workspaceRoot, history, progress, runId, ct);
                break;
        }

        if (history.Count == 0)
            throw new InvalidOperationException("팀 파이프라인이 응답을 생성하지 못했습니다.");

        var eventId = await LogEventsAsync(input, history, runId, false, false, null, null, null);
        var lastTurn = history[^1];

        return new OrchestratorOutput
        {
            EventId = eventId,
            RunId = runId,
            PersonaId = lastTurn.PersonaId,
            PersonaLabel = lastTurn.PersonaLabel,
            PersonaAvatar = lastTurn.PersonaAvatar,
            Content = lastTurn.Content,
            ModelUsed = lastTurn.ModelUsed,
            Verified = true,
            Turns = history
        };
    }

    private async Task RunPanelAsync(
        OrchestratorInput input,
        List<Persona> teamPersonas,
        string workspaceRoot,
        List<AgentTurn> history,
        IProgress<AgentTurn>? progress,
        string runId,
        CancellationToken ct = default)
    {
        for (int i = 0; i < teamPersonas.Count; i++)
        {
            var persona = teamPersonas[i];
            var config = await _configLoader.GetPersonaConfigAsync(persona.PersonaId, input.ProjectId);
            var streamKey = $"{runId}-panel-{i}";

            var addendum = BuildTeamModeAddendum(teamPersonas, config.Name, workspaceRoot, "panel");
            var systemPrompt = string.IsNullOrWhiteSpace(addendum)
                ? config.SystemPrompt
                : config.SystemPrompt + "\n\n" + addendum;

            var userPrompt = await _contextInjector.InjectAsync(
                input.Message, input.ProjectId, config.Name, 1.0);

            var streamBuffer = new StringBuilder();
            long lastEmitTicks = 0L;
            const long EmitIntervalTicks = TimeSpan.TicksPerMillisecond * 33;
            var turnIdx = i;

            void EmitPreview(bool force)
            {
                if (progress == null) return;
                var nowTicks = DateTime.UtcNow.Ticks;
                if (!force && nowTicks - lastEmitTicks < EmitIntervalTicks) return;
                lastEmitTicks = nowTicks;
                progress.Report(new AgentTurn
                {
                    TurnIndex = turnIdx,
                    PersonaId = config.PersonaId,
                    PersonaName = config.Name,
                    PersonaLabel = config.Label,
                    PersonaAvatar = config.Avatar,
                    Content = CleanStreamingPreview(streamBuffer.ToString()),
                    StreamKey = streamKey,
                    IsStreamingPreview = true
                });
            }

            EmitPreview(force: true);

            var response = await _aiClient.ChatWithFallbackStreamAsync(
                config.PrimaryModel, config.FallbackModel,
                systemPrompt, userPrompt,
                config.Temperature, config.MaxTokens,
                onDelta: chunk => { streamBuffer.Append(chunk); EmitPreview(force: false); },
                history: input.PriorConversation,
                ct: ct);

            var eventId = Guid.NewGuid().ToString();
            var wikiParse = WikiSaveParser.Parse(response.Content);
            var content = wikiParse.CleanedContent;
            foreach (var block in wikiParse.Saves)
            {
                try { await _wiki.CreateWikiAsync(input.ProjectId, block.Category, block.Title, block.Content, sourceEventId: eventId); }
                catch { }
            }

            var turn = new AgentTurn
            {
                EventId = eventId,
                TurnIndex = i,
                PersonaId = config.PersonaId,
                PersonaName = config.Name,
                PersonaLabel = config.Label,
                PersonaAvatar = config.Avatar,
                Content = string.IsNullOrWhiteSpace(content) ? "(응답 없음)" : content,
                ModelUsed = response.ModelUsed,
                StreamKey = streamKey,
                IsStreamingPreview = false
            };
            history.Add(turn);
            progress?.Report(turn);
        }
    }

    private async Task RunChainAsync(
        OrchestratorInput input,
        List<Persona> teamPersonas,
        string workspaceRoot,
        List<AgentTurn> history,
        IProgress<AgentTurn>? progress,
        string runId,
        CancellationToken ct = default)
    {
        for (int i = 0; i < teamPersonas.Count; i++)
        {
            var persona = teamPersonas[i];
            var config = await _configLoader.GetPersonaConfigAsync(persona.PersonaId, input.ProjectId);
            var streamKey = $"{runId}-chain-{i}";

            var addendum = BuildTeamModeAddendum(teamPersonas, config.Name, workspaceRoot, "chain");
            var systemPrompt = string.IsNullOrWhiteSpace(addendum)
                ? config.SystemPrompt
                : config.SystemPrompt + "\n\n" + addendum;

            var userPrompt = i == 0
                ? await _contextInjector.InjectAsync(input.Message, input.ProjectId, config.Name, 1.0)
                : BuildChainContextPrompt(input.Message, history, config.Label, i);

            var streamBuffer = new StringBuilder();
            long lastEmitTicks = 0L;
            const long EmitIntervalTicks = TimeSpan.TicksPerMillisecond * 33;
            var turnIdx = i;

            void EmitPreview(bool force)
            {
                if (progress == null) return;
                var nowTicks = DateTime.UtcNow.Ticks;
                if (!force && nowTicks - lastEmitTicks < EmitIntervalTicks) return;
                lastEmitTicks = nowTicks;
                progress.Report(new AgentTurn
                {
                    TurnIndex = turnIdx,
                    PersonaId = config.PersonaId,
                    PersonaName = config.Name,
                    PersonaLabel = config.Label,
                    PersonaAvatar = config.Avatar,
                    Content = CleanStreamingPreview(streamBuffer.ToString()),
                    StreamKey = streamKey,
                    IsStreamingPreview = true
                });
            }

            EmitPreview(force: true);

            var response = await _aiClient.ChatWithFallbackStreamAsync(
                config.PrimaryModel, config.FallbackModel,
                systemPrompt, userPrompt,
                config.Temperature, config.MaxTokens,
                onDelta: chunk => { streamBuffer.Append(chunk); EmitPreview(force: false); },
                history: i == 0 ? input.PriorConversation : null,
                ct: ct);

            var eventId = Guid.NewGuid().ToString();
            var wikiParse = WikiSaveParser.Parse(response.Content);
            var content = wikiParse.CleanedContent;
            foreach (var block in wikiParse.Saves)
            {
                try { await _wiki.CreateWikiAsync(input.ProjectId, block.Category, block.Title, block.Content, sourceEventId: eventId); }
                catch { }
            }

            var turn = new AgentTurn
            {
                EventId = eventId,
                TurnIndex = i,
                PersonaId = config.PersonaId,
                PersonaName = config.Name,
                PersonaLabel = config.Label,
                PersonaAvatar = config.Avatar,
                Content = string.IsNullOrWhiteSpace(content) ? "(응답 없음)" : content,
                ModelUsed = response.ModelUsed,
                StreamKey = streamKey,
                IsStreamingPreview = false
            };
            history.Add(turn);
            progress?.Report(turn);
        }
    }

    private static string BuildTeamModeAddendum(
        List<Persona> teamPersonas, string currentName, string workspaceRoot, string mode)
    {
        var sb = new StringBuilder();

        if (mode == "panel")
        {
            sb.AppendLine("[팀 패널 모드]");
            sb.AppendLine("사용자가 여러 에이전트에게 동시에 독립 응답을 요청했다.");
            sb.AppendLine("다른 에이전트의 응답을 알 수 없으므로 네 전문 영역에서 독립적으로 최선의 응답을 작성한다.");
            sb.AppendLine("handoff / pm_report / pm_intervention 블록은 사용하지 않는다.");
        }
        else // chain
        {
            sb.AppendLine("[팀 체인 모드]");
            sb.AppendLine("사용자가 여러 에이전트에게 순차적으로 작업을 이어받도록 요청했다.");
            sb.AppendLine("이전 에이전트의 결과를 이어받아 네 전문 영역에서 추가 기여한다.");
            sb.AppendLine("이미 충분히 다뤄진 내용은 반복하지 않고 네 역할에서 새로 추가할 내용에 집중한다.");
            sb.AppendLine("handoff / pm_report / pm_intervention 블록은 사용하지 않는다.");
        }

        sb.AppendLine("참여 팀원: " + string.Join(", ",
            teamPersonas.Select(p =>
                $"{p.Name}({p.Label})" +
                (string.Equals(p.Name, currentName, StringComparison.OrdinalIgnoreCase) ? " ← 너" : ""))));
        sb.AppendLine();
        sb.AppendLine("[도구 사용]");
        sb.AppendLine($"작업 폴더(workspace root): {workspaceRoot}");
        sb.AppendLine("  - read_file(path) / list_dir(path?) / search_files(pattern, path?, include?)");
        sb.AppendLine("  - write_file(path, content) — 신규 파일 전체 작성");
        sb.AppendLine("  - edit_file(path, old_text, new_text) — 기존 파일 부분 교체 (old_text는 파일 내 1곳만 존재해야 함)");
        sb.AppendLine("  - append_file(path, content) / make_dir(path) / delete_file(path)");
        sb.AppendLine("  - run_command(command) — 작업 폴더 기준 셸 명령 실행 (빌드·테스트·git). 타임아웃 60초.");
        sb.AppendLine("  - generate_image(prompt, path, size?, quality?) — DALL-E 3 이미지 생성 후 파일 저장. OPENAI 키 필요.");
        sb.AppendLine("```tool");
        sb.AppendLine("{\"name\": \"<도구명>\", \"args\": {<인자>}}");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("[위키 승격]");
        sb.AppendLine("나중에 참조할 가치가 있는 의사결정·명세·트러블슈팅은 wiki_save 블록으로 저장한다.");
        sb.AppendLine("```wiki_save");
        sb.AppendLine("{\"category\": \"WIKI_ADR|WIKI_SPEC|WIKI_TROUBLE\", \"title\": \"제목\", \"content\": \"내용\"}");
        sb.AppendLine("```");

        return sb.ToString().TrimEnd();
    }

    private static string BuildChainContextPrompt(
        string originalMessage, List<AgentTurn> priorTurns, string currentLabel, int step)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[원본 사용자 요청]");
        sb.AppendLine(originalMessage);
        sb.AppendLine();
        sb.AppendLine($"[이전 에이전트 응답 — {step}단계 누적]");
        foreach (var t in priorTurns)
        {
            sb.AppendLine($"── {t.PersonaLabel} ──");
            sb.AppendLine(string.IsNullOrWhiteSpace(t.Content) ? "(응답 없음)" : t.Content);
            sb.AppendLine();
        }
        sb.AppendLine($"[{currentLabel}(너)의 차례 — 단계 {step + 1}]");
        sb.AppendLine("이전 에이전트들의 작업을 이어받아 네 전문 역할에서 추가 기여한다.");
        return sb.ToString().TrimEnd();
    }

    private static void AppendWikiSaveProtocol(StringBuilder sb)
    {
        sb.AppendLine("[위키 승격 프로토콜 — wiki_save]");
        sb.AppendLine("대화 중 발생한 의사결정·명세·트러블슈팅 중 **나중에 채팅을 뒤지지 않고도 조회해야 할 가치가 있는** 내용은");
        sb.AppendLine("아래 블록을 응답 본문 끝에 추가해 즉시 프로젝트 위키로 승격한다. 한 응답에 여러 블록을 쓸 수 있다.");
        sb.AppendLine("```wiki_save");
        sb.AppendLine("{\"category\": \"WIKI_ADR|WIKI_SPEC|WIKI_TROUBLE\", \"title\": \"<간결한 제목>\", \"content\": \"<마크다운 본문 — 배경·결정·근거·영향 순으로 자기충족적으로 기록>\"}");
        sb.AppendLine("```");
        sb.AppendLine("- WIKI_ADR : 아키텍처·정책·워크플로 결정 (이유·대안·트레이드오프 포함)");
        sb.AppendLine("- WIKI_SPEC : 기능·API·데이터 스키마 명세");
        sb.AppendLine("- WIKI_TROUBLE : 재발 가능성 있는 문제의 증상·원인·해결 경로");
        sb.AppendLine("- 단순 상태 보고·짧은 대화·추측은 위키로 저장하지 않는다. 6개월 뒤 다시 꺼낼 가치가 있어야 한다.");
        sb.AppendLine("- 블록은 UI 본문에서 자동 제거되므로 사용자에게 설명하듯 자연스러운 톤으로 기록한다.");
        sb.AppendLine();
    }

    // 스트리밍 프리뷰에서 내부용 펜스 블록(tool/handoff/pm_*)은 UI에 노출하지 않는다
    private static readonly string[] InternalFenceMarkers =
    [
        "```tool",
        "```handoff",
        "```pm_report",
        "```pm_intervention",
        "```discussion",
        "```discussion_summary",
        "```stance",
        "```wiki_save"
    ];

    private static string CleanStreamingPreview(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        int earliest = raw.Length;
        foreach (var m in InternalFenceMarkers)
        {
            int idx = raw.IndexOf(m, StringComparison.Ordinal);
            if (idx >= 0 && idx < earliest) earliest = idx;
        }
        return earliest < raw.Length ? raw[..earliest].TrimEnd() : raw;
    }

    private static bool IsFileWritingTool(string toolName)
    {
        var n = toolName?.ToLowerInvariant();
        return n is "write_file" or "append_file" or "edit_file" or "generate_image";
    }

    private static string? GetStringArg(Dictionary<string, object?> args, string key)
    {
        if (!args.TryGetValue(key, out var val) || val == null) return null;
        return val.ToString();
    }

    private static string BuildAutoReturnRequest(string fromLabel, string body, List<string> writtenFiles)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[{fromLabel}] 역할의 작업이 완료되어 당신(PM)에게 자동 복귀했다.");
        sb.AppendLine();
        sb.AppendLine("역할 응답 본문:");
        sb.AppendLine(string.IsNullOrWhiteSpace(body) ? "(본문 없음)" : body.Trim());
        if (writtenFiles.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("이번 턴에서 생성·수정된 파일:");
            foreach (var f in writtenFiles.Distinct())
                sb.AppendLine($"- {f}");
        }
        sb.AppendLine();
        sb.AppendLine("다음 행동 주체를 결정한다:");
        sb.AppendLine("1) 다음 역할에게 이어서 지시한다 → handoff 블록");
        sb.AppendLine("2) User 개입이 필요하다 → pm_intervention 블록");
        sb.AppendLine("3) 프로젝트가 완료되었다 → pm_report 블록");
        return sb.ToString().TrimEnd();
    }

    private async Task<string> ResolveWorkspaceRootAsync(string projectId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var project = await db.Projects.FindAsync(projectId);
        if (project != null && !string.IsNullOrWhiteSpace(project.GitRepoPath))
            return project.GitRepoPath;

        var fallback = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AgentPaw", "repos", projectId);
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    private async Task<bool> ResolveAskUserEnabledAsync(OrchestratorInput input)
    {
        if (input.AskUserEnabled.HasValue) return input.AskUserEnabled.Value;
        await using var db = await _dbFactory.CreateDbContextAsync();
        var project = await db.Projects.FindAsync(input.ProjectId);
        return project?.AskUserEnabled ?? true;
    }

    private async Task<(int maxRounds, int maxParticipants)> ResolveDiscussionSettingsAsync(string projectId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var project = await db.Projects.FindAsync(projectId);
        return (
            project?.MaxDiscussionRounds       ?? DefaultMaxDiscussionRounds,
            project?.MaxDiscussionParticipants ?? DefaultMaxDiscussionParticipants
        );
    }

    private static string BuildProtocolAddendum(
        List<Persona> personas,
        string currentPersonaName,
        string workspaceRoot,
        bool isCurrentPm,
        Persona? pmPersona,
        bool askUserEnabled,
        int maxRounds = DefaultMaxDiscussionRounds,
        int maxParticipants = DefaultMaxDiscussionParticipants)
    {
        var sb = new StringBuilder();

        // === Handoff ===
        var others = personas
            .Where(p => !string.Equals(p.Name, currentPersonaName, StringComparison.OrdinalIgnoreCase))
            .Select(p =>
            {
                var desc = string.IsNullOrWhiteSpace(p.Description) ? "" : $" — {p.Description}";
                var pmTag = p.IsPm ? " [PM]" : "";
                return $"  - {p.Name} ({p.Label}){pmTag}{desc}";
            })
            .ToList();

        if (isCurrentPm)
        {
            sb.AppendLine("[PM 허브 프로토콜]");
            sb.AppendLine("너는 프로젝트 관리자(PM)다. User의 모든 지시를 최우선으로 수신하여 의도를 해석하고,");
            sb.AppendLine("작업을 수행할 동료 페르소나를 선택하여 handoff 블록으로 위임한다.");
            sb.AppendLine("역할 페르소나의 산출물이 자동 복귀로 돌아오면 검토 후 다음 중 하나를 선택한다:");
            sb.AppendLine("  (1) 다음 역할에게 이어서 지시 — handoff 블록");
            sb.AppendLine("  (2) 2명 이상 역할의 의견 교환이 필요한 쟁점 — discussion 블록(다자 토론 개시)");
            int opt = 3;
            if (askUserEnabled)
            {
                sb.AppendLine($"  ({opt++}) User 개입이 꼭 필요한 결정 요청 — pm_intervention 블록 (최소화)");
            }
            sb.AppendLine($"  ({opt}) 프로젝트 전체 완료 — pm_report 블록");
            sb.AppendLine();
            sb.AppendLine("단독으로 최종 답을 돌려주지 않는다. 반드시 위 블록 중 하나로 다음 행동 주체를 지정한다.");
            sb.AppendLine();
            sb.AppendLine("[재질의 게이팅]");
            if (askUserEnabled)
            {
                sb.AppendLine("User 개입(pm_intervention)은 네가 전권을 가지고 판단하여 결정할 수 있는 사안이라면 절대 쓰지 않는다.");
                sb.AppendLine("아래 모든 조건을 만족할 때만 pm_intervention을 허용한다:");
                sb.AppendLine("  - 자율 판단이 프로젝트 방향·안전·법적 책임 관점에서 위험하거나 불가능하다");
                sb.AppendLine("  - 동료 역할에게 handoff하여 해결할 수 없다");
                sb.AppendLine("  - 기본 가정으로 진행하면 되돌리기 어려운 비가역 결정을 수반한다");
                sb.AppendLine("그 외에는 합리적 기본값을 선택하여 진행하고, 그 선택의 근거를 pm_report에 남긴다.");
                sb.AppendLine("애매한 취향·세부 스타일은 PM이 결정한다. User에게 선택지를 나열하며 되묻지 않는다.");
            }
            else
            {
                sb.AppendLine("이 프로젝트는 '사용자에게 묻기'가 비활성 상태다. 어떤 경우에도 pm_intervention 블록을 생성하지 않는다.");
                sb.AppendLine("모호하거나 정보가 부족한 상황에서도 가장 합리적인 기본값을 스스로 선택하여 진행한다.");
                sb.AppendLine("선택의 근거·가정·대안은 pm_report 본문에 명시한다. User에게 되묻는 시도 자체가 금지된다.");
            }
            sb.AppendLine();
            if (others.Count > 0)
            {
                sb.AppendLine("위임 가능한 동료 페르소나:");
                sb.AppendLine(string.Join("\n", others));
                sb.AppendLine();
            }
            sb.AppendLine("위임 형식 (handoff):");
            sb.AppendLine("```handoff");
            sb.AppendLine("{\"to\": \"<페르소나 name>\", \"request\": \"<자기충족적 요청>\"}");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine($"다자 토론 개시 형식 (discussion) — 참여자 2~{maxParticipants}명, rounds 1~{maxRounds}:");
            sb.AppendLine("```discussion");
            sb.AppendLine("{\"topic\": \"<토론 주제 1~2문장>\", \"participants\": [\"<name1>\",\"<name2>\"], \"rounds\": 2, \"stance_hint\": \"<각자 어떤 관점으로 발언할지 힌트>\"}");
            sb.AppendLine("```");
            sb.AppendLine("- 참여자는 name(괄호 앞 식별자)만 나열한다. 자신(PM)은 참여자에 포함하지 않는다.");
            sb.AppendLine("- 토론 후 너는 discussion_summary 블록으로 합의·잔여 쟁점·다음 단계를 정리한 뒤, handoff 또는 pm_report를 이어낸다.");
            sb.AppendLine();
            sb.AppendLine("토론 종료 후 정리 형식 (discussion_summary):");
            sb.AppendLine("```discussion_summary");
            sb.AppendLine("{\"consensus\": \"<합의 내용>\", \"disagreements\": \"<잔여 쟁점>\", \"next_step\": \"<다음 행동>\"}");
            sb.AppendLine("```");
            sb.AppendLine();
            if (askUserEnabled)
            {
                sb.AppendLine("User 개입 요청 (위 게이팅 조건을 만족할 때만):");
                sb.AppendLine("```pm_intervention");
                sb.AppendLine("{\"reason\": \"<개입이 필요한 이유>\", \"question\": \"<User에게 묻는 구체적 질문>\"}");
                sb.AppendLine("```");
                sb.AppendLine();
            }
            sb.AppendLine("프로젝트 종료 보고 (모든 작업 완료 후):");
            sb.AppendLine("```pm_report");
            sb.AppendLine("{\"summary\": \"<1~2문장 요약>\", \"body\": \"<상세 보고 — 역할별 산출물·이슈·다음 단계·자율 결정 근거>\"}");
            sb.AppendLine("```");
            sb.AppendLine("- pm_report를 낸 시점에 시스템이 산출물을 outputs/<stamp>-<run_id>/ 아래로 취합하고 Git 커밋한다.");
            sb.AppendLine();
            AppendWikiSaveProtocol(sb);
        }
        else if (others.Count > 0)
        {
            sb.AppendLine("[팀 협업 프로토콜]");
            sb.AppendLine("너는 혼자가 아니다. 작업은 PM 허브 모델로 진행된다:");
            sb.AppendLine("  - 작업을 완료하면 네 응답은 자동으로 PM에게 복귀한다.");
            sb.AppendLine("  - 다른 역할의 전문성이 선행되어야 할 때만 handoff 블록으로 해당 역할에게 요청한다.");
            sb.AppendLine("  - pm_report / pm_intervention 블록은 PM 전용이므로 사용하지 않는다.");
            sb.AppendLine("  - User에게 직접 재질의하지 않는다. 정보가 부족하면 합리적 기본값으로 진행하거나, 꼭 필요하면 PM에게 handoff로 판단을 요청한다.");
            sb.AppendLine();
            AppendWikiSaveProtocol(sb);
            sb.AppendLine("동료 페르소나 목록:");
            sb.AppendLine(string.Join("\n", others));
            sb.AppendLine();
            sb.AppendLine("선행 작업 요청이 필요할 때만 다음 형식을 사용한다:");
            sb.AppendLine("```handoff");
            sb.AppendLine("{\"to\": \"<페르소나 name>\", \"request\": \"<구체적 요청>\"}");
            sb.AppendLine("```");
            sb.AppendLine("- to 값은 위 목록의 name(괄호 앞 식별자)을 정확히 사용한다.");
            sb.AppendLine("- 최종 완료 응답에는 handoff 블록을 넣지 않는다(자동으로 PM에게 복귀).");
            sb.AppendLine();
        }

        // === Tools ===
        sb.AppendLine("[도구 사용 프로토콜]");
        sb.AppendLine($"작업 폴더(workspace root): {workspaceRoot}");
        sb.AppendLine("모든 파일 경로는 이 폴더 기준 상대 경로를 사용한다. 절대 경로와 `..` 탈출은 거부된다.");
        sb.AppendLine();
        sb.AppendLine("사용 가능한 도구:");
        sb.AppendLine("  [파일 읽기/탐색]");
        sb.AppendLine("  - read_file(path): 파일 내용 읽기.");
        sb.AppendLine("  - list_dir(path?): 폴더 목록. path 생략 시 루트.");
        sb.AppendLine("  - search_files(pattern, path?, include?): 텍스트 패턴으로 파일 검색.");
        sb.AppendLine("      path: 검색 루트 (생략 시 전체). include: 파일 패턴 (예: \"*.cs\", \"*.ts\").");
        sb.AppendLine("  [파일 쓰기]");
        sb.AppendLine("  - write_file(path, content): 파일 생성/전체 덮어쓰기. 상위 폴더 자동 생성.");
        sb.AppendLine("  - edit_file(path, old_text, new_text): 파일에서 old_text를 찾아 new_text로 교체.");
        sb.AppendLine("      old_text는 파일에 정확히 1곳만 존재해야 한다. 기존 파일 부분 수정 시 이 도구를 사용한다.");
        sb.AppendLine("  - append_file(path, content): 파일 끝에 내용 추가.");
        sb.AppendLine("  - make_dir(path): 폴더 생성.");
        sb.AppendLine("  - delete_file(path): 파일을 .trash/<timestamp>/ 로 이동 (복구 가능).");
        sb.AppendLine("  [명령 실행]");
        sb.AppendLine("  - run_command(command): 작업 폴더 기준으로 셸 명령 실행. 타임아웃 60초.");
        sb.AppendLine("      빌드·테스트·git 조회·패키지 설치 등 개발 루프에 활용한다.");
        sb.AppendLine("      예: run_command({\"command\": \"dotnet build\"})");
        sb.AppendLine("  [AI 이미지 생성]");
        sb.AppendLine("  - generate_image(prompt, path, size?, quality?): DALL-E 3로 이미지를 생성하여 파일로 저장.");
        sb.AppendLine("      prompt: 이미지 설명 (영문 권장). path: 저장 경로 (예: assets/logo.png).");
        sb.AppendLine("      size: \"1024x1024\"(기본) | \"1792x1024\" | \"1024x1792\"");
        sb.AppendLine("      quality: \"standard\"(기본) | \"hd\"");
        sb.AppendLine("      설정 > API 키에서 OPENAI 키가 등록되어 있어야 한다.");
        sb.AppendLine();
        sb.AppendLine("도구 호출 형식 (필요할 때만 사용, 한 응답에 여러 개 가능):");
        sb.AppendLine("```tool");
        sb.AppendLine("{\"name\": \"edit_file\", \"args\": {\"path\": \"src/Foo.cs\", \"old_text\": \"기존 코드\", \"new_text\": \"새 코드\"}}");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("규칙:");
        sb.AppendLine("- 도구 호출이 있으면 시스템이 실행 후 결과를 돌려주며, 그때 다시 응답하여 작업을 이어간다.");
        sb.AppendLine("- 같은 응답에 tool과 handoff를 함께 쓰지 않는다. tool이 있으면 tool이 우선이다.");
        sb.AppendLine("- 파일을 새로 만들 때는 write_file에 전체 내용을 완성된 형태로 작성한다.");
        sb.AppendLine("- 기존 파일을 수정할 때는 edit_file로 최소한의 범위만 교체한다 (불필요한 전체 덮어쓰기 금지).");
        sb.AppendLine("- run_command 실행 후 오류가 있으면 원인을 분석하고 수정하여 다시 실행한다.");
        sb.AppendLine("- 사용자 요청을 완료했으면 도구 없이 평문으로 요약해 마무리한다.");

        return sb.ToString().TrimEnd();
    }

    private static string BuildTeamContextPrompt(
        string originalUserMessage, List<AgentTurn> history, string fromLabel, string requestToMe)
    {
        var historyText = RenderHistory(history);
        return
            "[팀 협업 컨텍스트]\n" +
            "사용자가 팀에게 요청한 원본 메시지:\n" +
            originalUserMessage + "\n\n" +
            "지금까지 팀에서 진행된 대화:\n" +
            historyText + "\n\n" +
            $"[지금 당신의 차례 — '{fromLabel}'로부터 받은 요청]\n" +
            requestToMe + "\n\n" +
            "당신의 역할에 맞게 응답하고, 필요하면 tool/handoff를 사용한다.";
    }

    private static string BuildToolFeedbackPrompt(
        string originalUserMessage, List<AgentTurn> history, string toolResults)
    {
        var historyText = RenderHistory(history);
        return
            "[컨텍스트]\n" +
            "사용자 원본 요청:\n" +
            originalUserMessage + "\n\n" +
            "지금까지의 대화:\n" +
            historyText + "\n\n" +
            "[방금 네가 호출한 도구의 실행 결과]\n" +
            toolResults + "\n\n" +
            "이 결과를 반영해 작업을 이어간다. 추가 도구가 필요하면 호출하고, 작업이 끝났으면 최종 응답 또는 handoff로 마무리한다.";
    }

    private static string RenderHistory(List<AgentTurn> history)
    {
        var parts = new List<string>();
        foreach (var h in history)
        {
            var body = string.IsNullOrWhiteSpace(h.Content) ? "(내용 없음)" : h.Content;
            var sb = new StringBuilder();
            sb.Append($"[{h.PersonaLabel}]\n{body}");

            if (h.ToolCalls.Count > 0)
            {
                sb.Append("\n  (도구 호출: ");
                sb.Append(string.Join(", ", h.ToolCalls.Select(t =>
                    $"{t.Name}({t.ArgsSummary}) → {(t.Success ? "ok" : "fail")}")));
                sb.Append(')');
            }
            parts.Add(sb.ToString());
        }
        return string.Join("\n\n", parts);
    }

    private static string BuildToolResultsText(List<ToolCallRecord> records)
    {
        var sb = new StringBuilder();
        foreach (var r in records)
        {
            sb.AppendLine($"▸ {r.Name}({r.ArgsSummary}) — {(r.Success ? "성공" : "실패")}");
            sb.AppendLine(r.Result);
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    private static string SummarizeArgs(Dictionary<string, object?> args)
    {
        var parts = new List<string>();
        foreach (var kv in args)
        {
            var v = kv.Value?.ToString() ?? "";
            if (kv.Key.Equals("content", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add($"{kv.Key}=<{System.Text.Encoding.UTF8.GetByteCount(v)}B>");
            }
            else
            {
                var trimmed = v.Length > 60 ? v[..60] + "…" : v;
                parts.Add($"{kv.Key}={trimmed}");
            }
        }
        return string.Join(", ", parts);
    }

    private async Task<string> LogEventsAsync(
        OrchestratorInput input,
        List<AgentTurn> turns,
        string runId,
        bool endReport,
        bool userIntervention,
        string? outputsFolder,
        string? commitSha,
        string? reportPath = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var userEventId = Guid.NewGuid().ToString();
        db.EventLogs.Add(new EventLog
        {
            EventId = userEventId,
            ProjectId = input.ProjectId,
            EventType = "USER_MESSAGE",
            Payload = JsonSerializer.Serialize(new
            {
                runId,
                message = input.Message,
                personaId = turns.FirstOrDefault()?.PersonaId
            }),
            TriggeredBy = input.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        });

        var lastEventId = userEventId;
        foreach (var turn in turns)
        {
            // 턴 생성 시 미리 발급된 EventId 를 그대로 사용한다 — Wiki sourceEventId 등 외부 참조와 일치시킨다.
            var aiEventId = string.IsNullOrEmpty(turn.EventId) ? Guid.NewGuid().ToString() : turn.EventId;
            db.EventLogs.Add(new EventLog
            {
                EventId = aiEventId,
                ProjectId = input.ProjectId,
                EventType = turn.IsPm ? "PM_RESPONSE" : "AI_RESPONSE",
                Payload = JsonSerializer.Serialize(new
                {
                    runId,
                    content = turn.Content,
                    personaId = turn.PersonaId,
                    personaName = turn.PersonaName,
                    isPm = turn.IsPm,
                    turnIndex = turn.TurnIndex,
                    handoffTo = turn.HandoffToName,
                    handoffRequest = turn.HandoffRequest,
                    writtenFiles = turn.WrittenFiles,
                    isEndReport = turn.IsEndReport,
                    isUserIntervention = turn.IsUserIntervention,
                    discussionId = turn.DiscussionId,
                    roundIndex = turn.RoundIndex,
                    speakerOrder = turn.SpeakerOrder,
                    stance = turn.Stance,
                    isDiscussionOpener = turn.IsDiscussionOpener,
                    isDiscussionSpeaker = turn.IsDiscussionSpeaker,
                    isDiscussionSummary = turn.IsDiscussionSummary,
                    discussionTopic = turn.DiscussionTopic,
                    toolCalls = turn.ToolCalls.Select(t => new
                    {
                        name = t.Name,
                        args = t.ArgsSummary,
                        success = t.Success,
                        result = t.Result
                    })
                }),
                ModelUsed = turn.ModelUsed,
                TriggeredBy = input.UserId,
                CreatedAt = DateTimeOffset.UtcNow
            });
            lastEventId = aiEventId;
        }

        if (endReport || userIntervention)
        {
            var pmEventId = Guid.NewGuid().ToString();
            db.EventLogs.Add(new EventLog
            {
                EventId = pmEventId,
                ProjectId = input.ProjectId,
                EventType = endReport ? "PM_REPORT" : "PM_INTERVENTION",
                Payload = JsonSerializer.Serialize(new
                {
                    runId,
                    outputsFolder,
                    commitSha,
                    reportPath
                }),
                TriggeredBy = input.UserId,
                CreatedAt = DateTimeOffset.UtcNow
            });
            lastEventId = pmEventId;
        }

        await db.SaveChangesAsync();
        return lastEventId;
    }

    public async Task<List<WikiDocument>> ConsolidateWikiAsync(string projectId)
    {
        // Load last 200 conversation events
        await using var db = await _dbFactory.CreateDbContextAsync();
        var events = await db.EventLogs.AsNoTracking()
            .Where(e => e.ProjectId == projectId && !e.IsDeleted
                && (e.EventType == "USER_MESSAGE" || e.EventType == "AI_RESPONSE"
                    || e.EventType == "PM_RESPONSE" || e.EventType == "PM_REPORT"
                    || e.EventType == "PM_INTERVENTION"))
            .OrderBy(e => e.CreatedAt)
            .Take(200)
            .ToListAsync();

        if (events.Count == 0) return [];

        // Build conversation transcript
        var sb = new System.Text.StringBuilder();
        foreach (var e in events)
        {
            var sender = e.EventType == "USER_MESSAGE" ? "User" : "Agent";
            string content = "";
            try
            {
                using var doc = JsonDocument.Parse(e.Payload);
                var root = doc.RootElement;
                content = (root.TryGetProperty("message", out var msg) ? msg.GetString() : null)
                       ?? (root.TryGetProperty("content", out var cnt) ? cnt.GetString() : null)
                       ?? (root.TryGetProperty("text", out var txt) ? txt.GetString() : null)
                       ?? "";
            }
            catch { }
            if (!string.IsNullOrWhiteSpace(content))
                sb.AppendLine($"[{sender}] {content}");
        }

        var transcript = sb.ToString();

        // Get a persona model to use (PM persona, or fallback to claude-sonnet-4-6)
        var personas = await _configLoader.ListPersonasAsync(projectId);
        var pm = personas.FirstOrDefault(p => p.IsPm) ?? personas.FirstOrDefault();
        var primaryModel = pm?.PrimaryModel ?? "claude-sonnet-4-6";
        var fallbackModel = pm?.FallbackModel;

        var systemPrompt = """
You are a knowledge management specialist. Analyze the conversation transcript below and extract structured knowledge into a wiki knowledge base.

Return ONLY a valid JSON array (no markdown, no explanation) with entries in this format:
[
  {
    "category": "결정사항",
    "title": "...",
    "content": "..."
  }
]

Use these categories as appropriate:
- 결정사항: Key decisions made during the conversation
- 기술명세: Technical specifications, architecture, or design decisions
- 문제해결: Problems encountered and their solutions
- 프로세스: Workflows, procedures, or process definitions

The content field should be formatted as Markdown. Be thorough but concise. Only include meaningful knowledge, not small talk or trivial exchanges. Respond in Korean.
""";

        var response = await _aiClient.ChatWithFallbackAsync(
            primaryModel,
            fallbackModel,
            systemPrompt,
            $"Conversation transcript:\n\n{transcript}",
            temperature: 0.3f,
            maxTokens: 4096
        );

        // Parse JSON response
        var created = new List<WikiDocument>();
        try
        {
            var text = response.Content.Trim();
            // Strip markdown code fences if present
            if (text.StartsWith("```")) text = text[(text.IndexOf('\n') + 1)..];
            if (text.EndsWith("```")) text = text[..text.LastIndexOf("```")].TrimEnd();

            using var doc = JsonDocument.Parse(text);
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var category = item.TryGetProperty("category", out var c) ? c.GetString() ?? "일반" : "일반";
                var title    = item.TryGetProperty("title",    out var t) ? t.GetString() ?? "" : "";
                var content  = item.TryGetProperty("content",  out var ct) ? ct.GetString() ?? "" : "";
                if (string.IsNullOrWhiteSpace(title)) continue;
                var wiki = await _wiki.CreateWikiAsync(projectId, category, title, content);
                created.Add(wiki);
            }
        }
        catch { }

        return created;
    }
}

public class OrchestratorInput
{
    public string ProjectId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ForcePersonaId { get; set; }

    /// <summary>
    /// 팀 모드: 이 목록에 PersonaId가 2개 이상이면 단일 에이전트 대신 팀 파이프라인을 실행한다.
    /// </summary>
    public List<string>? TeamPersonaIds { get; set; }

    /// <summary>panel | debate | chain — TeamPersonaIds가 있을 때만 사용.</summary>
    public string? TeamMode { get; set; }

    /// <summary>
    /// 같은 채팅 창의 이전 대화 기록. 에이전트가 세션을 이어서 인식할 수 있도록 첫 번째 AI 호출에 전달된다.
    /// </summary>
    public List<ConversationTurn>? PriorConversation { get; set; }

    /// <summary>
    /// PM이 User에게 재질의(pm_intervention)를 할 수 있는지 여부.
    /// null이면 프로젝트 설정값(Project.AskUserEnabled)을 사용한다.
    /// false면 PM은 스스로 판단하여 결정하고 절대 User에게 묻지 않는다.
    /// </summary>
    public bool? AskUserEnabled { get; set; }
}

public class OrchestratorOutput
{
    public string EventId { get; set; } = string.Empty;
    public string PersonaId { get; set; } = string.Empty;
    public string PersonaLabel { get; set; } = string.Empty;
    public string PersonaAvatar { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ModelUsed { get; set; } = string.Empty;
    public bool Verified { get; set; }
    public double Confidence { get; set; }
    public bool NeedsConfirmation { get; set; }
    public List<AgentTurn> Turns { get; set; } = [];

    // PM 허브 상태
    public string RunId { get; set; } = string.Empty;
    public bool IsEndReport { get; set; }
    public bool IsUserIntervention { get; set; }
    public string? OutputsFolder { get; set; }
    public string? ReportPath { get; set; }
    public string? CommitSha { get; set; }
    public string? InterventionReason { get; set; }
    public string? InterventionQuestion { get; set; }
}

public class AgentTurn
{
    /// <summary>event_log 에 적재될 이벤트 ID. 턴 생성 시점에 미리 발급해 Wiki sourceEventId 등 외부 참조와 동기화한다.</summary>
    public string EventId { get; set; } = Guid.NewGuid().ToString();
    public int TurnIndex { get; set; }
    public string PersonaId { get; set; } = string.Empty;
    public string PersonaName { get; set; } = string.Empty;
    public string PersonaLabel { get; set; } = string.Empty;
    public string PersonaAvatar { get; set; } = string.Empty;
    public bool IsPm { get; set; }
    public string Content { get; set; } = string.Empty;
    public string ModelUsed { get; set; } = string.Empty;
    public string? HandoffToLabel { get; set; }
    public string? HandoffToName { get; set; }
    public string? HandoffRequest { get; set; }
    public List<ToolCallRecord> ToolCalls { get; set; } = [];
    public List<string> WrittenFiles { get; set; } = [];

    // PM 전용 상태
    public bool IsEndReport { get; set; }
    public bool IsUserIntervention { get; set; }
    public bool IsPmGreeting { get; set; }   // PM 첫 수신("PM — 지시 접수") 뱃지용

    // 다자 토론(round-table) 상태 — DiscussionId가 있으면 이 턴은 토론 라운드의 일부다
    public string? DiscussionId { get; set; }
    public int? RoundIndex { get; set; }
    public int? SpeakerOrder { get; set; }
    public string? Stance { get; set; }        // agree / object / extend
    public bool IsDiscussionOpener { get; set; }
    public bool IsDiscussionSpeaker { get; set; }
    public bool IsDiscussionSummary { get; set; }
    public string? DiscussionTopic { get; set; }

    // 스트리밍 상태 — 동일 StreamKey의 프리뷰는 UI에서 이어붙여 렌더한다
    public string? StreamKey { get; set; }
    public bool IsStreamingPreview { get; set; }
}
