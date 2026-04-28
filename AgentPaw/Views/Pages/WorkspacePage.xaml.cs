using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AgentPaw.Models;
using AgentPaw.ViewModels;
using Microsoft.Win32;

namespace AgentPaw.Views.Pages;

public partial class WorkspacePage : UserControl
{
    public event Action? BackRequested;
    public event Action? DevAgentRequested;

    private TimelineViewModel? _timelineVm;
    private WikiViewModel? _wikiVm;
    private ProjectSettingsViewModel? _projectSettingsVm;
    private int _mentionAnchor = -1;

    public WorkspacePage()
    {
        InitializeComponent();
    }

    public void Initialize(WorkspaceViewModel vm, bool resetTab = true)
    {
        // 이전 VM의 이벤트 핸들러 해제 (중복 등록 방지)
        if (DataContext is WorkspaceViewModel oldVm && !ReferenceEquals(oldVm, vm))
            oldVm.Messages.CollectionChanged -= OnMessagesChanged;

        DataContext = vm;
        _timelineVm = null;
        _wikiVm = null;

        if (resetTab)
            SetTabVisibility("chat");

        // 동일 VM 재사용 시 중복 등록 방지
        vm.Messages.CollectionChanged -= OnMessagesChanged;
        vm.Messages.CollectionChanged += OnMessagesChanged;
    }

    public void SetTimelineViewModel(TimelineViewModel vm)
    {
        _timelineVm = vm;
        var page = new TimelinePage();
        page.Initialize(vm);
        TimelineHost.Content = page;
    }

    public void SetWikiViewModel(WikiViewModel vm)
    {
        _wikiVm = vm;
        var page = new WikiPage();
        page.Initialize(vm);
        WikiHost.Content = page;
    }

    public void SetProjectSettingsViewModel(ProjectSettingsViewModel vm)
    {
        _projectSettingsVm = vm;
        var page = new ProjectSettingsPage();
        page.Initialize(vm);
        ProjectSettingsHost.Content = page;
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add) return;
        // 가상화 ListBox는 ScrollToEnd 대신 마지막 아이템을 뷰포트로 가져오는 방식으로 스크롤한다
        Dispatcher.InvokeAsync(() =>
        {
            if (ChatList.Items.Count == 0) return;
            var last = ChatList.Items[ChatList.Items.Count - 1];
            if (last != null) ChatList.ScrollIntoView(last);
        });
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is WorkspaceViewModel vm)
        {
            await vm.SendMessageCommand.ExecuteAsync(null);
            MessageInput.Focus();
        }
    }

    private async void MessageInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
        {
            e.Handled = true;
            if (DataContext is WorkspaceViewModel vm && !vm.IsLoading)
            {
                await vm.SendMessageCommand.ExecuteAsync(null);
                MessageInput.Focus();
            }
        }
    }

    private void MessageInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is not WorkspaceViewModel vm) return;
        var text = MessageInput.Text ?? string.Empty;
        var caret = MessageInput.CaretIndex;

        if (vm.IsMentionPopupOpen)
        {
            if (_mentionAnchor < 0 || _mentionAnchor >= text.Length || text[_mentionAnchor] != '@' || caret <= _mentionAnchor)
            {
                _mentionAnchor = -1;
                vm.CloseMentionPopup();
                return;
            }

            var filter = text.Substring(_mentionAnchor + 1, caret - _mentionAnchor - 1);
            if (filter.Contains(' ') || filter.Contains('\n') || filter.Contains('\t'))
            {
                _mentionAnchor = -1;
                vm.CloseMentionPopup();
                return;
            }

            vm.UpdateMentionFilter(filter);
            return;
        }

        if (caret > 0 && caret <= text.Length && text[caret - 1] == '@')
        {
            var prev = caret - 2;
            if (prev < 0 || char.IsWhiteSpace(text[prev]))
            {
                _mentionAnchor = caret - 1;
                vm.OpenMentionPopup(string.Empty);
            }
        }
    }

    private void MessageInput_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not WorkspaceViewModel vm || !vm.IsMentionPopupOpen) return;

        switch (e.Key)
        {
            case Key.Down:
                vm.MoveMentionSelection(1);
                e.Handled = true;
                break;
            case Key.Up:
                vm.MoveMentionSelection(-1);
                e.Handled = true;
                break;
            case Key.Enter:
            case Key.Tab:
                if (AcceptMention(vm)) e.Handled = true;
                break;
            case Key.Escape:
                _mentionAnchor = -1;
                vm.CloseMentionPopup();
                e.Handled = true;
                break;
        }
    }

    private void MentionList_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not WorkspaceViewModel vm) return;
        if (AcceptMention(vm))
        {
            e.Handled = true;
            MessageInput.Focus();
        }
    }

    private bool AcceptMention(WorkspaceViewModel vm)
    {
        var persona = vm.GetSelectedMentionPersona();
        if (persona == null || _mentionAnchor < 0) return false;

        var text = MessageInput.Text ?? string.Empty;
        var caret = MessageInput.CaretIndex;
        if (_mentionAnchor >= text.Length || text[_mentionAnchor] != '@')
        {
            _mentionAnchor = -1;
            vm.CloseMentionPopup();
            return false;
        }

        var endOfFilter = Math.Min(caret, text.Length);
        var replacement = $"@{persona.Label} ";
        var newText = text.Substring(0, _mentionAnchor) + replacement + text.Substring(endOfFilter);
        var newCaret = _mentionAnchor + replacement.Length;

        vm.InputMessage = newText;
        MessageInput.CaretIndex = Math.Min(newCaret, newText.Length);

        _mentionAnchor = -1;
        vm.CloseMentionPopup();
        return true;
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        BackRequested?.Invoke();
    }

    private void OpenDevAgent_Click(object sender, RoutedEventArgs e)
    {
        DevAgentRequested?.Invoke();
    }

    private async void AttachFile_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not WorkspaceViewModel vm) return;

        var dlg = new OpenFileDialog
        {
            Title = "첨부할 마크다운 파일 선택",
            Filter = "Markdown files (*.md)|*.md",
            Multiselect = true
        };
        if (dlg.ShowDialog() != true) return;

        await vm.AddAttachmentsAsync(dlg.FileNames);
    }

    private void RemoveAttachment_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not WorkspaceViewModel vm) return;
        if (sender is not FrameworkElement { Tag: ChatAttachment attachment }) return;
        vm.RemoveAttachmentCommand.Execute(attachment);
    }

    private void TeamPickerButton_Click(object sender, RoutedEventArgs e)
    {
        TeamPickerPopup.IsOpen = !TeamPickerPopup.IsOpen;
    }

    private void TeamMode_Panel_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is WorkspaceViewModel vm) vm.TeamMode = "panel";
    }

    private void TeamMode_Debate_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is WorkspaceViewModel vm) vm.TeamMode = "debate";
    }

    private void TeamMode_Chain_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is WorkspaceViewModel vm) vm.TeamMode = "chain";
    }

    private void ChatTab_Click(object sender, RoutedEventArgs e)
    {
        SetTabVisibility("chat");
    }

    private async void TimelineTab_Click(object sender, RoutedEventArgs e)
    {
        SetTabVisibility("timeline");
        if (_timelineVm != null && DataContext is WorkspaceViewModel vm)
            await _timelineVm.LoadAsync(vm.ProjectId);
    }

    private async void WikiTab_Click(object sender, RoutedEventArgs e)
    {
        SetTabVisibility("wiki");
        if (_wikiVm != null && DataContext is WorkspaceViewModel vm)
            await _wikiVm.LoadAsync(vm.ProjectId);
    }

    private void SettingsTab_Click(object sender, RoutedEventArgs e)
    {
        SetTabVisibility("settings");
    }

    private void SetTabVisibility(string active)
    {
        ChatPanel.Visibility = active == "chat" ? Visibility.Visible : Visibility.Collapsed;
        TimelineHost.Visibility = active == "timeline" ? Visibility.Visible : Visibility.Collapsed;
        WikiHost.Visibility = active == "wiki" ? Visibility.Visible : Visibility.Collapsed;
        ProjectSettingsHost.Visibility = active == "settings" ? Visibility.Visible : Visibility.Collapsed;

        ChatTabBtn.Appearance = active == "chat"
            ? Wpf.Ui.Controls.ControlAppearance.Primary
            : Wpf.Ui.Controls.ControlAppearance.Secondary;
        TimelineTabBtn.Appearance = active == "timeline"
            ? Wpf.Ui.Controls.ControlAppearance.Primary
            : Wpf.Ui.Controls.ControlAppearance.Secondary;
        WikiTabBtn.Appearance = active == "wiki"
            ? Wpf.Ui.Controls.ControlAppearance.Primary
            : Wpf.Ui.Controls.ControlAppearance.Secondary;
        SettingsTabBtn.Appearance = active == "settings"
            ? Wpf.Ui.Controls.ControlAppearance.Primary
            : Wpf.Ui.Controls.ControlAppearance.Secondary;
    }

    private void ShowDetail_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is ChatMessage msg && DataContext is WorkspaceViewModel vm)
            vm.ShowMessageDetailCommand.Execute(msg);
    }

    private void CloseDetail_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is WorkspaceViewModel vm)
            vm.CloseMessageDetailCommand.Execute(null);
    }
}
