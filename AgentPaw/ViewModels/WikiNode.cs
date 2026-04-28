using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using AgentPaw.Models;

namespace AgentPaw.ViewModels;

public partial class WikiNode : ObservableObject
{
    public WikiDocument Document { get; }
    public List<WikiNode> Children { get; } = [];

    [ObservableProperty] private int _depth;
    [ObservableProperty] private bool _isExpanded = true;
    [ObservableProperty] private bool _isSelected;

    public WikiNode(WikiDocument document)
    {
        Document = document;
    }

    public string WikiId => Document.WikiId;
    public string Title => Document.Title;
    public bool HasChildren => Children.Count > 0;
    public Thickness Indent => new(8 + Depth * 16, 0, 4, 0);

    partial void OnDepthChanged(int value) => OnPropertyChanged(nameof(Indent));

    public void NotifyTitleChanged() => OnPropertyChanged(nameof(Title));
}
