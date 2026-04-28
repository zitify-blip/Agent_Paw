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

    public DevAgentPage()
    {
        InitializeComponent();
    }

    public void Initialize(DevAgentViewModel vm)
    {
        _vm = vm;
        DataContext = vm;

        vm.Messages.CollectionChanged += OnMessagesChanged;
        Unloaded += (_, _) => vm.Messages.CollectionChanged -= OnMessagesChanged;
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
        {
            foreach (var item in e.NewItems)
            {
                if (item is DevAgentMessage msg)
                    msg.PropertyChanged += OnMessagePropertyChanged;
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
