using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgentPaw.Models;
using AgentPaw.Services;

namespace AgentPaw.ViewModels;

public partial class WikiViewModel : ObservableObject
{
    private readonly WikiService _wikiService;

    [ObservableProperty] private string _projectId = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private string _activeCategory = "ALL";

    // 목록/상세/편집 상태
    [ObservableProperty] private WikiDocument? _selectedWiki;
    [ObservableProperty] private bool _isDetailView;
    [ObservableProperty] private bool _isEditing;

    // 생성 다이얼로그
    [ObservableProperty] private bool _isCreateDialogOpen;
    [ObservableProperty] private string _newTitle = string.Empty;
    [ObservableProperty] private string _newContent = string.Empty;
    [ObservableProperty] private string _newCategory = "WIKI_ADR";

    // 편집 필드
    [ObservableProperty] private string _editTitle = string.Empty;
    [ObservableProperty] private string _editContent = string.Empty;
    [ObservableProperty] private string _editCategory = string.Empty;

    public ObservableCollection<WikiDocument> Wikis { get; } = [];

    // 카테고리 카운트
    [ObservableProperty] private int _allCount;
    [ObservableProperty] private int _adrCount;
    [ObservableProperty] private int _specCount;
    [ObservableProperty] private int _troubleCount;

    public WikiViewModel(WikiService wikiService)
    {
        _wikiService = wikiService;
    }

    public async Task LoadAsync(string projectId)
    {
        ProjectId = projectId;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            List<WikiDocument> wikis;
            if (!string.IsNullOrWhiteSpace(SearchQuery))
                wikis = await _wikiService.SearchWikisAsync(ProjectId, SearchQuery.Trim());
            else
                wikis = await _wikiService.ListWikisAsync(ProjectId);

            // 카테고리 카운트 (검색 전 전체 기준)
            var all = string.IsNullOrWhiteSpace(SearchQuery)
                ? wikis
                : await _wikiService.ListWikisAsync(ProjectId);
            AllCount = all.Count;
            AdrCount = all.Count(w => w.Category == "WIKI_ADR");
            SpecCount = all.Count(w => w.Category == "WIKI_SPEC");
            TroubleCount = all.Count(w => w.Category == "WIKI_TROUBLE");

            // 카테고리 필터 적용
            if (ActiveCategory != "ALL")
                wikis = wikis.Where(w => w.Category == ActiveCategory).ToList();

            Wikis.Clear();
            foreach (var w in wikis)
                Wikis.Add(w);
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
    private async Task SearchAsync()
    {
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task SetCategoryFilterAsync(string category)
    {
        ActiveCategory = category;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task SelectWikiAsync(WikiDocument wiki)
    {
        IsLoading = true;
        try
        {
            var detail = await _wikiService.GetWikiAsync(wiki.WikiId);
            if (detail != null)
            {
                SelectedWiki = detail;
                IsDetailView = true;
                IsEditing = false;
            }
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
    private void BackToList()
    {
        SelectedWiki = null;
        IsDetailView = false;
        IsEditing = false;
    }

    [RelayCommand]
    private void OpenCreateDialog()
    {
        NewTitle = string.Empty;
        NewContent = string.Empty;
        NewCategory = "WIKI_ADR";
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
        if (string.IsNullOrWhiteSpace(NewTitle) || string.IsNullOrWhiteSpace(NewContent)) return;

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            await _wikiService.CreateWikiAsync(ProjectId, NewCategory, NewTitle.Trim(), NewContent.Trim());
            IsCreateDialogOpen = false;
            await RefreshAsync();
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
        if (SelectedWiki == null) return;
        EditTitle = SelectedWiki.Title;
        EditContent = SelectedWiki.Content;
        EditCategory = SelectedWiki.Category;
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
        if (SelectedWiki == null || string.IsNullOrWhiteSpace(EditTitle) || string.IsNullOrWhiteSpace(EditContent))
            return;

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            await _wikiService.UpdateWikiAsync(SelectedWiki.WikiId, EditTitle.Trim(), EditContent.Trim(), EditCategory);
            // 상세 다시 로드
            var updated = await _wikiService.GetWikiAsync(SelectedWiki.WikiId);
            if (updated != null)
                SelectedWiki = updated;
            IsEditing = false;
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
