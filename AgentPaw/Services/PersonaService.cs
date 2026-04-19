using Microsoft.EntityFrameworkCore;
using AgentPaw.Data;
using AgentPaw.Models;

namespace AgentPaw.Services;

/// <summary>
/// 프로젝트-페르소나 연결 및 전역 페르소나 템플릿 시드를 담당한다.
/// 페르소나는 전역 레지스트리(persona.project_id IS NULL)로 등록되며,
/// 프로젝트에는 project_persona 링크 테이블로만 연결한다.
/// </summary>
public class PersonaService
{
    private const string MetaKeyPersonaSeed = "persona_seed_version";
    private readonly IDbContextFactory<AgentPawDbContext> _dbFactory;
    private readonly ConfigLoaderService _configLoader;

    public PersonaService(IDbContextFactory<AgentPawDbContext> dbFactory, ConfigLoaderService configLoader)
    {
        _dbFactory = dbFactory;
        _configLoader = configLoader;
    }

    // === Project-Persona 연결 ===

    public async Task<List<Persona>> ListForProjectAsync(string projectId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var personaIds = await db.ProjectPersonas
            .Where(pp => pp.ProjectId == projectId)
            .Select(pp => pp.PersonaId)
            .ToListAsync();

        return await db.Personas
            .Where(p => personaIds.Contains(p.PersonaId))
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Label)
            .ToListAsync();
    }

    /// <summary>전역 페르소나(ProjectId IS NULL) 목록</summary>
    public async Task<List<Persona>> ListGlobalAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Personas
            .Where(p => p.ProjectId == null)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Label)
            .ToListAsync();
    }

    public async Task LinkToProjectAsync(string projectId, string personaId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var exists = await db.ProjectPersonas
            .AnyAsync(pp => pp.ProjectId == projectId && pp.PersonaId == personaId);
        if (exists) return;

        db.ProjectPersonas.Add(new ProjectPersona
        {
            ProjectId = projectId,
            PersonaId = personaId
        });
        await db.SaveChangesAsync();
        _configLoader.InvalidateAll();
    }

    public async Task UnlinkFromProjectAsync(string projectId, string personaId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var link = await db.ProjectPersonas
            .FirstOrDefaultAsync(pp => pp.ProjectId == projectId && pp.PersonaId == personaId);
        if (link != null)
        {
            db.ProjectPersonas.Remove(link);
            await db.SaveChangesAsync();
            _configLoader.InvalidateAll();
        }
    }

    // === Global Template Seeding ===

    /// <summary>
    /// app_meta.persona_seed_version 이 최신이 아니면 빌트인 페르소나·그룹을 재시드한다.
    /// 설치본 업그레이드 시 새 빌트인 목록이 반영되도록 기존 빌트인은 모두 교체한다.
    /// 사용자가 편집한 비-빌트인(IsBuiltin=false) 페르소나는 보존한다.
    /// </summary>
    public async Task EnsureSeedAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        string? currentVersion = null;
        try
        {
            var conn = db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM app_meta WHERE key = @k LIMIT 1";
            var p = cmd.CreateParameter();
            p.ParameterName = "@k";
            p.Value = MetaKeyPersonaSeed;
            cmd.Parameters.Add(p);
            var result = await cmd.ExecuteScalarAsync();
            currentVersion = result as string;
        }
        catch { /* app_meta가 아직 없는 초기 상태 */ }

        if (string.Equals(currentVersion, PersonaDefaultsService.SeedVersion, StringComparison.Ordinal))
        {
            return;
        }

        await ReseedInternalAsync(db);

        await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO app_meta (key, value, updated_at)
            VALUES ({MetaKeyPersonaSeed}, {PersonaDefaultsService.SeedVersion}, now())
            ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value, updated_at = now()
        ");
    }

    /// <summary>
    /// 전역 페르소나 템플릿을 강제로 시드한다.
    /// overwrite=true: 기존 빌트인 페르소나·그룹을 교체한다.
    /// overwrite=false: 이미 존재하면 건너뛴다.
    /// </summary>
    public async Task SeedGlobalTemplatesAsync(bool overwrite)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        if (!overwrite)
        {
            var anyExists = await db.Personas.AnyAsync(p => p.ProjectId == null && p.IsBuiltin);
            if (anyExists) return;
        }

        await ReseedInternalAsync(db);
    }

    private static async Task ReseedInternalAsync(AgentPawDbContext db)
    {
        // 기존 빌트인 페르소나·연결 제거를 raw SQL 로 수행해 EF 트래킹 동시성 예외를 회피한다.
        await db.Database.ExecuteSqlRawAsync(@"
            DELETE FROM project_persona
             WHERE persona_id IN (
                 SELECT persona_id FROM persona
                  WHERE project_id IS NULL AND is_builtin
             );
            DELETE FROM persona_instruction
             WHERE persona_id IN (
                 SELECT persona_id FROM persona
                  WHERE project_id IS NULL AND is_builtin
             );
            DELETE FROM persona
             WHERE project_id IS NULL AND is_builtin;
        ");

        // 기존 빌트인 그룹(전역) 제거 — 시드에 정의된 GroupId만 지운다 (사용자 추가 그룹 보존).
        var seedGroups = PersonaDefaultsService.GetDefaultGroups();
        var seedGroupIds = seedGroups.Select(g => g.GroupId).ToArray();
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM persona_group WHERE project_id IS NULL AND group_id = ANY(@ids)",
            new Npgsql.NpgsqlParameter("@ids", seedGroupIds));

        // 트래커 리셋 후 새 그룹·페르소나 삽입
        db.ChangeTracker.Clear();

        db.PersonaGroups.AddRange(seedGroups);
        await db.SaveChangesAsync();

        var templates = PersonaDefaultsService.GetDefaultPersonas(projectId: null);
        db.Personas.AddRange(templates);
        await db.SaveChangesAsync();

        // 기존 모든 프로젝트에 새 빌트인 페르소나를 자동 링크 (기존 UX 유지)
        var projectIds = await db.Projects.Select(p => p.ProjectId).ToListAsync();
        if (projectIds.Count > 0)
        {
            var newLinks = new List<ProjectPersona>();
            foreach (var projectId in projectIds)
            {
                foreach (var persona in templates)
                {
                    newLinks.Add(new ProjectPersona
                    {
                        ProjectId = projectId,
                        PersonaId = persona.PersonaId
                    });
                }
            }
            db.ProjectPersonas.AddRange(newLinks);
            await db.SaveChangesAsync();
        }
    }

    /// <summary>프로젝트 생성 시 모든 전역 빌트인 템플릿을 link 테이블로 연결한다.</summary>
    public async Task LinkAllGlobalTemplatesAsync(string projectId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var templateIds = await db.Personas
            .Where(p => p.ProjectId == null && p.IsBuiltin)
            .Select(p => p.PersonaId)
            .ToListAsync();

        var existing = await db.ProjectPersonas
            .Where(pp => pp.ProjectId == projectId && templateIds.Contains(pp.PersonaId))
            .Select(pp => pp.PersonaId)
            .ToListAsync();

        var newLinks = templateIds.Except(existing)
            .Select(pid => new ProjectPersona { ProjectId = projectId, PersonaId = pid });

        db.ProjectPersonas.AddRange(newLinks);
        await db.SaveChangesAsync();
    }
}
