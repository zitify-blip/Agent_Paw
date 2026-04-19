using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using AgentPaw.ViewModels;

namespace AgentPaw.Views.Pages;

public partial class InstructionsPage : UserControl
{
    private InstructionsViewModel? _vm;
    private string? _pendingGroupId;

    public InstructionsPage()
    {
        InitializeComponent();
    }

    public void Initialize(InstructionsViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
    }

    private async void InstructionsPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        await _vm.LoadCommand.ExecuteAsync(null);
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (_vm == null) return;
        GroupsList.ItemsSource = _vm.Groups;
        UngroupedList.ItemsSource = _vm.UngroupedFiles;
        UngroupedSection.Visibility = _vm.UngroupedFiles.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        if (!string.IsNullOrEmpty(_vm.ErrorMessage))
        {
            ErrorText.Text = _vm.ErrorMessage;
            ErrorText.Visibility = Visibility.Visible;
        }
        else ErrorText.Visibility = Visibility.Collapsed;
    }

    // === Group ===

    private void CreateGroup_Click(object sender, RoutedEventArgs e)
    {
        GroupDialogTitle.Text = "새 그룹";
        GroupNameInput.Text = string.Empty;
        _vm?.OpenCreateGroupCommand.Execute(null);
        GroupDialogOverlay.Visibility = Visibility.Visible;
    }

    private void EditGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: InstructionGroupItem group }) return;
        GroupDialogTitle.Text = "그룹 이름 변경";
        GroupNameInput.Text = group.Name;
        _vm?.OpenEditGroupCommand.Execute(group);
        GroupDialogOverlay.Visibility = Visibility.Visible;
    }

    private async void DeleteGroup_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null || sender is not FrameworkElement { Tag: InstructionGroupItem group }) return;
        await _vm.DeleteGroupCommand.ExecuteAsync(group);
        UpdateUI();
    }

    private void GroupDialogCancel_Click(object sender, RoutedEventArgs e)
    {
        GroupDialogOverlay.Visibility = Visibility.Collapsed;
        _vm?.CloseGroupDialogCommand.Execute(null);
    }

    private async void GroupDialogSave_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        _vm.GroupDialogName = GroupNameInput.Text;
        await _vm.SaveGroupCommand.ExecuteAsync(null);
        GroupDialogOverlay.Visibility = Visibility.Collapsed;
        UpdateUI();
    }

    private void GroupDialogOverlay_Click(object sender, MouseButtonEventArgs e)
    {
        GroupDialogOverlay.Visibility = Visibility.Collapsed;
        _vm?.CloseGroupDialogCommand.Execute(null);
    }

    // === File ===

    private async void UploadFiles_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;

        var dialog = new OpenFileDialog
        {
            Filter = "Markdown files (*.md)|*.md|Text files (*.txt)|*.txt|All files (*.*)|*.*",
            Title = "인스트럭션 파일 선택",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true && dialog.FileNames.Length > 0)
        {
            await _vm.UploadFilesAsync(dialog.FileNames);
            UpdateUI();
        }
    }

    private void CreateFile_Click(object sender, RoutedEventArgs e)
    {
        _pendingGroupId = null;
        FileNameInput.Text = string.Empty;
        FileContentInput.Text = string.Empty;
        FileDialogOverlay.Visibility = Visibility.Visible;
    }

    private void CreateFileInGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: InstructionGroupItem group }) return;
        _pendingGroupId = group.GroupId;
        FileNameInput.Text = string.Empty;
        FileContentInput.Text = string.Empty;
        FileDialogOverlay.Visibility = Visibility.Visible;
    }

    private async void UploadFilesToGroup_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null || sender is not FrameworkElement { Tag: InstructionGroupItem group }) return;

        var dialog = new OpenFileDialog
        {
            Filter = "Markdown files (*.md)|*.md|Text files (*.txt)|*.txt|All files (*.*)|*.*",
            Title = $"'{group.Name}' 그룹에 파일 업로드",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true && dialog.FileNames.Length > 0)
        {
            await _vm.UploadFilesAsync(dialog.FileNames, group.GroupId);
            UpdateUI();
        }
    }

    private void FileDialogCancel_Click(object sender, RoutedEventArgs e)
    {
        FileDialogOverlay.Visibility = Visibility.Collapsed;
    }

    private async void FileDialogSave_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        _vm.FileDialogName = FileNameInput.Text;
        _vm.FileDialogContent = FileContentInput.Text;
        _vm.FileDialogGroupId = _pendingGroupId;
        await _vm.SaveFileCommand.ExecuteAsync(null);
        FileDialogOverlay.Visibility = Visibility.Collapsed;
        UpdateUI();
    }

    private void FileDialogOverlay_Click(object sender, MouseButtonEventArgs e)
    {
        FileDialogOverlay.Visibility = Visibility.Collapsed;
    }

    private async void DeleteFile_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null || sender is not FrameworkElement { Tag: InstructionFileItem file }) return;
        await _vm.DeleteFileCommand.ExecuteAsync(file);
        UpdateUI();
    }

    // === Preview ===

    private async void PreviewFile_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null || sender is not FrameworkElement { Tag: InstructionFileItem file }) return;
        await _vm.PreviewFileCommand.ExecuteAsync(file);
        PreviewTitle.Text = file.Name;
        PreviewContentText.Text = _vm.PreviewContent;
        PreviewDialogOverlay.Visibility = Visibility.Visible;
    }

    private void PreviewClose_Click(object sender, RoutedEventArgs e)
    {
        PreviewDialogOverlay.Visibility = Visibility.Collapsed;
    }

    private void PreviewDialogOverlay_Click(object sender, MouseButtonEventArgs e)
    {
        PreviewDialogOverlay.Visibility = Visibility.Collapsed;
    }

    private void Page_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;

        if (PreviewDialogOverlay.Visibility == Visibility.Visible)
        {
            PreviewDialogOverlay.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }
        else if (FileDialogOverlay.Visibility == Visibility.Visible)
        {
            FileDialogOverlay.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }
        else if (GroupDialogOverlay.Visibility == Visibility.Visible)
        {
            GroupDialogOverlay.Visibility = Visibility.Collapsed;
            _vm?.CloseGroupDialogCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void ToggleGroup_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: InstructionGroupItem group }) return;
        group.IsExpanded = !group.IsExpanded;
        e.Handled = true;
    }

    private void DialogContent_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }
}
