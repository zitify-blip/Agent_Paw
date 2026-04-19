using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using AgentPaw.Models;
using AgentPaw.Services;
using AgentPaw.ViewModels;
using Microsoft.Win32;

namespace AgentPaw.Views.Pages;

public partial class PersonaPage : UserControl
{
    private ProjectSettingsViewModel? _vm;
    private ProjectService? _projectService;
    private AuthService? _authService;
    private string _pendingAvatarPath = string.Empty;
    private string? _pendingGroupId;

    private static readonly List<ModelOption> AllModels =
    [
        new("claude-sonnet", "Claude Sonnet 4.6"),
        new("claude-opus", "Claude Opus 4.6"),
        new("claude-haiku", "Claude Haiku 4.5"),
        new("gemini-pro", "Gemini 2.5 Pro"),
        new("gemini-flash", "Gemini 2.5 Flash"),
        new("gemini-flash-lite", "Gemini 2.0 Flash Lite"),
    ];

    private static readonly List<ModelOption> FallbackModels =
        [new(null, "(없음)"), .. AllModels];

    private static readonly string AvatarsDir = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "Assets", "Avatars");

    public PersonaPage()
    {
        InitializeComponent();
    }

    public void Initialize(ProjectSettingsViewModel vm, ProjectService projectService, AuthService authService)
    {
        _vm = vm;
        _projectService = projectService;
        _authService = authService;
        DataContext = vm;
    }

    private async void PersonaPage_Loaded(object sender, RoutedEventArgs e)
    {
        AvatarTemplateGallery.ItemsSource = EngineAvatarService.GetTemplates();

        PersonaModelCombo.DisplayMemberPath = "DisplayName";
        PersonaModelCombo.ItemsSource = AllModels;
        PersonaModelCombo.SelectedIndex = 0;

        PersonaFallbackCombo.DisplayMemberPath = "DisplayName";
        PersonaFallbackCombo.ItemsSource = FallbackModels;
        PersonaFallbackCombo.SelectedIndex = 0;

        await LoadScopesAsync();
    }

    private async Task LoadScopesAsync()
    {
        if (_projectService == null || _authService?.CurrentUserId == null) return;

        var items = new List<ScopeItem> { new() { ProjectId = null, DisplayName = "글로벌 프리셋" } };
        var projects = await _projectService.ListProjectsForUserAsync(_authService.CurrentUserId, "ACTIVE");
        foreach (var p in projects)
            items.Add(new ScopeItem { ProjectId = p.ProjectId, DisplayName = p.ProjectName });

        ScopeCombo.DisplayMemberPath = "DisplayName";
        ScopeCombo.ItemsSource = items;
        ScopeCombo.SelectedIndex = 0; // 기본: 글로벌 프리셋
    }

    private async void ScopeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_vm == null || ScopeCombo.SelectedItem is not ScopeItem scope) return;

        if (scope.ProjectId == null)
            await _vm.LoadPresetsAsync();
        else
            await _vm.LoadAsync(scope.ProjectId, scope.DisplayName);

        // 글로벌 프리셋일 때만 "기본 템플릿 다시 불러오기" 버튼 노출
        ReseedTemplatesBtn.Visibility = scope.ProjectId == null
            ? Visibility.Visible : Visibility.Collapsed;

        UpdateUI();
    }

    private async void ReseedTemplates_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        var result = MessageBox.Show(
            "기존 빌트인 전역 페르소나를 삭제하고 최신 템플릿으로 다시 시드한다.\n사용자가 직접 추가한 페르소나는 영향을 받지 않는다.\n진행한다?",
            "템플릿 재시드", MessageBoxButton.YesNo);
        if (result != MessageBoxResult.Yes) return;

        try
        {
            var personaService = App.GetService<PersonaService>();
            await personaService.SeedGlobalTemplatesAsync(overwrite: true);
            await _vm.LoadPresetsAsync();
            UpdateUI();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"재시드 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateUI()
    {
        if (_vm == null) return;

        GroupsList.ItemsSource = _vm.Groups;
        UngroupedList.ItemsSource = _vm.UngroupedPersonas;

        UngroupedSection.Visibility = _vm.UngroupedPersonas.Count > 0
            ? Visibility.Visible : Visibility.Collapsed;

        var hasAny = _vm.Groups.Count > 0 || _vm.UngroupedPersonas.Count > 0;
        NoPersonasText.Visibility = hasAny ? Visibility.Collapsed : Visibility.Visible;

        if (!string.IsNullOrEmpty(_vm.ErrorMessage))
        {
            ErrorText.Text = _vm.ErrorMessage;
            ErrorText.Visibility = Visibility.Visible;
        }
        else ErrorText.Visibility = Visibility.Collapsed;
    }

    // === Group CRUD ===

    private void CreateGroup_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        _vm.OpenCreateGroupCommand.Execute(null);
        GroupDialogTitle.Text = "새 그룹";
        GroupNameInput.Text = string.Empty;
        GroupDescInput.Text = string.Empty;
        GroupDialogOverlay.Visibility = Visibility.Visible;
    }

    private void EditGroup_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null || sender is not FrameworkElement { Tag: PersonaGroupItem group }) return;
        _vm.OpenEditGroupCommand.Execute(group);
        GroupDialogTitle.Text = "그룹 편집";
        GroupNameInput.Text = _vm.GroupName;
        GroupDescInput.Text = _vm.GroupDescription;
        GroupDialogOverlay.Visibility = Visibility.Visible;
    }

    private async void DeleteGroup_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null || sender is not FrameworkElement { Tag: PersonaGroupItem group }) return;
        var result = MessageBox.Show(
            $"'{group.Name}' 그룹을 삭제하시겠습니까?\n그룹 내 페르소나는 미분류로 이동됩니다.",
            "삭제 확인", MessageBoxButton.YesNo);
        if (result != MessageBoxResult.Yes) return;
        await _vm.DeleteGroupCommand.ExecuteAsync(group);
        UpdateUI();
    }

    private void GroupDialogCancel_Click(object sender, RoutedEventArgs e)
    {
        GroupDialogOverlay.Visibility = Visibility.Collapsed;
    }

    private async void GroupDialogSave_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        _vm.GroupName = GroupNameInput.Text;
        _vm.GroupDescription = GroupDescInput.Text;
        await _vm.SaveGroupCommand.ExecuteAsync(null);
        GroupDialogOverlay.Visibility = Visibility.Collapsed;
        UpdateUI();
    }

    private void GroupDialogOverlay_Click(object sender, MouseButtonEventArgs e)
    {
        GroupDialogOverlay.Visibility = Visibility.Collapsed;
    }

    // === Persona CRUD ===

    private void CreatePersonaUngrouped_Click(object sender, RoutedEventArgs e)
    {
        OpenPersonaDialog(null);
    }

    private void CreatePersonaInGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: PersonaGroupItem group }) return;
        OpenPersonaDialog(group.GroupId);
    }

    private void OpenPersonaDialog(string? groupId)
    {
        if (_vm == null) return;
        _pendingGroupId = groupId;
        _vm.OpenCreatePersonaCommand.Execute(groupId);
        PersonaDialogTitle.Text = "새 페르소나";
        ClearPersonaFields();
        PopulateGroupCombo(groupId);
        PersonaLinkedInstructionsSection.Visibility = Visibility.Collapsed;
        PersonaDialogOverlay.Visibility = Visibility.Visible;
    }

    private void EditPersona_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null || sender is not FrameworkElement { Tag: Persona persona }) return;
        _vm.OpenEditPersonaCommand.Execute(persona);
        _pendingGroupId = persona.GroupId;
        PersonaDialogTitle.Text = "페르소나 편집";
        PersonaNameInput.Text = _vm.PersonaName;
        PersonaLabelInput.Text = _vm.PersonaLabel;
        PersonaDescInput.Text = _vm.PersonaDescription;
        PersonaPromptInput.Text = _vm.PersonaSystemPrompt;
        PersonaInstructionsInput.Text = _vm.PersonaInstructions;
        PersonaKeywordsInput.Text = _vm.PersonaKeywords;
        SelectModel(PersonaModelCombo, AllModels, _vm.PersonaPrimaryModel);
        SelectModel(PersonaFallbackCombo, FallbackModels, _vm.PersonaFallbackModel);
        PersonaTempInput.Text = _vm.PersonaTemperature.ToString("F1");
        PersonaTokensInput.Text = _vm.PersonaMaxTokens.ToString();
        _pendingAvatarPath = _vm.PersonaAvatar;
        UpdateAvatarPreview(_vm.PersonaAvatar);
        PopulateGroupCombo(persona.GroupId);
        PersonaLinkedInstructionsSection.Visibility = Visibility.Visible;
        PersonaDialogOverlay.Visibility = Visibility.Visible;
    }

    // === Persona-Instruction Link ===

    private async void OpenPersonaInstructionLink_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        await _vm.OpenPersonaInstructionLinkDialogCommand.ExecuteAsync(null);
        UpdateNoAvailableInstructionsText();
        PersonaInstructionLinkOverlay.Visibility = Visibility.Visible;
    }

    private void ClosePersonaInstructionLink_Click(object sender, RoutedEventArgs e)
    {
        PersonaInstructionLinkOverlay.Visibility = Visibility.Collapsed;
    }

    private void PersonaInstructionLinkOverlay_Click(object sender, MouseButtonEventArgs e)
    {
        PersonaInstructionLinkOverlay.Visibility = Visibility.Collapsed;
    }

    private async void LinkPersonaInstruction_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null || sender is not FrameworkElement { Tag: InstructionFileItem item }) return;
        await _vm.LinkInstructionToPersonaCommand.ExecuteAsync(item);
        UpdateNoAvailableInstructionsText();
    }

    private async void LinkPersonaInstructionGroup_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null || sender is not FrameworkElement { Tag: AvailableInstructionGroup group }) return;
        await _vm.LinkInstructionGroupToPersonaCommand.ExecuteAsync(group);
        UpdateNoAvailableInstructionsText();
    }

    private async void UnlinkPersonaInstruction_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null || sender is not FrameworkElement { Tag: InstructionFileItem item }) return;
        await _vm.UnlinkInstructionFromPersonaCommand.ExecuteAsync(item);
    }

    private void UpdateNoAvailableInstructionsText()
    {
        if (_vm == null) return;
        NoAvailableInstructionsText.Visibility =
            _vm.PersonaAvailableInstructionGroups.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PopulateGroupCombo(string? selectedGroupId)
    {
        if (_vm == null) return;

        var items = new List<GroupComboItem> { new() { GroupId = null, Name = "(미분류)" } };
        foreach (var g in _vm.Groups)
            items.Add(new GroupComboItem { GroupId = g.GroupId, Name = g.Name });

        PersonaGroupCombo.ItemsSource = items;
        PersonaGroupCombo.SelectedItem = items.FirstOrDefault(i => i.GroupId == selectedGroupId) ?? items[0];
    }

    private void ClearPersonaFields()
    {
        PersonaNameInput.Text = string.Empty;
        PersonaLabelInput.Text = string.Empty;
        PersonaDescInput.Text = string.Empty;
        PersonaPromptInput.Text = string.Empty;
        PersonaInstructionsInput.Text = string.Empty;
        PersonaKeywordsInput.Text = string.Empty;
        SelectModel(PersonaModelCombo, AllModels, "claude-sonnet");
        SelectModel(PersonaFallbackCombo, FallbackModels, null);
        PersonaTempInput.Text = "0.7";
        PersonaTokensInput.Text = "4096";
        _pendingAvatarPath = string.Empty;
        UpdateAvatarPreview(string.Empty);
    }

    private static void SelectModel(ComboBox combo, List<ModelOption> options, string? alias)
    {
        combo.SelectedItem = options.FirstOrDefault(m => m.Alias == alias) ?? options[0];
    }

    private void PersonaDialogCancel_Click(object sender, RoutedEventArgs e)
    {
        PersonaDialogOverlay.Visibility = Visibility.Collapsed;
    }

    private async void PersonaDialogSave_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;

        // 그룹 선택
        var selectedGroup = PersonaGroupCombo.SelectedItem as GroupComboItem;
        _vm.PersonaGroupId = selectedGroup?.GroupId;

        _vm.PersonaName = PersonaNameInput.Text;
        _vm.PersonaLabel = PersonaLabelInput.Text;
        _vm.PersonaDescription = PersonaDescInput.Text;
        _vm.PersonaSystemPrompt = PersonaPromptInput.Text;
        _vm.PersonaInstructions = PersonaInstructionsInput.Text;
        _vm.PersonaKeywords = PersonaKeywordsInput.Text;
        var primaryModel = PersonaModelCombo.SelectedItem as ModelOption;
        var fallbackModel = PersonaFallbackCombo.SelectedItem as ModelOption;
        _vm.PersonaPrimaryModel = primaryModel?.Alias ?? "claude-sonnet";
        _vm.PersonaFallbackModel = fallbackModel?.Alias;
        _vm.PersonaAvatar = _pendingAvatarPath;

        if (float.TryParse(PersonaTempInput.Text, out var temp))
            _vm.PersonaTemperature = temp;
        if (int.TryParse(PersonaTokensInput.Text, out var tokens))
            _vm.PersonaMaxTokens = tokens;

        await _vm.SavePersonaCommand.ExecuteAsync(null);
        PersonaDialogOverlay.Visibility = Visibility.Collapsed;
        UpdateUI();
    }

    private async void DeletePersona_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null || sender is not FrameworkElement { Tag: Persona persona }) return;
        var result = MessageBox.Show($"'{persona.Label}' 페르소나를 삭제하시겠습니까?",
            "삭제 확인", MessageBoxButton.YesNo);
        if (result != MessageBoxResult.Yes) return;
        await _vm.DeletePersonaCommand.ExecuteAsync(persona);
        UpdateUI();
    }

    private void PersonaDialogOverlay_Click(object sender, MouseButtonEventArgs e)
    {
        PersonaDialogOverlay.Visibility = Visibility.Collapsed;
    }

    private void DialogContent_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    // === Avatar ===

    private void AvatarTemplate_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: AvatarTemplate template }) return;
        _pendingAvatarPath = template.DataUri;
        UpdateAvatarPreview(template.DataUri);
        TemplateDialogOverlay.Visibility = Visibility.Collapsed;
    }

    private void OpenTemplateDialog_Click(object sender, RoutedEventArgs e)
    {
        TemplateDialogOverlay.Visibility = Visibility.Visible;
    }

    private void CloseTemplateDialog_Click(object sender, RoutedEventArgs e)
    {
        TemplateDialogOverlay.Visibility = Visibility.Collapsed;
    }

    private void TemplateDialogOverlay_Click(object sender, MouseButtonEventArgs e)
    {
        TemplateDialogOverlay.Visibility = Visibility.Collapsed;
    }

    private void Page_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;

        if (PersonaInstructionLinkOverlay.Visibility == Visibility.Visible)
        {
            PersonaInstructionLinkOverlay.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }
        else if (TemplateDialogOverlay.Visibility == Visibility.Visible)
        {
            TemplateDialogOverlay.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }
        else if (PersonaDialogOverlay.Visibility == Visibility.Visible)
        {
            PersonaDialogOverlay.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }
        else if (GroupDialogOverlay.Visibility == Visibility.Visible)
        {
            GroupDialogOverlay.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }
    }

    private void ChooseAvatar_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select Avatar Image",
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp",
            Multiselect = false
        };

        if (dlg.ShowDialog() != true) return;

        Directory.CreateDirectory(AvatarsDir);
        var ext = Path.GetExtension(dlg.FileName);
        var destName = $"{Guid.NewGuid():N}{ext}";
        var destPath = Path.Combine(AvatarsDir, destName);
        File.Copy(dlg.FileName, destPath, overwrite: true);

        _pendingAvatarPath = destPath;
        UpdateAvatarPreview(destPath);
    }

    private void RemoveAvatar_Click(object sender, RoutedEventArgs e)
    {
        _pendingAvatarPath = string.Empty;
        UpdateAvatarPreview(string.Empty);
    }

    private void UpdateAvatarPreview(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            AvatarPreviewBorder.Visibility = Visibility.Collapsed;
            AvatarFallback.Visibility = Visibility.Visible;
            RemoveAvatarBtn.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            BitmapImage bmp;

            if (path.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                var commaIdx = path.IndexOf(',');
                if (commaIdx < 0) throw new FormatException("Invalid data URI");
                var base64 = path[(commaIdx + 1)..];
                var bytes = Convert.FromBase64String(base64);

                bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = new MemoryStream(bytes);
                bmp.EndInit();
                bmp.Freeze();
            }
            else if (File.Exists(path))
            {
                bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
            }
            else
            {
                AvatarPreviewBorder.Visibility = Visibility.Collapsed;
                AvatarFallback.Visibility = Visibility.Visible;
                RemoveAvatarBtn.Visibility = Visibility.Collapsed;
                return;
            }

            AvatarPreviewBrush.ImageSource = bmp;
            AvatarPreviewBorder.Visibility = Visibility.Visible;
            AvatarFallback.Visibility = Visibility.Collapsed;
            RemoveAvatarBtn.Visibility = Visibility.Visible;
        }
        catch
        {
            AvatarPreviewBorder.Visibility = Visibility.Collapsed;
            AvatarFallback.Visibility = Visibility.Visible;
            RemoveAvatarBtn.Visibility = Visibility.Collapsed;
        }
    }
}

/// <summary>그룹 콤보박스 아이템</summary>
public class GroupComboItem
{
    public string? GroupId { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>스코프 선택 (글로벌 프리셋 / 프로젝트별)</summary>
public class ScopeItem
{
    public string? ProjectId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>모델 드롭다운 아이템</summary>
public class ModelOption(string? alias, string displayName)
{
    public string? Alias { get; } = alias;
    public string DisplayName { get; } = displayName;
}
