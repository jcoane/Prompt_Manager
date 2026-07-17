using System.Collections.ObjectModel;
using PromptManager.ViewModels;

namespace PromptManager.Models;

public sealed class FolderNode : ObservableObject
{
    private string _name;
    private bool _isExpanded = true;
    private bool _isSelected;

    public FolderNode(int id, int? parentId, string name)
    {
        Id = id;
        ParentId = parentId;
        _name = name;
    }

    public int Id { get; }

    public int? ParentId { get; }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public ObservableCollection<FolderNode> Children { get; } = new();
}



