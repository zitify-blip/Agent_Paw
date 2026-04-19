using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgentPaw.Models;
using AgentPaw.Services;

namespace AgentPaw.ViewModels;

public partial class InstructionsViewModel : ObservableObject
{
    private readonly InstructionService _instructionService;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    // 그룹 목록
    public ObservableCollection<InstructionGroupItem> Groups { get; } = [];
    // 미분류 파일
    public ObservableCollection<InstructionFileItem> UngroupedFiles { get; } = [];

    // 파일 상세/프리뷰
    [ObservableProperty] private InstructionFileItem? _selectedFile;
    [ObservableProperty] private string _previewContent = string.Empty;
    [ObservableProperty] private bool _isPreviewOpen;

    // 그룹 생성 다이얼로그
    [ObservableProperty] private bool _isGroupDialogOpen;
    [ObservableProperty] private bool _isGroupEditMode;
    [ObservableProperty] private string _groupDialogName = string.Empty;
    [ObservableProperty] private string? _editingGroupId;

    // 파일 생성 다이얼로그
    [ObservableProperty] private bool _isFileDialogOpen;
    [ObservableProperty] private string _fileDialogName = string.Empty;
    [ObservableProperty] private string _fileDialogContent = string.Empty;
    [ObservableProperty] private string? _fileDialogGroupId;

    public InstructionsViewModel(InstructionService instructionService)
    {
        _instructionService = instructionService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var groups = await _instructionService.ListGroupsAsync();
            var files = await _instructionService.ListFilesAsync();

            Groups.Clear();
            foreach (var g in groups)
            {
                var groupFiles = files.Where(f => f.GroupId == g.GroupId).ToList();
                var item = new InstructionGroupItem
                {
                    GroupId = g.GroupId,
                    Name = g.Name,
                    Description = g.Description,
                    FileCount = groupFiles.Count
                };
                foreach (var f in groupFiles)
                    item.Files.Add(new InstructionFileItem { FileId = f.FileId, Name = f.Name, GroupId = f.GroupId });
                Groups.Add(item);
            }

            UngroupedFiles.Clear();
            foreach (var f in files.Where(f => string.IsNullOrEmpty(f.GroupId)))
                UngroupedFiles.Add(new InstructionFileItem { FileId = f.FileId, Name = f.Name });
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
        GroupDialogName = string.Empty;
        EditingGroupId = null;
        IsGroupDialogOpen = true;
    }

    [RelayCommand]
    private void OpenEditGroup(InstructionGroupItem group)
    {
        IsGroupEditMode = true;
        GroupDialogName = group.Name;
        EditingGroupId = group.GroupId;
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
        if (string.IsNullOrWhiteSpace(GroupDialogName)) return;
        IsLoading = true;
        try
        {
            if (IsGroupEditMode && EditingGroupId != null)
                await _instructionService.UpdateGroupAsync(EditingGroupId, GroupDialogName.Trim());
            else
                await _instructionService.CreateGroupAsync(GroupDialogName.Trim());

            IsGroupDialogOpen = false;
            await LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task DeleteGroupAsync(InstructionGroupItem group)
    {
        IsLoading = true;
        try
        {
            await _instructionService.DeleteGroupAsync(group.GroupId);
            await LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    // === File CRUD ===

    [RelayCommand]
    private void OpenCreateFile(string? groupId)
    {
        FileDialogName = string.Empty;
        FileDialogContent = string.Empty;
        FileDialogGroupId = groupId;
        IsFileDialogOpen = true;
    }

    [RelayCommand]
    private void CloseFileDialog()
    {
        IsFileDialogOpen = false;
    }

    [RelayCommand]
    private async Task SaveFileAsync()
    {
        if (string.IsNullOrWhiteSpace(FileDialogName)) return;
        IsLoading = true;
        try
        {
            await _instructionService.CreateFileAsync(FileDialogName.Trim(), FileDialogContent, FileDialogGroupId);
            IsFileDialogOpen = false;
            await LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task DeleteFileAsync(InstructionFileItem file)
    {
        IsLoading = true;
        try
        {
            await _instructionService.DeleteFileAsync(file.FileId);
            await LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task PreviewFileAsync(InstructionFileItem file)
    {
        try
        {
            var content = await _instructionService.GetFileContentAsync(file.FileId);
            PreviewContent = content ?? "(내용 없음)";
            SelectedFile = file;
            IsPreviewOpen = true;
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private void ClosePreview()
    {
        IsPreviewOpen = false;
        SelectedFile = null;
    }

    public async Task UploadFilesAsync(string[] filePaths, string? groupId = null)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            await _instructionService.UploadFilesAsync(filePaths, groupId);
            await LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    public async Task MoveFileToGroupAsync(string fileId, string? groupId)
    {
        try
        {
            if (groupId == null)
                await _instructionService.UpdateFileAsync(fileId, clearGroup: true);
            else
                await _instructionService.UpdateFileAsync(fileId, groupId: groupId);
            await LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }
}

public class InstructionGroupItem : ObservableObject
{
    public string GroupId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int FileCount { get; set; }
    public ObservableCollection<InstructionFileItem> Files { get; } = [];

    private bool _isExpanded = true;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }
}

public class InstructionFileItem
{
    public string FileId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? GroupId { get; set; }
}
