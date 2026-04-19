using Microsoft.EntityFrameworkCore;
using AgentPaw.Models;

namespace AgentPaw.Data;

public class AgentPawDbContext : DbContext
{
    public AgentPawDbContext(DbContextOptions<AgentPawDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<AuthToken> AuthTokens => Set<AuthToken>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<Persona> Personas => Set<Persona>();
    public DbSet<PersonaGroup> PersonaGroups => Set<PersonaGroup>();
    public DbSet<EventLog> EventLogs => Set<EventLog>();
    public DbSet<Snapshot> Snapshots => Set<Snapshot>();
    public DbSet<WikiDocument> WikiDocuments => Set<WikiDocument>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ApiKeyStore> ApiKeyStores => Set<ApiKeyStore>();
    public DbSet<InstructionGroup> InstructionGroups => Set<InstructionGroup>();
    public DbSet<InstructionFile> InstructionFiles => Set<InstructionFile>();
    public DbSet<ProjectInstruction> ProjectInstructions => Set<ProjectInstruction>();
    public DbSet<PersonaInstruction> PersonaInstructions => Set<PersonaInstruction>();
    public DbSet<ProjectPersona> ProjectPersonas => Set<ProjectPersona>();
    public DbSet<ChatSpaceLink> ChatSpaceLinks => Set<ChatSpaceLink>();
    public DbSet<ChatSessionState> ChatSessionStates => Set<ChatSessionState>();
    public DbSet<ChatBotConfig> ChatBotConfigs => Set<ChatBotConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.HasIndex(u => u.OauthUid).IsUnique();
        });

        // AuthToken
        modelBuilder.Entity<AuthToken>(e =>
        {
            e.HasIndex(t => t.UserId).HasDatabaseName("idx_auth_token_user");
        });

        // Project
        modelBuilder.Entity<Project>(e =>
        {
            e.HasIndex(p => p.GitRepoPath).IsUnique();
        });

        // ProjectMember
        modelBuilder.Entity<ProjectMember>(e =>
        {
            e.HasIndex(m => m.UserId).HasDatabaseName("idx_project_member_user");
            e.HasIndex(m => m.ProjectId).HasDatabaseName("idx_project_member_project");
            e.HasIndex(m => new { m.ProjectId, m.UserId }).IsUnique().HasDatabaseName("idx_project_user");
        });

        // Workspace
        modelBuilder.Entity<Workspace>(e =>
        {
            e.HasIndex(w => w.ProjectId).IsUnique();
        });

        // PersonaGroup
        modelBuilder.Entity<PersonaGroup>(e =>
        {
            e.HasIndex(g => g.ProjectId).HasDatabaseName("idx_persona_group_project");
        });

        // Persona
        modelBuilder.Entity<Persona>(e =>
        {
            e.HasIndex(p => p.ProjectId).HasDatabaseName("idx_persona_project");
            e.HasIndex(p => p.GroupId).HasDatabaseName("idx_persona_group");
            e.HasIndex(p => new { p.ProjectId, p.Name }).HasDatabaseName("idx_persona_project_name");
            // 프로젝트당 PM(is_pm=true) 페르소나 최대 1개 강제
            e.HasIndex(p => p.ProjectId).IsUnique()
                .HasFilter("is_pm = TRUE")
                .HasDatabaseName("ux_persona_project_pm");
        });

        // EventLog
        modelBuilder.Entity<EventLog>(e =>
        {
            e.HasIndex(el => el.ProjectId).HasDatabaseName("idx_event_log_project");
        });

        // Snapshot
        modelBuilder.Entity<Snapshot>(e =>
        {
            e.HasIndex(s => s.ProjectId).HasDatabaseName("idx_snapshot_project");
        });

        // WikiDocument
        modelBuilder.Entity<WikiDocument>(e =>
        {
            e.HasIndex(w => w.ProjectId).HasDatabaseName("idx_wiki_document_project");
        });

        // AuditLog
        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasIndex(a => a.UserId).HasDatabaseName("idx_audit_log_user");
            e.HasIndex(a => a.ProjectId).HasDatabaseName("idx_audit_log_project");
        });

        // InstructionFile
        modelBuilder.Entity<InstructionFile>(e =>
        {
            e.HasIndex(f => f.GroupId).HasDatabaseName("idx_instruction_file_group");
        });

        // ProjectInstruction (composite PK configured via [PrimaryKey] attribute)

        // ChatSpaceLink
        modelBuilder.Entity<ChatSpaceLink>(e =>
        {
            e.HasIndex(l => new { l.Platform, l.SpaceName }).IsUnique()
                .HasDatabaseName("idx_chat_space_link_platform_space");
        });

        // ChatSessionState
        modelBuilder.Entity<ChatSessionState>(e =>
        {
            e.HasIndex(s => s.LinkId).IsUnique();
        });
    }
}
