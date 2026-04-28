using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using AgentPaw.Data;
using AgentPaw.Models;
using AgentPaw.Services;

namespace AgentPaw.ViewModels;

/// <summary>UI에 바인딩할 그룹+페르소나 래퍼</summary>
public class PersonaGroupItem
{
    public string GroupId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "folder";
    public int SortOrder { get; set; }
    public ObservableCollection<Persona> Personas { get; set; } = [];
    public bool IsExpanded { get; set; } = true;
}

/// <summary>연결 다이얼로그용 그룹 묶음 (그룹 일괄 연결 지원)</summary>
public class AvailablePersonaGroup
{
    public string GroupId { get; set; } = string.Empty;
    public string Name { get; set; } = "미분류";
    public ObservableCollection<Persona> Personas { get; set; } = [];
}

/// <summary>지침 연결 다이얼로그용 그룹 묶음</summary>
public class AvailableInstructionGroup
{
    public string GroupId { get; set; } = string.Empty;
    public string Name { get; set; } = "미분류";
    public ObservableCollection<InstructionFileItem> Files { get; set; } = [];
}

public partial class ProjectSettingsViewModel : ObservableObject
{
    private readonly IDbContextFactory<AgentPawDbContext> _dbFactory;
    private readonly InstructionService _instructionService;
    private readonly PersonaService _personaService;
    private readonly ConfigLoaderService _configLoader;
    private readonly GitService _gitService;

    [ObservableProperty] private string _projectId = string.Empty;
    [ObservableProperty] private string _projectName = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    // === 작업 폴더 ===
    [ObservableProperty] private string _workspacePath = string.Empty;
    [ObservableProperty] private string _effectiveWorkspacePath = string.Empty;
    [ObservableProperty] private string? _workspaceSaveMessage;

    // === 대화 설정 ===
    [ObservableProperty] private int _maxDiscussionRounds = 10;
    [ObservableProperty] private int _maxDiscussionParticipants = 4;
    [ObservableProperty] private bool _askUserEnabled = true;
    [ObservableProperty] private string? _discussionSaveMessage;

    // === Git 클론 ===
    [ObservableProperty] private string _cloneUrl = string.Empty;
    [ObservableProperty] private string _cloneToken = string.Empty;
    [ObservableProperty] private string? _cloneMessage;
    [ObservableProperty] private bool _isCloning;

    // 그룹 목록 (그룹별 페르소나 포함)
    public ObservableCollection<PersonaGroupItem> Groups { get; } = [];

    // 미분류 페르소나 (그룹 없음)
    public ObservableCollection<Persona> UngroupedPersonas { get; } = [];

    // Persona 목록 (전체, 하위 호환)
    public ObservableCollection<Persona> Personas { get; } = [];

    // === 그룹 다이얼로그 ===
    [ObservableProperty] private bool _isGroupDialogOpen;
    [ObservableProperty] private bool _isGroupEditMode;
    [ObservableProperty] private string? _editingGroupId;
    [ObservableProperty] private string _groupName = string.Empty;
    [ObservableProperty] private string _groupDescription = string.Empty;
    [ObservableProperty] private string _groupIcon = "folder";

    // === Persona 편집 다이얼로그 ===
    [ObservableProperty] private bool _isPersonaDialogOpen;
    [ObservableProperty] private bool _isPersonaEditMode;
    [ObservableProperty] private string? _editingPersonaId;
    [ObservableProperty] private string? _personaGroupId;
    [ObservableProperty] private string _personaName = string.Empty;
    [ObservableProperty] private string _personaLabel = string.Empty;
    [ObservableProperty] private string _personaDescription = string.Empty;
    [ObservableProperty] private string _personaSystemPrompt = string.Empty;
    [ObservableProperty] private string _personaInstructions = string.Empty;
    [ObservableProperty] private string _personaKeywords = string.Empty;
    [ObservableProperty] private string _personaAvatar = string.Empty;
    [ObservableProperty] private string _personaIcon = "bot";
    [ObservableProperty] private string _personaColor = "blue";
    [ObservableProperty] private string _personaPrimaryModel = "claude-sonnet";
    [ObservableProperty] private string? _personaFallbackModel;
    [ObservableProperty] private float _personaTemperature = 0.7f;
    [ObservableProperty] private int _personaMaxTokens = 4096;

    // Linked instructions
    public ObservableCollection<InstructionFileItem> LinkedInstructions { get; } = [];
    public ObservableCollection<AvailableInstructionGroup> LinkedInstructionGroups { get; } = [];
    public ObservableCollection<InstructionFileItem> LinkedUngroupedFiles { get; } = [];
    public ObservableCollection<InstructionFileItem> AvailableInstructions { get; } = [];
    public ObservableCollection<AvailableInstructionGroup> AvailableInstructionGroups { get; } = [];
    [ObservableProperty] private bool _isLinkDialogOpen;

    // Persona Link (전역 페르소나 → 현 프로젝트 연결)
    public ObservableCollection<Persona> AvailablePersonas { get; } = [];
    public ObservableCollection<AvailablePersonaGroup> AvailablePersonaGroups { get; } = [];
    [ObservableProperty] private bool _isPersonaLinkDialogOpen;

    // Persona-Instruction Link (페르소나별 지침 연결)
    public ObservableCollection<InstructionFileItem> PersonaLinkedInstructions { get; } = [];
    public ObservableCollection<InstructionFileItem> PersonaAvailableInstructions { get; } = [];
    public ObservableCollection<AvailableInstructionGroup> PersonaAvailableInstructionGroups { get; } = [];
    [ObservableProperty] private bool _isPersonaInstructionLinkDialogOpen;

    public ProjectSettingsViewModel(
        IDbContextFactory<AgentPawDbContext> dbFactory,
        InstructionService instructionService,
        PersonaService personaService,
        ConfigLoaderService configLoader,
        GitService gitService)
    {
        _dbFactory = dbFactory;
        _instructionService = instructionService;
        _personaService = personaService;
        _configLoader = configLoader;
        _gitService = gitService;
    }

    /// <summary>빈 문자열이면 null 반환 (DB에 null로 저장)</summary>
    private string? ProjectIdOrNull => string.IsNullOrEmpty(ProjectId) ? null : ProjectId;

    public async Task LoadAsync(string projectId, string projectName)
    {
        ProjectId = projectId;
        ProjectName = projectName;
        await RefreshAsync();
    }

    /// <summary>프로젝트 없이 글로벌 프리셋 로드</summary>
    public async Task LoadPresetsAsync()
    {
        ProjectId = string.Empty;
        ProjectName = string.Empty;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            // 프로젝트가 없으면 글로벌 프리셋(project_id IS NULL) 로드
            var isGlobal = string.IsNullOrEmpty(ProjectId);

            List<Persona> personas;
            List<PersonaGroup> groups;
            if (isGlobal)
            {
                groups = await db.PersonaGroups
                    .Where(g => g.ProjectId == null)
                    .OrderBy(g => g.SortOrder)
                    .ToListAsync();
                personas = await db.Personas
                    .Where(p => p.ProjectId == null)
                    .OrderBy(p => p.SortOrder)
                    .ToListAsync();
            }
            else
            {
                // 프로젝트 모드: project_persona 링크로 연결된 페르소나만 표시
                personas = await _personaService.ListForProjectAsync(ProjectId);

                // 링크된 페르소나들이 속한 전역 그룹 + 이 프로젝트 고유 그룹을 모두 표시
                var usedGroupIds = personas
                    .Where(p => !string.IsNullOrEmpty(p.GroupId))
                    .Select(p => p.GroupId!)
                    .Distinct()
                    .ToList();

                groups = await db.PersonaGroups
                    .Where(g => g.ProjectId == ProjectId || usedGroupIds.Contains(g.GroupId))
                    .OrderBy(g => g.SortOrder)
                    .ToListAsync();
            }

            Groups.Clear();
            UngroupedPersonas.Clear();
            Personas.Clear();

            foreach (var p in personas)
                Personas.Add(p);

            // 그룹별 분류
            var grouped = personas.ToLookup(p => p.GroupId ?? "");

            foreach (var g in groups)
            {
                var item = new PersonaGroupItem
                {
                    GroupId = g.GroupId,
                    Name = g.Name,
                    Description = g.Description,
                    Icon = g.Icon,
                    SortOrder = g.SortOrder
                };
                foreach (var p in grouped[g.GroupId])
                    item.Personas.Add(p);
                Groups.Add(item);
            }

            // 미분류
            foreach (var p in grouped[""])
                UngroupedPersonas.Add(p);

            // Linked instructions (프로젝트가 있을 때만)
            LinkedInstructions.Clear();
            LinkedInstructionGroups.Clear();
            LinkedUngroupedFiles.Clear();
            if (!isGlobal)
            {
                var linked = await _instructionService.ListForProjectAsync(ProjectId);
                foreach (var f in linked)
                    LinkedInstructions.Add(new InstructionFileItem { FileId = f.FileId, Name = f.Name, GroupId = f.GroupId });

                // 그룹별 묶기
                var usedGroupIds = linked
                    .Where(f => !string.IsNullOrEmpty(f.GroupId))
                    .Select(f => f.GroupId!)
                    .Distinct()
                    .ToList();
                var linkedGroups = await db.InstructionGroups
                    .Where(g => usedGroupIds.Contains(g.GroupId))
                    .OrderBy(g => g.Name)
                    .ToListAsync();
                var byGroup = linked.ToLookup(f => f.GroupId ?? "");

                foreach (var g in linkedGroups)
                {
                    var bucket = new AvailableInstructionGroup { GroupId = g.GroupId, Name = g.Name };
                    foreach (var f in byGroup[g.GroupId])
                        bucket.Files.Add(new InstructionFileItem { FileId = f.FileId, Name = f.Name, GroupId = f.GroupId });
                    if (bucket.Files.Count > 0)
                        LinkedInstructionGroups.Add(bucket);
                }
                foreach (var f in byGroup[""])
                    LinkedUngroupedFiles.Add(new InstructionFileItem { FileId = f.FileId, Name = f.Name, GroupId = f.GroupId });

                var project = await db.Projects.FindAsync(ProjectId);
                WorkspacePath = project?.GitRepoPath ?? string.Empty;
                EffectiveWorkspacePath = string.IsNullOrWhiteSpace(WorkspacePath) ? DefaultWorkspacePath(ProjectId) : WorkspacePath;
                WorkspaceSaveMessage = null;
                MaxDiscussionRounds = project?.MaxDiscussionRounds ?? 10;
                MaxDiscussionParticipants = project?.MaxDiscussionParticipants ?? 4;
                AskUserEnabled = project?.AskUserEnabled ?? true;
                DiscussionSaveMessage = null;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    // === Group CRUD ===

    [RelayCommand]
    private void OpenCreateGroup()
    {
        IsGroupEditMode = false;
        EditingGroupId = null;
        GroupName = string.Empty;
        GroupDescription = string.Empty;
        GroupIcon = "folder";
        IsGroupDialogOpen = true;
    }

    [RelayCommand]
    private void OpenEditGroup(PersonaGroupItem group)
    {
        IsGroupEditMode = true;
        EditingGroupId = group.GroupId;
        GroupName = group.Name;
        GroupDescription = group.Description;
        GroupIcon = group.Icon;
        IsGroupDialogOpen = true;
    }

    [RelayCommand]
    private void CloseGroupDialog()
    {
        IsGroupDialogOpen = false;
    }

    [RelayCommand]
    private async Task SaveGroupAsync()
    {
        if (string.IsNullOrWhiteSpace(GroupName)) return;

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            if (IsGroupEditMode && EditingGroupId != null)
            {
                var group = await db.PersonaGroups.FindAsync(EditingGroupId);
                if (group != null)
                {
                    group.Name = GroupName.Trim();
                    group.Description = GroupDescription.Trim();
                    group.Icon = GroupIcon;
                    group.UpdatedAt = DateTimeOffset.UtcNow;
                }
            }
            else
            {
                var pid = ProjectIdOrNull;
                var maxOrder = await db.PersonaGroups
                    .Where(g => pid == null ? g.ProjectId == null : g.ProjectId == pid)
                    .Select(g => (int?)g.SortOrder)
                    .MaxAsync() ?? -1;

                db.PersonaGroups.Add(new PersonaGroup
                {
                    GroupId = Guid.NewGuid().ToString(),
                    ProjectId = pid,
                    Name = GroupName.Trim(),
                    Description = GroupDescription.Trim(),
                    Icon = GroupIcon,
                    SortOrder = maxOrder + 1,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }

            await db.SaveChangesAsync();
            IsGroupDialogOpen = false;
            await RefreshAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.InnerException?.Message ?? ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task DeleteGroupAsync(PersonaGroupItem group)
    {
        IsLoading = true;
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            // 그룹 내 페르소나는 미분류로 이동
            var personas = await db.Personas
                .Where(p => p.GroupId == group.GroupId)
                .ToListAsync();
            foreach (var p in personas)
                p.GroupId = null;

            var g = await db.PersonaGroups.FindAsync(group.GroupId);
            if (g != null)
                db.PersonaGroups.Remove(g);

            await db.SaveChangesAsync();
            await RefreshAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    // === Persona CRUD ===

    [RelayCommand]
    private void OpenCreatePersona(string? groupId = null)
    {
        IsPersonaEditMode = false;
        EditingPersonaId = null;
        PersonaGroupId = groupId;
        PersonaName = string.Empty;
        PersonaLabel = string.Empty;
        PersonaDescription = string.Empty;
        PersonaSystemPrompt = string.Empty;
        PersonaInstructions = string.Empty;
        PersonaKeywords = string.Empty;
        PersonaAvatar = string.Empty;
        PersonaIcon = "bot";
        PersonaColor = "blue";
        PersonaPrimaryModel = "claude-sonnet";
        PersonaFallbackModel = null;
        PersonaTemperature = 0.7f;
        PersonaMaxTokens = 4096;
        IsPersonaDialogOpen = true;
    }

    [RelayCommand]
    private async Task OpenEditPersonaAsync(Persona persona)
    {
        IsPersonaEditMode = true;
        EditingPersonaId = persona.PersonaId;
        PersonaGroupId = persona.GroupId;
        PersonaName = persona.Name;
        PersonaLabel = persona.Label;
        PersonaDescription = persona.Description;
        PersonaSystemPrompt = persona.SystemPrompt;
        PersonaInstructions = persona.Instructions;
        PersonaKeywords = persona.Keywords;
        PersonaAvatar = persona.Avatar;
        PersonaIcon = persona.Icon;
        PersonaColor = persona.Color;
        PersonaPrimaryModel = persona.PrimaryModel;
        PersonaFallbackModel = persona.FallbackModel;
        PersonaTemperature = persona.Temperature;
        PersonaMaxTokens = persona.MaxTokens;
        IsPersonaDialogOpen = true;
        await LoadPersonaLinkedInstructionsAsync(persona.PersonaId);
    }

    private async Task LoadPersonaLinkedInstructionsAsync(string personaId)
    {
        PersonaLinkedInstructions.Clear();
        try
        {
            var linked = await _instructionService.ListForPersonaAsync(personaId);
            foreach (var f in linked)
                PersonaLinkedInstructions.Add(new InstructionFileItem { FileId = f.FileId, Name = f.Name, GroupId = f.GroupId });
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private void ClosePersonaDialog()
    {
        IsPersonaDialogOpen = false;
    }

    [RelayCommand]
    private async Task SavePersonaAsync()
    {
        if (string.IsNullOrWhiteSpace(PersonaName) || string.IsNullOrWhiteSpace(PersonaLabel)) return;

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            if (IsPersonaEditMode && EditingPersonaId != null)
            {
                var persona = await db.Personas.FindAsync(EditingPersonaId);
                if (persona != null)
                {
                    persona.GroupId = PersonaGroupId;
                    persona.Name = PersonaName.Trim();
                    persona.Label = PersonaLabel.Trim();
                    persona.Description = PersonaDescription.Trim();
                    persona.SystemPrompt = PersonaSystemPrompt;
                    persona.Instructions = PersonaInstructions;
                    persona.Keywords = PersonaKeywords;
                    persona.Avatar = PersonaAvatar;
                    persona.Icon = PersonaIcon;
                    persona.Color = PersonaColor;
                    persona.PrimaryModel = PersonaPrimaryModel;
                    persona.FallbackModel = PersonaFallbackModel;
                    persona.Temperature = PersonaTemperature;
                    persona.MaxTokens = PersonaMaxTokens;
                    persona.UpdatedAt = DateTimeOffset.UtcNow;
                }
            }
            else
            {
                var pid = ProjectIdOrNull;
                var maxOrder = await db.Personas
                    .Where(p => pid == null ? p.ProjectId == null : p.ProjectId == pid)
                    .Select(p => (int?)p.SortOrder)
                    .MaxAsync() ?? -1;

                var newPersonaId = Guid.NewGuid().ToString();
                db.Personas.Add(new Persona
                {
                    PersonaId = newPersonaId,
                    ProjectId = pid,
                    GroupId = PersonaGroupId,
                    Name = PersonaName.Trim(),
                    Label = PersonaLabel.Trim(),
                    Description = PersonaDescription.Trim(),
                    SystemPrompt = PersonaSystemPrompt,
                    Instructions = PersonaInstructions,
                    Keywords = PersonaKeywords,
                    Avatar = PersonaAvatar,
                    Icon = PersonaIcon,
                    Color = PersonaColor,
                    SortOrder = maxOrder + 1,
                    PrimaryModel = PersonaPrimaryModel,
                    FallbackModel = PersonaFallbackModel,
                    Temperature = PersonaTemperature,
                    MaxTokens = PersonaMaxTokens,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                });

                // 프로젝트 스코프에서 신규 생성 시 link 테이블에도 동시 등록
                if (pid != null)
                {
                    db.ProjectPersonas.Add(new ProjectPersona
                    {
                        ProjectId = pid,
                        PersonaId = newPersonaId
                    });
                }
            }

            await db.SaveChangesAsync();
            _configLoader.InvalidateAll();
            IsPersonaDialogOpen = false;
            await RefreshAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.InnerException?.Message ?? ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task DeletePersonaAsync(Persona persona)
    {
        IsLoading = true;
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var p = await db.Personas.FindAsync(persona.PersonaId);
            if (p != null)
            {
                db.Personas.Remove(p);
                await db.SaveChangesAsync();
                _configLoader.InvalidateAll();
            }
            await RefreshAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    // === Instruction Linking ===

    [RelayCommand]
    private async Task OpenLinkDialogAsync()
    {
        try
        {
            var allFiles = await _instructionService.ListFilesAsync();
            var linkedIds = LinkedInstructions.Select(l => l.FileId).ToHashSet();
            var available = allFiles
                .Where(f => !linkedIds.Contains(f.FileId))
                .Select(f => new InstructionFileItem { FileId = f.FileId, Name = f.Name, GroupId = f.GroupId })
                .ToList();

            AvailableInstructions.Clear();
            foreach (var f in available) AvailableInstructions.Add(f);

            // 그룹별 묶음 구성
            await using var db = await _dbFactory.CreateDbContextAsync();
            var groups = await db.InstructionGroups.OrderBy(g => g.Name).ToListAsync();
            var byGroup = available.ToLookup(f => f.GroupId ?? "");

            AvailableInstructionGroups.Clear();
            foreach (var g in groups)
            {
                var items = byGroup[g.GroupId].ToList();
                if (items.Count == 0) continue;
                var bucket = new AvailableInstructionGroup { GroupId = g.GroupId, Name = g.Name };
                foreach (var f in items) bucket.Files.Add(f);
                AvailableInstructionGroups.Add(bucket);
            }
            // 미분류 묶음
            var ungrouped = byGroup[""].ToList();
            if (ungrouped.Count > 0)
            {
                var bucket = new AvailableInstructionGroup { GroupId = string.Empty, Name = "미분류" };
                foreach (var f in ungrouped) bucket.Files.Add(f);
                AvailableInstructionGroups.Add(bucket);
            }

            IsLinkDialogOpen = true;
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task LinkInstructionGroupAsync(AvailableInstructionGroup group)
    {
        if (string.IsNullOrEmpty(ProjectId) || group == null) return;
        try
        {
            foreach (var f in group.Files.ToList())
                await _instructionService.LinkToProjectAsync(ProjectId, f.FileId);

            await RefreshAsync();

            AvailableInstructionGroups.Remove(group);
            foreach (var f in group.Files)
            {
                var item = AvailableInstructions.FirstOrDefault(a => a.FileId == f.FileId);
                if (item != null) AvailableInstructions.Remove(item);
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private void CloseLinkDialog()
    {
        IsLinkDialogOpen = false;
    }

    [RelayCommand]
    private async Task LinkInstructionAsync(InstructionFileItem file)
    {
        try
        {
            await _instructionService.LinkToProjectAsync(ProjectId, file.FileId);
            await RefreshAsync();

            var item = AvailableInstructions.FirstOrDefault(a => a.FileId == file.FileId);
            if (item != null) AvailableInstructions.Remove(item);

            foreach (var bucket in AvailableInstructionGroups.ToList())
            {
                var f = bucket.Files.FirstOrDefault(x => x.FileId == file.FileId);
                if (f != null) bucket.Files.Remove(f);
                if (bucket.Files.Count == 0)
                    AvailableInstructionGroups.Remove(bucket);
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task UnlinkInstructionAsync(InstructionFileItem file)
    {
        try
        {
            await _instructionService.UnlinkFromProjectAsync(ProjectId, file.FileId);
            await RefreshAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task UnlinkInstructionGroupAsync(AvailableInstructionGroup group)
    {
        if (string.IsNullOrEmpty(ProjectId) || group == null) return;
        try
        {
            foreach (var f in group.Files.ToList())
                await _instructionService.UnlinkFromProjectAsync(ProjectId, f.FileId);
            await RefreshAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    // === Persona Linking (전역 페르소나 → 현 프로젝트 연결) ===

    [RelayCommand]
    private async Task OpenPersonaLinkDialogAsync()
    {
        if (string.IsNullOrEmpty(ProjectId)) return;
        try
        {
            var allGlobals = await _personaService.ListGlobalAsync();
            var linkedIds = Personas.Select(p => p.PersonaId).ToHashSet();
            var available = allGlobals.Where(p => !linkedIds.Contains(p.PersonaId)).ToList();

            AvailablePersonas.Clear();
            foreach (var p in available)
                AvailablePersonas.Add(p);

            // 그룹별 묶음 구성 (그룹 일괄 연결용)
            await using var db = await _dbFactory.CreateDbContextAsync();
            var groups = await db.PersonaGroups
                .Where(g => g.ProjectId == null)
                .OrderBy(g => g.SortOrder)
                .ToListAsync();
            var byGroup = available.ToLookup(p => p.GroupId ?? "");

            AvailablePersonaGroups.Clear();
            foreach (var g in groups)
            {
                var items = byGroup[g.GroupId].ToList();
                if (items.Count == 0) continue;
                var bucket = new AvailablePersonaGroup { GroupId = g.GroupId, Name = g.Name };
                foreach (var p in items) bucket.Personas.Add(p);
                AvailablePersonaGroups.Add(bucket);
            }
            // 미분류 묶음
            var ungrouped = byGroup[""].ToList();
            if (ungrouped.Count > 0)
            {
                var bucket = new AvailablePersonaGroup { GroupId = string.Empty, Name = "미분류" };
                foreach (var p in ungrouped) bucket.Personas.Add(p);
                AvailablePersonaGroups.Add(bucket);
            }

            IsPersonaLinkDialogOpen = true;
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task LinkPersonaGroupAsync(AvailablePersonaGroup group)
    {
        if (string.IsNullOrEmpty(ProjectId) || group == null) return;
        try
        {
            foreach (var p in group.Personas.ToList())
                await _personaService.LinkToProjectAsync(ProjectId, p.PersonaId);

            await RefreshAsync();

            // 다이얼로그 컬렉션에서 묶음 제거
            AvailablePersonaGroups.Remove(group);
            foreach (var p in group.Personas)
            {
                var item = AvailablePersonas.FirstOrDefault(a => a.PersonaId == p.PersonaId);
                if (item != null) AvailablePersonas.Remove(item);
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private void ClosePersonaLinkDialog()
    {
        IsPersonaLinkDialogOpen = false;
    }

    [RelayCommand]
    private async Task LinkPersonaAsync(Persona persona)
    {
        if (string.IsNullOrEmpty(ProjectId)) return;
        try
        {
            await _personaService.LinkToProjectAsync(ProjectId, persona.PersonaId);
            await RefreshAsync();

            var item = AvailablePersonas.FirstOrDefault(a => a.PersonaId == persona.PersonaId);
            if (item != null) AvailablePersonas.Remove(item);

            foreach (var bucket in AvailablePersonaGroups.ToList())
            {
                var p = bucket.Personas.FirstOrDefault(x => x.PersonaId == persona.PersonaId);
                if (p != null) bucket.Personas.Remove(p);
                if (bucket.Personas.Count == 0)
                    AvailablePersonaGroups.Remove(bucket);
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task UnlinkPersonaAsync(Persona persona)
    {
        if (string.IsNullOrEmpty(ProjectId)) return;
        try
        {
            await _personaService.UnlinkFromProjectAsync(ProjectId, persona.PersonaId);
            await RefreshAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task UnlinkPersonaGroupAsync(PersonaGroupItem group)
    {
        if (string.IsNullOrEmpty(ProjectId) || group == null) return;
        try
        {
            foreach (var p in group.Personas.ToList())
                await _personaService.UnlinkFromProjectAsync(ProjectId, p.PersonaId);
            await RefreshAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    // === Persona-Instruction Linking ===

    [RelayCommand]
    private async Task OpenPersonaInstructionLinkDialogAsync()
    {
        if (string.IsNullOrEmpty(EditingPersonaId)) return;
        try
        {
            var allFiles = await _instructionService.ListFilesAsync();
            var linkedIds = PersonaLinkedInstructions.Select(l => l.FileId).ToHashSet();
            var available = allFiles
                .Where(f => !linkedIds.Contains(f.FileId))
                .Select(f => new InstructionFileItem { FileId = f.FileId, Name = f.Name, GroupId = f.GroupId })
                .ToList();

            PersonaAvailableInstructions.Clear();
            foreach (var f in available) PersonaAvailableInstructions.Add(f);

            await using var db = await _dbFactory.CreateDbContextAsync();
            var groups = await db.InstructionGroups.OrderBy(g => g.Name).ToListAsync();
            var byGroup = available.ToLookup(f => f.GroupId ?? "");

            PersonaAvailableInstructionGroups.Clear();
            foreach (var g in groups)
            {
                var items = byGroup[g.GroupId].ToList();
                if (items.Count == 0) continue;
                var bucket = new AvailableInstructionGroup { GroupId = g.GroupId, Name = g.Name };
                foreach (var f in items) bucket.Files.Add(f);
                PersonaAvailableInstructionGroups.Add(bucket);
            }
            var ungrouped = byGroup[""].ToList();
            if (ungrouped.Count > 0)
            {
                var bucket = new AvailableInstructionGroup { GroupId = string.Empty, Name = "미분류" };
                foreach (var f in ungrouped) bucket.Files.Add(f);
                PersonaAvailableInstructionGroups.Add(bucket);
            }

            IsPersonaInstructionLinkDialogOpen = true;
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private void ClosePersonaInstructionLinkDialog()
    {
        IsPersonaInstructionLinkDialogOpen = false;
    }

    [RelayCommand]
    private async Task LinkInstructionToPersonaAsync(InstructionFileItem file)
    {
        if (string.IsNullOrEmpty(EditingPersonaId) || file == null) return;
        try
        {
            await _instructionService.LinkToPersonaAsync(EditingPersonaId, file.FileId);
            await LoadPersonaLinkedInstructionsAsync(EditingPersonaId);

            var item = PersonaAvailableInstructions.FirstOrDefault(a => a.FileId == file.FileId);
            if (item != null) PersonaAvailableInstructions.Remove(item);

            foreach (var bucket in PersonaAvailableInstructionGroups.ToList())
            {
                var f = bucket.Files.FirstOrDefault(x => x.FileId == file.FileId);
                if (f != null) bucket.Files.Remove(f);
                if (bucket.Files.Count == 0)
                    PersonaAvailableInstructionGroups.Remove(bucket);
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task LinkInstructionGroupToPersonaAsync(AvailableInstructionGroup group)
    {
        if (string.IsNullOrEmpty(EditingPersonaId) || group == null) return;
        try
        {
            foreach (var f in group.Files.ToList())
                await _instructionService.LinkToPersonaAsync(EditingPersonaId, f.FileId);

            await LoadPersonaLinkedInstructionsAsync(EditingPersonaId);

            PersonaAvailableInstructionGroups.Remove(group);
            foreach (var f in group.Files)
            {
                var item = PersonaAvailableInstructions.FirstOrDefault(a => a.FileId == f.FileId);
                if (item != null) PersonaAvailableInstructions.Remove(item);
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task UnlinkInstructionFromPersonaAsync(InstructionFileItem file)
    {
        if (string.IsNullOrEmpty(EditingPersonaId) || file == null) return;
        try
        {
            await _instructionService.UnlinkFromPersonaAsync(EditingPersonaId, file.FileId);
            await LoadPersonaLinkedInstructionsAsync(EditingPersonaId);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    // === Workspace folder ===

    [RelayCommand]
    private async Task SaveDiscussionSettingsAsync()
    {
        if (string.IsNullOrEmpty(ProjectId)) return;

        IsLoading = true;
        DiscussionSaveMessage = null;
        ErrorMessage = null;
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var project = await db.Projects.FindAsync(ProjectId);
            if (project == null) { ErrorMessage = "프로젝트를 찾지 못했다."; return; }

            project.MaxDiscussionRounds = Math.Clamp(MaxDiscussionRounds, 1, 10);
            project.MaxDiscussionParticipants = Math.Clamp(MaxDiscussionParticipants, 2, 8);
            project.AskUserEnabled = AskUserEnabled;
            project.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();

            DiscussionSaveMessage = "저장됨";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task SaveWorkspacePathAsync()
    {
        if (string.IsNullOrEmpty(ProjectId)) return;

        IsLoading = true;
        ErrorMessage = null;
        WorkspaceSaveMessage = null;
        try
        {
            var trimmed = (WorkspacePath ?? string.Empty).Trim();
            if (trimmed.Length > 0 && !System.IO.Path.IsPathRooted(trimmed))
            {
                ErrorMessage = "작업 폴더는 절대 경로여야 한다.";
                return;
            }

            await using var db = await _dbFactory.CreateDbContextAsync();
            var project = await db.Projects.FindAsync(ProjectId);
            if (project == null)
            {
                ErrorMessage = "프로젝트를 찾지 못했다.";
                return;
            }

            project.GitRepoPath = trimmed;
            project.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();

            if (trimmed.Length > 0)
                System.IO.Directory.CreateDirectory(trimmed);

            EffectiveWorkspacePath = trimmed.Length == 0 ? DefaultWorkspacePath(ProjectId) : trimmed;
            WorkspaceSaveMessage = $"저장됨 — {EffectiveWorkspacePath}";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void OpenWorkspaceFolder()
    {
        try
        {
            var path = string.IsNullOrWhiteSpace(EffectiveWorkspacePath)
                ? DefaultWorkspacePath(ProjectId)
                : EffectiveWorkspacePath;
            System.IO.Directory.CreateDirectory(path);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    // === Git 클론 ===

    [RelayCommand]
    private async Task CloneRepoAsync()
    {
        if (string.IsNullOrEmpty(ProjectId)) return;
        var url = CloneUrl.Trim();
        if (string.IsNullOrWhiteSpace(url)) { CloneMessage = "레포 URL을 입력하라."; return; }

        IsCloning = true;
        CloneMessage = "클론 중…";
        ErrorMessage = null;
        try
        {
            var targetPath = System.IO.Path.Combine(DefaultWorkspacePath(ProjectId), "repo");
            var token = string.IsNullOrWhiteSpace(CloneToken) ? null : CloneToken.Trim();
            var prog = new Progress<string>(msg => CloneMessage = msg);

            await _gitService.CloneAsync(url, targetPath, token, prog);

            await using var db = await _dbFactory.CreateDbContextAsync();
            var project = await db.Projects.FindAsync(ProjectId);
            if (project != null)
            {
                project.GitRepoPath = targetPath;
                project.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync();
            }

            WorkspacePath = targetPath;
            EffectiveWorkspacePath = targetPath;
            CloneMessage = $"클론 완료 — {targetPath}";
        }
        catch (Exception ex)
        {
            CloneMessage = null;
            ErrorMessage = ex.Message;
        }
        finally { IsCloning = false; }
    }

    public static string DefaultWorkspacePath(string projectId)
        => System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AgentPaw", "repos", projectId);
}
