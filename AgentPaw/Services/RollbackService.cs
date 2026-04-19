using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using AgentPaw.Data;
using AgentPaw.Models;

namespace AgentPaw.Services;

public class RollbackService
{
    private readonly IDbContextFactory<AgentPawDbContext> _dbFactory;
    private readonly GitService _gitService;
    private readonly SnapshotService _snapshotService;

    public RollbackService(
        IDbContextFactory<AgentPawDbContext> dbFactory,
        GitService gitService,
        SnapshotService snapshotService)
    {
        _dbFactory = dbFactory;
        _gitService = gitService;
        _snapshotService = snapshotService;
    }

    public async Task<RollbackResult> ExecuteRollbackAsync(string snapshotId, bool backup, string? userId)
    {
        var snapshot = await _snapshotService.GetSnapshotAsync(snapshotId)
            ?? throw new InvalidOperationException("스냅샷을 찾을 수 없습니다.");

        string? backupSnapshotId = null;
        if (backup)
        {
            var backupSnapshot = await _snapshotService.CreateSnapshotAsync(
                snapshot.ProjectId, "PRE_ROLLBACK", userId, "롤백 전 자동 백업");
            backupSnapshotId = backupSnapshot.SnapshotId;
        }

        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            // Git reset --hard
            var project = await db.Projects.FirstAsync(p => p.ProjectId == snapshot.ProjectId);
            _gitService.ResetHard(project.GitRepoPath, snapshot.GitCommitHash);

            // 스냅샷 이후 이벤트 soft-delete
            var eventsToDelete = await db.EventLogs
                .Where(e => e.ProjectId == snapshot.ProjectId
                    && e.CreatedAt > snapshot.CreatedAt
                    && !e.IsDeleted)
                .ToListAsync();

            foreach (var evt in eventsToDelete)
                evt.IsDeleted = true;

            // 롤백 이벤트 기록
            db.EventLogs.Add(new EventLog
            {
                EventId = Guid.NewGuid().ToString(),
                ProjectId = snapshot.ProjectId,
                EventType = "ROLLBACK",
                Payload = JsonSerializer.Serialize(new
                {
                    targetSnapshotId = snapshotId,
                    backupSnapshotId,
                    restoredToCommit = snapshot.GitCommitHash,
                    eventsDeleted = eventsToDelete.Count
                }),
                TriggeredBy = userId,
                CreatedAt = DateTimeOffset.UtcNow
            });

            await db.SaveChangesAsync();

            return new RollbackResult
            {
                Success = true,
                BackupSnapshotId = backupSnapshotId,
                RestoredTo = snapshot.GitCommitHash,
                EventsDeleted = eventsToDelete.Count
            };
        }
        catch (Exception ex)
        {
            // 롤백 실패 시 백업에서 복구 시도
            if (backupSnapshotId != null)
            {
                try
                {
                    var backupSnapshot = await _snapshotService.GetSnapshotAsync(backupSnapshotId);
                    if (backupSnapshot != null)
                    {
                        await using var db = await _dbFactory.CreateDbContextAsync();
                        var project = await db.Projects.FirstAsync(p => p.ProjectId == snapshot.ProjectId);
                        _gitService.ResetHard(project.GitRepoPath, backupSnapshot.GitCommitHash);
                    }
                }
                catch { /* 복구 실패 무시 */ }
            }

            // 에러 이벤트 기록
            await using var errorDb = await _dbFactory.CreateDbContextAsync();
            errorDb.EventLogs.Add(new EventLog
            {
                EventId = Guid.NewGuid().ToString(),
                ProjectId = snapshot.ProjectId,
                EventType = "ERROR",
                Payload = JsonSerializer.Serialize(new
                {
                    action = "ROLLBACK",
                    error = ex.Message,
                    targetSnapshotId = snapshotId
                }),
                TriggeredBy = userId,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await errorDb.SaveChangesAsync();

            return new RollbackResult
            {
                Success = false,
                RestoredTo = string.Empty,
                EventsDeleted = 0
            };
        }
    }

    public async Task<RollbackImpact> GetRollbackImpactAsync(string snapshotId)
    {
        var snapshot = await _snapshotService.GetSnapshotAsync(snapshotId)
            ?? throw new InvalidOperationException("스냅샷을 찾을 수 없습니다.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        var affectedEvents = await db.EventLogs
            .Where(e => e.ProjectId == snapshot.ProjectId
                && e.CreatedAt > snapshot.CreatedAt
                && !e.IsDeleted)
            .ToListAsync();

        var eventsByType = affectedEvents
            .GroupBy(e => e.EventType)
            .ToDictionary(g => g.Key, g => g.Count());

        return new RollbackImpact
        {
            TargetSnapshot = snapshot,
            AffectedEvents = affectedEvents.Count,
            EventsByType = eventsByType
        };
    }
}

public class RollbackResult
{
    public bool Success { get; set; }
    public string? BackupSnapshotId { get; set; }
    public string RestoredTo { get; set; } = string.Empty;
    public int EventsDeleted { get; set; }
}

public class RollbackImpact
{
    public Snapshot TargetSnapshot { get; set; } = null!;
    public int AffectedEvents { get; set; }
    public Dictionary<string, int> EventsByType { get; set; } = [];
}
