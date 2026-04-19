using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using AgentPaw.Data;
using AgentPaw.Models;

namespace AgentPaw.Services;

public class MemberService
{
    private readonly IDbContextFactory<AgentPawDbContext> _dbFactory;

    public MemberService(IDbContextFactory<AgentPawDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<MemberListItem>> ListMembersAsync(string projectId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var query = from pm in db.ProjectMembers
                    join u in db.Users on pm.UserId equals u.UserId
                    where pm.ProjectId == projectId
                    orderby pm.CreatedAt
                    select new MemberListItem
                    {
                        MemberId = pm.MemberId,
                        UserId = u.UserId,
                        Email = u.Email,
                        DisplayName = u.DisplayName,
                        Role = pm.Role,
                        CreatedAt = pm.CreatedAt
                    };

        return await query.ToListAsync();
    }

    public async Task InviteMemberAsync(string actorUserId, string projectId, string targetEmail, string role)
    {
        if (role is not ("EDITOR" or "VIEWER"))
            throw new InvalidOperationException("INVALID_ROLE");

        await using var db = await _dbFactory.CreateDbContextAsync();

        var targetUser = await db.Users.FirstOrDefaultAsync(u => u.Email == targetEmail)
                         ?? throw new InvalidOperationException("USER_NOT_FOUND");

        var existing = await db.ProjectMembers
            .AnyAsync(m => m.ProjectId == projectId && m.UserId == targetUser.UserId);
        if (existing)
            throw new InvalidOperationException("ALREADY_MEMBER");

        db.ProjectMembers.Add(new ProjectMember
        {
            MemberId = Guid.NewGuid().ToString(),
            ProjectId = projectId,
            UserId = targetUser.UserId,
            Role = role,
            InvitedBy = actorUserId,
            CreatedAt = DateTimeOffset.UtcNow
        });

        db.AuditLogs.Add(new AuditLog
        {
            AuditId = Guid.NewGuid().ToString(),
            UserId = actorUserId,
            ProjectId = projectId,
            Action = "MEMBER_INVITE",
            TargetUserId = targetUser.UserId,
            Detail = JsonSerializer.Serialize(new { email = targetEmail, role }),
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();
    }

    public async Task ChangeRoleAsync(string actorUserId, string projectId, string memberId, string newRole)
    {
        if (newRole is not ("EDITOR" or "VIEWER"))
            throw new InvalidOperationException("INVALID_ROLE");

        await using var db = await _dbFactory.CreateDbContextAsync();

        var member = await db.ProjectMembers.FindAsync(memberId)
                     ?? throw new InvalidOperationException("MEMBER_NOT_FOUND");

        if (member.Role == "OWNER")
            throw new InvalidOperationException("CANNOT_CHANGE_OWNER_ROLE");

        var oldRole = member.Role;
        member.Role = newRole;

        db.AuditLogs.Add(new AuditLog
        {
            AuditId = Guid.NewGuid().ToString(),
            UserId = actorUserId,
            ProjectId = projectId,
            Action = "ROLE_CHANGE",
            TargetUserId = member.UserId,
            Detail = JsonSerializer.Serialize(new { oldRole, newRole }),
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();
    }

    public async Task RemoveMemberAsync(string actorUserId, string projectId, string memberId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var member = await db.ProjectMembers.FindAsync(memberId)
                     ?? throw new InvalidOperationException("MEMBER_NOT_FOUND");

        if (member.Role == "OWNER")
            throw new InvalidOperationException("CANNOT_REMOVE_OWNER");

        db.ProjectMembers.Remove(member);

        db.AuditLogs.Add(new AuditLog
        {
            AuditId = Guid.NewGuid().ToString(),
            UserId = actorUserId,
            ProjectId = projectId,
            Action = "MEMBER_REMOVE",
            TargetUserId = member.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();
    }

    public async Task TransferOwnershipAsync(string actorUserId, string projectId, string targetMemberId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var actorMember = await db.ProjectMembers
            .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == actorUserId);
        if (actorMember == null || actorMember.Role != "OWNER")
            throw new InvalidOperationException("OWNER_ONLY");

        var targetMember = await db.ProjectMembers.FindAsync(targetMemberId)
                           ?? throw new InvalidOperationException("MEMBER_NOT_FOUND");

        actorMember.Role = "EDITOR";
        targetMember.Role = "OWNER";

        // Update project owner
        var project = await db.Projects.FindAsync(projectId);
        if (project != null)
            project.OwnerUserId = targetMember.UserId;

        db.AuditLogs.Add(new AuditLog
        {
            AuditId = Guid.NewGuid().ToString(),
            UserId = actorUserId,
            ProjectId = projectId,
            Action = "OWNER_TRANSFER",
            TargetUserId = targetMember.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();
    }
}

public class MemberListItem
{
    public string MemberId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
