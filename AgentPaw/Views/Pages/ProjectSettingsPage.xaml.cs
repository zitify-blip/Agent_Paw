using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AgentPaw.Models;
using AgentPaw.ViewModels;
using Microsoft.Win32;

namespace AgentPaw.Views.Pages;

public partial class ProjectSettingsPage : UserControl
{
    private ProjectSettingsViewModel? _vm;

    public ProjectSettingsPage()
    {
        InitializeComponent();
    }

    public void Initialize(ProjectSettingsViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
    }

    private void ProjectSettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (_vm == null) return;
        ProjectNameText.Text = $" — {_vm.ProjectName}";
        GroupsList.ItemsSource = _vm.Groups;
        UngroupedList.ItemsSource = _vm.UngroupedPersonas;
        UngroupedSection.Visibility = _vm.UngroupedPersonas.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        NoPersonasText.Visibility = (_vm.Groups.Count == 0 && _vm.UngroupedPersonas.Count == 0)
            ? Visibility.Visible : Visibility.Collapsed;
        LinkedGroupsList.ItemsSource = _vm.LinkedInstructionGroups;
        LinkedUngroupedList.ItemsSource = _vm.LinkedUngroupedFiles;
        LinkedUngroupedSection.Visibility = _vm.LinkedUngroupedFiles.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        NoLinkedText.Visibility = (_vm.LinkedInstructionGroups.Count == 0 && _vm.LinkedUngroupedFiles.Count == 0)
            ? Visibility.Visible : Visibility.Collapsed;
        var showProjectCards = string.IsNullOrEmpty(_vm.ProjectId) ? Visibility.Collapsed : Visibility.Visible;
        WorkspaceCard.Visibility = showProjectCards;
        CloneCard.Visibility = showProjectCards;

        if (!string.IsNullOrEmpty(_vm.ErrorMessage))
        {
            ErrorText.Text = _vm.ErrorMessage;
            ErrorText.Visibility = Visibility.Visible;
        }
        else ErrorText.Visibility = Visibility.Collapsed;
    }

    private void BrowseWorkspace_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        var dlg = new OpenFolderDialog
        {
            Title = "작업 폴더 선택",
            InitialDirectory = !string.IsNullOrWhiteSpace(_vm.WorkspacePath)
                ? _vm.WorkspacePath
                : (Directory.Exists(_vm.EffectiveWorkspacePath) ? _vm.EffectiveWorkspacePath : "")
        };
        if (dlg.ShowDialog() == true)
            _vm.WorkspacePath = dlg.FolderName;
    }

    // === Persona Linking (전역 페르소나 → 프로젝트 연결) ===

    private async void OpenPersonaLinkDialog_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        await _vm.OpenPersonaLinkDialogCommand.ExecuteAsync(null);
        AvailablePersonaGroupsList.ItemsSource = _vm.AvailablePersonaGroups;
        PersonaLinkDialogOverlay.Visibility = Visibility.Visible;
    }

    private void PersonaLinkDialogClose_Click(object sender, RoutedEventArgs e)
    {
        PersonaLinkDialogOverlay.Visibility = Visibility.Collapsed;
    }

    private void PersonaLinkDialogOverlay_Click(object sender, MouseButtonEventArgs e)
    {
        PersonaLinkDialogOverlay.Visibility = Visibility.Collapsed;
    }

    private async void LinkPersona_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null || sender is not FrameworkElement { Tag: Persona persona }) return;
        await _vm.LinkPersonaCommand.ExecuteAsync(persona);
        UpdateUI();
    }

    private async void LinkPersonaGroup_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null || sender is not FrameworkElement { Tag: AvailablePersonaGroup group }) return;
        await _vm.LinkPersonaGroupCommand.ExecuteAsync(group);
        UpdateUI();
    }

    private async void UnlinkPersona_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null || sender is not FrameworkElement { Tag: Persona persona }) return;
        await _vm.UnlinkPersonaCommand.ExecuteAsync(persona);
        UpdateUI();
    }

    private async void UnlinkPersonaGroup_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null || sender is not FrameworkElement { Tag: PersonaGroupItem group }) return;
        var result = MessageBox.Show(
            $"'{group.Name}' 그룹의 페르소나 {group.Personas.Count}개를 이 프로젝트에서 모두 연결 해제하겠는가?",
            "그룹 연결 해제",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.OK) return;
        await _vm.UnlinkPersonaGroupCommand.ExecuteAsync(group);
        UpdateUI();
    }

    // === Instruction Linking ===

    private async void OpenLinkDialog_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        await _vm.OpenLinkDialogCommand.ExecuteAsync(null);
        AvailableInstructionGroupsList.ItemsSource = _vm.AvailableInstructionGroups;
        LinkDialogOverlay.Visibility = Visibility.Visible;
    }

    private async void LinkInstructionGroup_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null || sender is not FrameworkElement { Tag: AvailableInstructionGroup group }) return;
        await _vm.LinkInstructionGroupCommand.ExecuteAsync(group);
        UpdateUI();
    }

    private void LinkDialogClose_Click(object sender, RoutedEventArgs e)
    {
        LinkDialogOverlay.Visibility = Visibility.Collapsed;
    }

    private void LinkDialogOverlay_Click(object sender, MouseButtonEventArgs e)
    {
        LinkDialogOverlay.Visibility = Visibility.Collapsed;
    }

    private async void LinkInstruction_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null || sender is not FrameworkElement { Tag: InstructionFileItem file }) return;
        await _vm.LinkInstructionCommand.ExecuteAsync(file);
        UpdateUI();
    }

    private async void UnlinkInstruction_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null || sender is not FrameworkElement { Tag: InstructionFileItem file }) return;
        await _vm.UnlinkInstructionCommand.ExecuteAsync(file);
        UpdateUI();
    }

    private async void UnlinkInstructionGroup_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null || sender is not FrameworkElement { Tag: AvailableInstructionGroup group }) return;
        await _vm.UnlinkInstructionGroupCommand.ExecuteAsync(group);
        UpdateUI();
    }

    private void CloneTokenBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_vm != null && sender is PasswordBox pb)
            _vm.CloneToken = pb.Password;
    }

    private void Page_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;

        if (LinkDialogOverlay.Visibility == Visibility.Visible)
        {
            LinkDialogOverlay.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }
        else if (PersonaLinkDialogOverlay.Visibility == Visibility.Visible)
        {
            PersonaLinkDialogOverlay.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }
    }

    private void DialogContent_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }
}
