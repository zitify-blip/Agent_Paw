using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgentPaw.Models;
using AgentPaw.Orchestrator;
using AgentPaw.Services;

namespace AgentPaw.ViewModels;

public partial class WikiViewModel : ObservableObject
{
    private readonly WikiService _wikiService;
    private readonly OrchestratorService _orchestrator;

    private List<WikiNode> _rootNodes = [];
    private string? _createParentId;

    [ObservableProperty] private string _projectId = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isConsolidating;
    [ObservableProperty] private string _consolidationStatus = string.Empty;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string _searchQuery = string.Empty;

    [ObservableProperty] private WikiNode? _selectedNode;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private bool _isTreeEmpty = true;
    [ObservableProperty] private bool _hasSelectedNode;

    // Create dialog
    [ObservableProperty] private bool _isCreateDialogOpen;
    [ObservableProperty] private string _newTitle = string.Empty;
    [ObservableProperty] private string _newContent = string.Empty;
    [ObservableProperty] private string _createDialogTitle = "새 페이지";

    // Edit fields
    [ObservableProperty] private string _editTitle = string.Empty;
    [ObservableProperty] private string _editContent = string.Empty;

    public ObservableCollection<WikiNode> FlatNodes { get; } = [];

    public WikiViewModel(WikiService wikiService, OrchestratorService orchestrator)
    {
        _wikiService = wikiService;
        _orchestrator = orchestrator;
    }

    partial void OnSelectedNodeChanged(WikiNode? value)
    {
        HasSelectedNode = value != null;
        IsEditing = false;
    }

    public async Task LoadAsync(string projectId)
    {
        ProjectId = projectId;
        SelectedNode = null;
        SearchQuery = string.Empty;
        await RefreshTreeAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var selectedId = SelectedNode?.WikiId;
        await RefreshTreeAsync();
        if (selectedId != null)
        {
            var node = FindNode(_rootNodes, selectedId);
            if (node != null) SelectNodeInternal(node);
        }
    }

    private async Task RefreshTreeAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var docs = await _wikiService.ListWikisAsync(ProjectId);
            BuildTree(docs);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.InnerException?.Message ?? ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void BuildTree(List<WikiDocument> docs)
    {
        var nodeMap = docs.ToDictionary(d => d.WikiId, d => new WikiNode(d));

        _rootNodes.Clear();
        foreach (var doc in docs)
        {
            if (doc.ParentId != null && nodeMap.TryGetValue(doc.ParentId, out var parent))
                parent.Children.Add(nodeMap[doc.WikiId]);
            else
                _rootNodes.Add(nodeMap[doc.WikiId]);
        }

        foreach (var root in _rootNodes)
            SetDepthRecursive(root, 0);

        RebuildFlatList();
    }

    private void SetDepthRecursive(WikiNode node, int depth)
    {
        node.Depth = depth;
        foreach (var child in node.Children)
            SetDepthRecursive(child, depth + 1);
    }

    private void RebuildFlatList()
    {
        FlatNodes.Clear();
        foreach (var root in _rootNodes)
            AddNodeRecursive(root);
        IsTreeEmpty = FlatNodes.Count == 0;
    }

    private void AddNodeRecursive(WikiNode node)
    {
        FlatNodes.Add(node);
        if (node.IsExpanded)
            foreach (var child in node.Children)
                AddNodeRecursive(child);
    }

    private WikiNode? FindNode(List<WikiNode> nodes, string wikiId)
    {
        foreach (var node in nodes)
        {
            if (node.WikiId == wikiId) return node;
            var found = FindNode(node.Children, wikiId);
            if (found != null) return found;
        }
        return null;
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            RebuildFlatList();
        }
        else
        {
            IsLoading = true;
            ErrorMessage = null;
            try
            {
                var results = await _wikiService.SearchWikisAsync(ProjectId, SearchQuery.Trim());
                FlatNodes.Clear();
                foreach (var doc in results)
                    FlatNodes.Add(new WikiNode(doc));
                IsTreeEmpty = FlatNodes.Count == 0;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.InnerException?.Message ?? ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand]
    private void ToggleExpand(WikiNode node)
    {
        node.IsExpanded = !node.IsExpanded;
        RebuildFlatList();
        // Re-apply selection marker if needed
        if (SelectedNode != null)
            SelectedNode.IsSelected = true;
    }

    [RelayCommand]
    private void SelectNode(WikiNode node) => SelectNodeInternal(node);

    private void SelectNodeInternal(WikiNode node)
    {
        if (SelectedNode != null) SelectedNode.IsSelected = false;
        SelectedNode = node;
        node.IsSelected = true;
        IsEditing = false;
    }

    [RelayCommand]
    private void OpenCreateRootDialog()
    {
        _createParentId = null;
        CreateDialogTitle = "새 루트 페이지";
        NewTitle = string.Empty;
        NewContent = string.Empty;
        IsCreateDialogOpen = true;
    }

    [RelayCommand]
    private void OpenCreateChildDialog()
    {
        if (SelectedNode == null) return;
        _createParentId = SelectedNode.WikiId;
        CreateDialogTitle = $"하위 페이지 추가 — {SelectedNode.Title}";
        NewTitle = string.Empty;
        NewContent = string.Empty;
        IsCreateDialogOpen = true;
    }

    [RelayCommand]
    private void CloseCreateDialog()
    {
        IsCreateDialogOpen = false;
    }

    [RelayCommand]
    private async Task CreateWikiAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTitle)) return;
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var created = await _wikiService.CreateWikiAsync(
                ProjectId, "WIKI", NewTitle.Trim(), NewContent.Trim(),
                parentId: _createParentId);
            IsCreateDialogOpen = false;
            var docs = await _wikiService.ListWikisAsync(ProjectId);
            BuildTree(docs);
            var node = FindNode(_rootNodes, created.WikiId);
            if (node != null) SelectNodeInternal(node);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.InnerException?.Message ?? ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void StartEdit()
    {
        if (SelectedNode == null) return;
        EditTitle = SelectedNode.Document.Title;
        EditContent = SelectedNode.Document.Content;
        IsEditing = true;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
    }

    [RelayCommand]
    private async Task SaveEditAsync()
    {
        if (SelectedNode == null || string.IsNullOrWhiteSpace(EditTitle)) return;
        var wikiId = SelectedNode.WikiId;
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            await _wikiService.UpdateWikiAsync(wikiId, EditTitle.Trim(), EditContent.Trim(), null);
            IsEditing = false;
            var docs = await _wikiService.ListWikisAsync(ProjectId);
            BuildTree(docs);
            var node = FindNode(_rootNodes, wikiId);
            if (node != null) SelectNodeInternal(node);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.InnerException?.Message ?? ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DeleteNodeAsync()
    {
        if (SelectedNode == null) return;
        var wikiId = SelectedNode.WikiId;
        SelectedNode = null;
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            await _wikiService.DeleteWithChildrenAsync(wikiId);
            var docs = await _wikiService.ListWikisAsync(ProjectId);
            BuildTree(docs);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.InnerException?.Message ?? ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ConsolidateWikiAsync()
    {
        if (IsConsolidating) return;
        IsConsolidating = true;
        ConsolidationStatus = "준비 중...";
        ErrorMessage = null;
        try
        {
            var progress = new Progress<string>(s => ConsolidationStatus = s);
            await _orchestrator.ConsolidateWikiAsync(ProjectId, progress);
            ConsolidationStatus = "위키 트리 갱신 중...";
            await RefreshTreeAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.InnerException?.Message ?? ex.Message;
        }
        finally
        {
            IsConsolidating = false;
            ConsolidationStatus = string.Empty;
        }
    }
}
