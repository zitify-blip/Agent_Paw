using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AgentPaw.Services;
using AgentPaw.ViewModels;

namespace AgentPaw.Views.Pages;

public partial class DevAgentPage : UserControl
{
    private DevAgentViewModel? _vm;
    private readonly List<DevAgentMessage> _subscribedMessages = [];

    public DevAgentPage()
    {
        InitializeComponent();
    }

    public void Initialize(DevAgentViewModel vm)
    {
        _vm = vm;
        DataContext = vm;

        vm.Messages.CollectionChanged += OnMessagesChanged;
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        _vm.Messages.CollectionChanged -= OnMessagesChanged;
        foreach (var msg in _subscribedMessages)
            msg.PropertyChanged -= OnMessagePropertyChanged;
        _subscribedMessages.Clear();
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
        {
            foreach (var item in e.NewItems)
            {
                if (item is DevAgentMessage msg)
                {
                    msg.PropertyChanged += OnMessagePropertyChanged;
                    _subscribedMessages.Add(msg);
                }
            }
            ScrollToBottom();
        }
    }

    private void OnMessagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DevAgentMessage.Content))
            Dispatcher.InvokeAsync(ScrollToBottom);
    }

    private void ScrollToBottom() => MessagesScroll.ScrollToEnd();

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter
            && !e.KeyboardDevice.IsKeyDown(Key.LeftShift)
            && !e.KeyboardDevice.IsKeyDown(Key.RightShift))
        {
            e.Handled = true;
            _vm?.SendCommand.Execute(null);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
        => _vm?.CancelCommand.Execute(null);

    private void NewProjectItem_Click(object sender, MouseButtonEventArgs e)
        => _vm?.NewProjectCommand.Execute(null);

    private void ProjectItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is DevProjectRecord record)
            _vm?.SelectProjectCommand.Execute(record);
    }

    private void RootPath_Click(object sender, MouseButtonEventArgs e)
        => _vm?.ChangeDevRootCommand.Execute(null);
}
