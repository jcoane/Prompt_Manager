using PromptManager.Models;

namespace PromptManager.ViewModels;

public sealed class FilterItem : ObservableObject
{
    private int _count;

    public FilterItem(PromptFilterKind kind, string name)
    {
        Kind = kind;
        Name = name;
    }

    public PromptFilterKind Kind { get; }

    public string Name { get; }

    public int Count
    {
        get => _count;
        set => SetProperty(ref _count, value);
    }
}

