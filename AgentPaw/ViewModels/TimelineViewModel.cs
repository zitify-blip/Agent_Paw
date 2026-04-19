using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using AgentPaw.Data;
using AgentPaw.Models;
using AgentPaw.Services;

namespace AgentPaw.ViewModels;

public partial class TimelineViewModel : ObservableObject
{
    private readonly SnapshotService _snapshotService;
    private readonly RollbackService _rollbackService;
    private readonly IDbContextFactory<AgentPawDbContext> _dbFactory;
    private readonly AuthService _authService;

    [ObservableProperty] private string _projectId = string.Empty;
    [ObservableProperty] private bool _isEventsTab = true;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    // 스냅샷 생성 다이얼로그
    [ObservableProperty] private bool _isCreateDialogOpen;
    [ObservableProperty] private string _newSnapshotDescription = string.Empty;

    // 롤백 확인 다이얼로그
    [ObservableProperty] private bool _isRollbackDialogOpen;
    [ObservableProperty] private Snapshot? _rollbackTarget;
    [ObservableProperty] private bool _rollbackBackup = true;
    [ObservableProperty] private RollbackImpact? _rollbackImpact;

    public ObservableCollection<TimelineEvent> Events { get; } = [];
    public ObservableCollection<Snapshot> Snapshots { get; } = [];

    public TimelineViewModel(
        SnapshotService snapshotService,
        RollbackService rollbackService,
        IDbContextFactory<AgentPawDbContext> dbFactory,
        AuthService authService)
    {
        _snapshotService = snapshotService;
        _rollbackService = rollbackService;
        _dbFactory = dbFactory;
        _authService = authService;
    }

    public async Task LoadAsync(string projectId)
    {
        ProjectId = projectId;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            Events.Clear();
            Snapshots.Clear();

            // 이벤트 로드
            await using var db = await _dbFactory.CreateDbContextAsync();
            var events = await db.EventLogs
                .Where(e => e.ProjectId == ProjectId && !e.IsDeleted)
                .OrderByDescending(e => e.CreatedAt)
                .Take(200)
                .ToListAsync();

            foreach (var evt in events)
            {
                var summary = GetPayloadSummary(evt.Payload);
                Events.Add(new TimelineEvent
                {
                    EventId = evt.EventId,
                    EventType = evt.EventType,
                    Summary = summary,
                    ModelUsed = evt.ModelUsed,
                    TriggeredBy = evt.TriggeredBy,
                    CreatedAt = evt.CreatedAt
                });
            }

            // 스냅샷 로드
            var snapshots = await _snapshotService.ListSnapshotsAsync(ProjectId);
            foreach (var s in snapshots)
                Snapshots.Add(s);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.InnerException?.Message ?? ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void SwitchToEvents()
    {
        IsEventsTab = true;
    }

    [RelayCommand]
    private void SwitchToSnapshots()
    {
        IsEventsTab = false;
    }

    [RelayCommand]
    private void OpenCreateDialog()
    {
        NewSnapshotDescription = string.Empty;
        IsCreateDialogOpen = true;
    }

    [RelayCommand]
    private void CloseCreateDialog()
    {
        IsCreateDialogOpen = false;
    }

    [RelayCommand]
    private async Task CreateSnapshotAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            await _snapshotService.CreateSnapshotAsync(
                ProjectId, "MANUAL", _authService.CurrentUserId, NewSnapshotDescription.Trim());
            IsCreateDialogOpen = false;
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.InnerException?.Message ?? ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task OpenRollbackDialogAsync(Snapshot snapshot)
    {
        IsLoading = true;
        try
        {
            RollbackTarget = snapshot;
            RollbackBackup = true;
            RollbackImpact = await _rollbackService.GetRollbackImpactAsync(snapshot.SnapshotId);
            IsRollbackDialogOpen = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.InnerException?.Message ?? ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void CloseRollbackDialog()
    {
        IsRollbackDialogOpen = false;
        RollbackTarget = null;
        RollbackImpact = null;
    }

    [RelayCommand]
    private async Task ExecuteRollbackAsync()
    {
        if (RollbackTarget == null) return;

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var result = await _rollbackService.ExecuteRollbackAsync(
                RollbackTarget.SnapshotId, RollbackBackup, _authService.CurrentUserId);

            if (!result.Success)
            {
                ErrorMessage = "롤백에 실패했습니다.";
            }

            IsRollbackDialogOpen = false;
            RollbackTarget = null;
            RollbackImpact = null;
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.InnerException?.Message ?? ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static string GetPayloadSummary(string payload)
    {
        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(payload);
            // message, content, description 중 첫 번째 발견 필드를 요약
            foreach (var key in new[] { "message", "content", "description", "error" })
            {
                if (json.TryGetProperty(key, out var val))
                {
                    var text = val.GetString() ?? string.Empty;
                    return text.Length > 80 ? text[..80] + "…" : text;
                }
            }
            return payload.Length > 80 ? payload[..80] + "…" : payload;
        }
        catch
        {
            return payload.Length > 80 ? payload[..80] + "…" : payload;
        }
    }
}

public class TimelineEvent
{
    public string EventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? ModelUsed { get; set; }
    public string? TriggeredBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
