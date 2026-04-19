using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using AgentPaw.Data;
using AgentPaw.Models;

namespace AgentPaw.Services;

public class SnapshotService
{
    private readonly IDbContextFactory<AgentPawDbContext> _dbFactory;
    private readonly GitService _gitService;
    private const int MaxSnapshots = 100;
    private const int MaxPreRollback = 5;

    public SnapshotService(IDbContextFactory<AgentPawDbContext> dbFactory, GitService gitService)
    {
        _dbFactory = dbFactory;
        _gitService = gitService;
    }

    public async Task<Snapshot> CreateSnapshotAsync(string projectId, string triggerType, string? createdBy, string? description)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        // 용량 제한
        if (triggerType == "PRE_ROLLBACK")
            await EnforcePreRollbackLimitAsync(db, projectId);
        else
            await EnforceSnapshotCapacityAsync(db, projectId);

        // Git repo path 조회
        var project = await db.Projects.FirstAsync(p => p.ProjectId == projectId);
        var repoPath = project.GitRepoPath;

        // Git commit
        string commitHash;
        try
        {
            var commitMessage = triggerType switch
            {
                "MANUAL" => $"[Snapshot] Manual: {description ?? "no description"}",
                "AUTO" => "[Snapshot] Auto snapshot",
                "PRE_ROLLBACK" => "[Snapshot] Pre-rollback backup",
                _ => "[Snapshot]"
            };
            commitHash = _gitService.CommitAll(repoPath, commitMessage);
        }
        catch
        {
            commitHash = _gitService.GetCurrentCommitHash(repoPath);
        }

        var snapshotId = Guid.NewGuid().ToString();
        var snapshot = new Snapshot
        {
            SnapshotId = snapshotId,
            ProjectId = projectId,
            GitCommitHash = commitHash,
            VectorIndexId = $"vec_{snapshotId[..8]}",
            TriggerType = triggerType,
            CreatedBy = createdBy,
            Description = description,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Snapshots.Add(snapshot);

        // 이벤트 로그
        db.EventLogs.Add(new EventLog
        {
            EventId = Guid.NewGuid().ToString(),
            ProjectId = projectId,
            EventType = "SNAPSHOT",
            Payload = JsonSerializer.Serialize(new
            {
                snapshotId,
                triggerType,
                commitHash,
                description
            }),
            TriggeredBy = createdBy,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();
        return snapshot;
    }

    public async Task<List<Snapshot>> ListSnapshotsAsync(string projectId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Snapshots
            .Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<Snapshot?> GetSnapshotAsync(string snapshotId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Snapshots.FindAsync(snapshotId);
    }

    private async Task EnforceSnapshotCapacityAsync(AgentPawDbContext db, string projectId)
    {
        var count = await db.Snapshots.CountAsync(s => s.ProjectId == projectId);
        if (count >= MaxSnapshots)
        {
            var oldest = await db.Snapshots
                .Where(s => s.ProjectId == projectId && s.TriggerType == "AUTO")
                .OrderBy(s => s.CreatedAt)
                .Take(count - MaxSnapshots + 1)
                .ToListAsync();
            db.Snapshots.RemoveRange(oldest);
        }
    }

    private async Task EnforcePreRollbackLimitAsync(AgentPawDbContext db, string projectId)
    {
        var count = await db.Snapshots.CountAsync(s => s.ProjectId == projectId && s.TriggerType == "PRE_ROLLBACK");
        if (count >= MaxPreRollback)
        {
            var oldest = await db.Snapshots
                .Where(s => s.ProjectId == projectId && s.TriggerType == "PRE_ROLLBACK")
                .OrderBy(s => s.CreatedAt)
                .Take(count - MaxPreRollback + 1)
                .ToListAsync();
            db.Snapshots.RemoveRange(oldest);
        }
    }
}
