using Microsoft.EntityFrameworkCore;
using AgentPaw.Data;
using AgentPaw.Models;

namespace AgentPaw.Services;

public class InstructionService
{
    private readonly IDbContextFactory<AgentPawDbContext> _dbFactory;
    private readonly ConfigLoaderService _configLoader;

    public InstructionService(IDbContextFactory<AgentPawDbContext> dbFactory, ConfigLoaderService configLoader)
    {
        _dbFactory = dbFactory;
        _configLoader = configLoader;
    }

    // === Group CRUD ===

    public async Task<List<InstructionGroup>> ListGroupsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.InstructionGroups.OrderBy(g => g.Name).ToListAsync();
    }

    public async Task<InstructionGroup> CreateGroupAsync(string name, string? description = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var group = new InstructionGroup
        {
            GroupId = Guid.NewGuid().ToString(),
            Name = name.Trim(),
            Description = description?.Trim() ?? string.Empty,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.InstructionGroups.Add(group);
        await db.SaveChangesAsync();
        return group;
    }

    public async Task UpdateGroupAsync(string groupId, string name, string? description = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var group = await db.InstructionGroups.FindAsync(groupId);
        if (group == null) return;

        group.Name = name.Trim();
        if (description != null) group.Description = description.Trim();
        group.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task DeleteGroupAsync(string groupId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        // 그룹 내 파일을 미분류로 이동
        var files = await db.InstructionFiles.Where(f => f.GroupId == groupId).ToListAsync();
        foreach (var f in files)
            f.GroupId = null;

        var group = await db.InstructionGroups.FindAsync(groupId);
        if (group != null)
            db.InstructionGroups.Remove(group);

        await db.SaveChangesAsync();
    }

    // === File CRUD ===

    public async Task<List<InstructionFile>> ListFilesAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.InstructionFiles.OrderBy(f => f.Name).ToListAsync();
    }

    public async Task<InstructionFile> CreateFileAsync(string name, string content, string? groupId = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var file = new InstructionFile
        {
            FileId = Guid.NewGuid().ToString(),
            GroupId = groupId,
            Name = name.Trim(),
            Content = content,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.InstructionFiles.Add(file);
        await db.SaveChangesAsync();
        _configLoader.InvalidateAll();
        return file;
    }

    public async Task UpdateFileAsync(string fileId, string? name = null, string? content = null, string? groupId = null, bool clearGroup = false)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var file = await db.InstructionFiles.FindAsync(fileId);
        if (file == null) return;

        if (name != null) file.Name = name.Trim();
        if (content != null) file.Content = content;
        if (clearGroup) file.GroupId = null;
        else if (groupId != null) file.GroupId = groupId;
        file.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        _configLoader.InvalidateAll();
    }

    public async Task DeleteFileAsync(string fileId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var projectLinks = await db.ProjectInstructions.Where(pi => pi.FileId == fileId).ToListAsync();
        db.ProjectInstructions.RemoveRange(projectLinks);

        var personaLinks = await db.PersonaInstructions.Where(pi => pi.FileId == fileId).ToListAsync();
        db.PersonaInstructions.RemoveRange(personaLinks);

        var file = await db.InstructionFiles.FindAsync(fileId);
        if (file != null)
            db.InstructionFiles.Remove(file);

        await db.SaveChangesAsync();
        _configLoader.InvalidateAll();
    }

    public async Task<string?> GetFileContentAsync(string fileId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var file = await db.InstructionFiles.FindAsync(fileId);
        return file?.Content;
    }

    /// <summary>
    /// 로컬 파일 경로에서 인스트럭션 파일을 업로드하여 DB에 저장한다.
    /// </summary>
    public async Task<List<InstructionFile>> UploadFilesAsync(string[] filePaths, string? groupId = null)
    {
        var results = new List<InstructionFile>();
        foreach (var path in filePaths)
        {
            var name = System.IO.Path.GetFileName(path);
            var content = await System.IO.File.ReadAllTextAsync(path);
            var file = await CreateFileAsync(name, content, groupId);
            results.Add(file);
        }
        return results;
    }

    // === Project-Instruction 연결 ===

    public async Task<List<InstructionFile>> ListForProjectAsync(string projectId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var fileIds = await db.ProjectInstructions
            .Where(pi => pi.ProjectId == projectId)
            .Select(pi => pi.FileId)
            .ToListAsync();

        return await db.InstructionFiles
            .Where(f => fileIds.Contains(f.FileId))
            .OrderBy(f => f.Name)
            .ToListAsync();
    }

    public async Task LinkToProjectAsync(string projectId, string fileId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var exists = await db.ProjectInstructions
            .AnyAsync(pi => pi.ProjectId == projectId && pi.FileId == fileId);
        if (exists) return;

        db.ProjectInstructions.Add(new ProjectInstruction
        {
            ProjectId = projectId,
            FileId = fileId
        });
        await db.SaveChangesAsync();
        _configLoader.InvalidateAll();
    }

    public async Task UnlinkFromProjectAsync(string projectId, string fileId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var link = await db.ProjectInstructions
            .FirstOrDefaultAsync(pi => pi.ProjectId == projectId && pi.FileId == fileId);
        if (link != null)
        {
            db.ProjectInstructions.Remove(link);
            await db.SaveChangesAsync();
            _configLoader.InvalidateAll();
        }
    }

    public async Task LinkGroupToProjectAsync(string projectId, string groupId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var fileIds = await db.InstructionFiles
            .Where(f => f.GroupId == groupId)
            .Select(f => f.FileId)
            .ToListAsync();

        var existingLinks = await db.ProjectInstructions
            .Where(pi => pi.ProjectId == projectId && fileIds.Contains(pi.FileId))
            .Select(pi => pi.FileId)
            .ToListAsync();

        var newLinks = fileIds.Except(existingLinks)
            .Select(fid => new ProjectInstruction { ProjectId = projectId, FileId = fid });

        db.ProjectInstructions.AddRange(newLinks);
        await db.SaveChangesAsync();
        _configLoader.InvalidateAll();
    }

    // === Persona-Instruction 연결 ===

    public async Task<List<InstructionFile>> ListForPersonaAsync(string personaId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var fileIds = await db.PersonaInstructions
            .Where(pi => pi.PersonaId == personaId)
            .Select(pi => pi.FileId)
            .ToListAsync();

        return await db.InstructionFiles
            .Where(f => fileIds.Contains(f.FileId))
            .OrderBy(f => f.Name)
            .ToListAsync();
    }

    public async Task LinkToPersonaAsync(string personaId, string fileId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var exists = await db.PersonaInstructions
            .AnyAsync(pi => pi.PersonaId == personaId && pi.FileId == fileId);
        if (exists) return;

        db.PersonaInstructions.Add(new PersonaInstruction
        {
            PersonaId = personaId,
            FileId = fileId
        });
        await db.SaveChangesAsync();
        _configLoader.InvalidateAll();
    }

    public async Task UnlinkFromPersonaAsync(string personaId, string fileId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var link = await db.PersonaInstructions
            .FirstOrDefaultAsync(pi => pi.PersonaId == personaId && pi.FileId == fileId);
        if (link != null)
        {
            db.PersonaInstructions.Remove(link);
            await db.SaveChangesAsync();
            _configLoader.InvalidateAll();
        }
    }

    public async Task LinkGroupToPersonaAsync(string personaId, string groupId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var fileIds = await db.InstructionFiles
            .Where(f => f.GroupId == groupId)
            .Select(f => f.FileId)
            .ToListAsync();

        var existingLinks = await db.PersonaInstructions
            .Where(pi => pi.PersonaId == personaId && fileIds.Contains(pi.FileId))
            .Select(pi => pi.FileId)
            .ToListAsync();

        var newLinks = fileIds.Except(existingLinks)
            .Select(fid => new PersonaInstruction { PersonaId = personaId, FileId = fid });

        db.PersonaInstructions.AddRange(newLinks);
        await db.SaveChangesAsync();
        _configLoader.InvalidateAll();
    }
}
