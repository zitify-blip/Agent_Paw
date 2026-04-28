using Microsoft.EntityFrameworkCore;
using AgentPaw.Data;
using AgentPaw.Models;

namespace AgentPaw.Services;

public class WikiService
{
    private readonly IDbContextFactory<AgentPawDbContext> _dbFactory;

    public WikiService(IDbContextFactory<AgentPawDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<WikiDocument>> ListWikisAsync(string projectId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.WikiDocuments
            .Where(w => w.ProjectId == projectId)
            .OrderBy(w => w.SortOrder)
            .ThenBy(w => w.CreatedAt)
            .ToListAsync();
    }

    public async Task<WikiDocument?> GetWikiAsync(string wikiId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.WikiDocuments.FindAsync(wikiId);
    }

    public async Task<WikiDocument> CreateWikiAsync(string projectId, string category, string title, string content, string? sourceEventId = null, string? parentId = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var maxSort = 0;
        if (await db.WikiDocuments.AnyAsync(w => w.ProjectId == projectId && w.ParentId == parentId))
            maxSort = await db.WikiDocuments
                .Where(w => w.ProjectId == projectId && w.ParentId == parentId)
                .MaxAsync(w => w.SortOrder);

        var wiki = new WikiDocument
        {
            WikiId = Guid.NewGuid().ToString(),
            ProjectId = projectId,
            Category = category,
            Title = title,
            Content = content,
            SourceEventId = sourceEventId,
            ParentId = parentId,
            SortOrder = maxSort + 10,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.WikiDocuments.Add(wiki);
        await db.SaveChangesAsync();
        return wiki;
    }

    public async Task UpdateWikiAsync(string wikiId, string? title, string? content, string? category)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var wiki = await db.WikiDocuments.FindAsync(wikiId)
            ?? throw new InvalidOperationException("위키 문서를 찾을 수 없습니다.");

        if (title != null) wiki.Title = title;
        if (content != null) wiki.Content = content;
        if (category != null) wiki.Category = category;
        wiki.Version++;
        wiki.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync();
    }

    public async Task<List<WikiDocument>> SearchWikisAsync(string projectId, string query)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var lowerQuery = query.ToLowerInvariant();
        return await db.WikiDocuments
            .Where(w => w.ProjectId == projectId
                && (w.Title.ToLower().Contains(lowerQuery) || w.Content.ToLower().Contains(lowerQuery)))
            .OrderByDescending(w => w.UpdatedAt)
            .ToListAsync();
    }

    public async Task SetParentAsync(string wikiId, string? parentId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var wiki = await db.WikiDocuments.FindAsync(wikiId);
        if (wiki == null) return;
        wiki.ParentId = parentId;
        wiki.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task DeleteWikiAsync(string wikiId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var wiki = await db.WikiDocuments.FindAsync(wikiId);
        if (wiki == null) return;
        db.WikiDocuments.Remove(wiki);
        await db.SaveChangesAsync();
    }

    public async Task DeleteWithChildrenAsync(string wikiId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        await DeleteRecursive(db, wikiId);
        await db.SaveChangesAsync();
    }

    private async Task DeleteRecursive(AgentPawDbContext db, string wikiId)
    {
        var children = await db.WikiDocuments
            .Where(w => w.ParentId == wikiId)
            .ToListAsync();
        foreach (var child in children)
            await DeleteRecursive(db, child.WikiId);
        var doc = await db.WikiDocuments.FindAsync(wikiId);
        if (doc != null) db.WikiDocuments.Remove(doc);
    }
}
