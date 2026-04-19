using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using AgentPaw.Data;
using AgentPaw.Models;

namespace AgentPaw.Services;

public class ProjectService
{
    private readonly IDbContextFactory<AgentPawDbContext> _dbFactory;
    private readonly GitService _gitService;
    private readonly PersonaService _personaService;

    public ProjectService(IDbContextFactory<AgentPawDbContext> dbFactory, GitService gitService, PersonaService personaService)
    {
        _dbFactory = dbFactory;
        _gitService = gitService;
        _personaService = personaService;
    }

    public async Task<Project> CreateProjectAsync(string userId, string name, string? description, string hierarchyType = "ROOT", string? parentProjectId = null)
    {
        var projectId = Guid.NewGuid().ToString();
        var repoPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AgentPaw", "repos", projectId);

        await using var db = await _dbFactory.CreateDbContextAsync();

        // 1단계: Project 먼저 저장 (다른 테이블의 FK 참조 대상)
        var project = new Project
        {
            ProjectId = projectId,
            ProjectName = name,
            Description = description,
            HierarchyType = hierarchyType,
            ParentProjectId = parentProjectId,
            OwnerUserId = userId,
            GitRepoPath = repoPath,
            Status = "ACTIVE",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        // 2단계: FK가 충족된 후 나머지 엔티티 저장
        db.ProjectMembers.Add(new ProjectMember
        {
            MemberId = Guid.NewGuid().ToString(),
            ProjectId = projectId,
            UserId = userId,
            Role = "OWNER",
            CreatedAt = DateTimeOffset.UtcNow
        });

        db.Workspaces.Add(new Workspace
        {
            WorkspaceId = Guid.NewGuid().ToString(),
            ProjectId = projectId,
            ContextWindowSize = 32000,
            AutoSnapshotEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        db.AuditLogs.Add(new AuditLog
        {
            AuditId = Guid.NewGuid().ToString(),
            UserId = userId,
            ProjectId = projectId,
            Action = "PROJECT_CREATE",
            Detail = JsonSerializer.Serialize(new { name, hierarchyType }),
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();

        // 전역 빌트인 페르소나 템플릿을 link 테이블로 일괄 연결
        await _personaService.LinkAllGlobalTemplatesAsync(projectId);

        // Init git repo outside transaction
        _gitService.InitRepo(repoPath);

        return project;
    }

    public async Task<List<ProjectListItem>> ListProjectsForUserAsync(string userId, string status = "ACTIVE")
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var query = from pm in db.ProjectMembers
                    join p in db.Projects on pm.ProjectId equals p.ProjectId
                    where pm.UserId == userId && p.Status == status
                    orderby p.UpdatedAt descending
                    select new ProjectListItem
                    {
                        ProjectId = p.ProjectId,
                        ProjectName = p.ProjectName,
                        Description = p.Description,
                        HierarchyType = p.HierarchyType,
                        Status = p.Status,
                        Role = pm.Role,
                        CreatedAt = p.CreatedAt,
                        UpdatedAt = p.UpdatedAt
                    };

        return await query.ToListAsync();
    }

    public async Task<Project?> GetProjectAsync(string projectId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Projects.FindAsync(projectId);
    }

    public async Task UpdateProjectAsync(string userId, string projectId, string name, string? description)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var member = await db.ProjectMembers
            .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId);
        if (member == null || member.Role == "VIEWER")
            throw new InvalidOperationException("PERMISSION_DENIED");

        var project = await db.Projects.FindAsync(projectId)
                      ?? throw new InvalidOperationException("PROJECT_NOT_FOUND");

        project.ProjectName = name;
        project.Description = description;
        project.UpdatedAt = DateTimeOffset.UtcNow;

        db.AuditLogs.Add(new AuditLog
        {
            AuditId = Guid.NewGuid().ToString(),
            UserId = userId,
            ProjectId = projectId,
            Action = "PROJECT_UPDATE",
            Detail = JsonSerializer.Serialize(new { name, description }),
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();
    }

    public async Task ArchiveProjectAsync(string userId, string projectId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var project = await db.Projects.FindAsync(projectId)
                      ?? throw new InvalidOperationException("PROJECT_NOT_FOUND");

        project.Status = "ARCHIVED";
        project.UpdatedAt = DateTimeOffset.UtcNow;

        db.AuditLogs.Add(new AuditLog
        {
            AuditId = Guid.NewGuid().ToString(),
            UserId = userId,
            ProjectId = projectId,
            Action = "PROJECT_ARCHIVE",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();
    }

    public async Task RestoreProjectAsync(string userId, string projectId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var project = await db.Projects.FindAsync(projectId)
                      ?? throw new InvalidOperationException("PROJECT_NOT_FOUND");

        project.Status = "ACTIVE";
        project.UpdatedAt = DateTimeOffset.UtcNow;

        db.AuditLogs.Add(new AuditLog
        {
            AuditId = Guid.NewGuid().ToString(),
            UserId = userId,
            ProjectId = projectId,
            Action = "PROJECT_RESTORE",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();
    }

    public async Task DeleteProjectAsync(string userId, string projectId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var member = await db.ProjectMembers
            .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId);
        if (member == null || member.Role != "OWNER")
            throw new InvalidOperationException("OWNER_ONLY");

        var project = await db.Projects.FindAsync(projectId)
                      ?? throw new InvalidOperationException("PROJECT_NOT_FOUND");

        var repoPath = project.GitRepoPath;

        // Raw SQL로 관련 데이터 삭제 (존재하지 않는 테이블은 자동 스킵)
        var tables = new[]
        {
            "project_instruction",
            "event_log",
            "snapshot",
            "wiki_document",
            "audit_log",
            "persona",
            "persona_group",
            "workspace",
            "project_member",
        };

        foreach (var table in tables)
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync(
                    $"DELETE FROM {table} WHERE project_id = {{0}}", projectId);
            }
            catch { /* 테이블 미존재 시 무시 */ }
        }

        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM project WHERE project_id = {0}", projectId);
        }
        catch
        {
            // EF tracker 초기화 후 재시도
            db.ChangeTracker.Clear();
            db.Projects.Remove(project);
            await db.SaveChangesAsync();
        }

        // Git 저장소 삭제
        if (!string.IsNullOrEmpty(repoPath) && Directory.Exists(repoPath))
        {
            try { Directory.Delete(repoPath, recursive: true); } catch { }
        }
    }
}

public class ProjectListItem
{
    public string ProjectId { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string HierarchyType { get; set; } = "ROOT";
    public string Status { get; set; } = "ACTIVE";
    public string Role { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
