using System.Reflection;
using PromptManager.Data;
using PromptManager.Models;
using PromptManager.Services;
using PromptManager.ViewModels;

var repository = new PromptRepository();
repository.Initialize();
var fileService = new PromptFileService(repository);
var viewModel = new MainViewModel(repository, fileService);
var suffix = DateTime.Now.ToString("yyyyMMddHHmmss");
var parentName = "ZZZ Folder Refresh Parent " + suffix;
var childName = "ZZZ Folder Refresh Child " + suffix;
var parentId = 0;
var childId = 0;

try
{
    parentId = repository.CreateFolder(null, parentName);
    RefreshFolders(viewModel, parentId, true);

    var parentNode = FindFolder(viewModel.FolderTree, parentName);
    Assert(parentNode is not null, "Created parent folder was not present in FolderTree after refresh.");
    Assert(viewModel.SelectedFolder?.Id == parentId, "Created parent folder was not selected after refresh.");
    Assert(parentNode!.IsSelected, "Created parent node IsSelected was false after refresh.");

    childId = repository.CreateFolder(parentId, childName);
    RefreshFolders(viewModel, childId, true);

    parentNode = FindFolder(viewModel.FolderTree, parentName);
    var childNode = FindFolder(viewModel.FolderTree, childName);
    Assert(parentNode is not null, "Parent folder disappeared after child folder refresh.");
    Assert(childNode is not null, "Created child folder was not present in FolderTree after refresh.");
    Assert(parentNode!.IsExpanded, "Parent folder was not expanded after selecting child folder.");
    Assert(viewModel.SelectedFolder?.Id == childId, "Created child folder was not selected after refresh.");
    Assert(childNode!.IsSelected, "Created child node IsSelected was false after refresh.");

    repository.DeleteFolder(childId);
    childId = 0;
    RefreshFolders(viewModel, parentId, false);

    Assert(FindFolder(viewModel.FolderTree, childName) is null, "Deleted child folder was still present in FolderTree after refresh.");
    Assert(viewModel.SelectedFolder?.Id == parentId, "Parent folder was not selected after deleting child folder.");

    repository.DeleteFolder(parentId);
    parentId = 0;
    RefreshFolders(viewModel, null, false);

    Assert(FindFolder(viewModel.FolderTree, parentName) is null, "Deleted parent folder was still present in FolderTree after refresh.");
    Assert(viewModel.SelectedFolder is null, "SelectedFolder was not cleared after deleting the top-level parent folder.");

    Console.WriteLine("Folder create and refresh smoke test passed.");
    Console.WriteLine("Created parent folder appeared and was selected: yes");
    Console.WriteLine("Created child folder appeared and was selected: yes");
    Console.WriteLine("Parent remained expanded for child display: yes");
    Console.WriteLine("Deleted child folder disappeared and parent was selected: yes");
    Console.WriteLine("Deleted parent folder disappeared and All Folders was selected: yes");
}
finally
{
    if (childId != 0)
    {
        repository.DeleteFolder(childId);
    }

    if (parentId != 0)
    {
        repository.DeleteFolder(parentId);
    }
}

static void RefreshFolders(MainViewModel viewModel, int? selectedFolderId, bool preserveCurrentSelection)
{
    var method = typeof(MainViewModel).GetMethod("RefreshFolders", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("RefreshFolders method was not found.");
    method.Invoke(viewModel, new object?[] { selectedFolderId, preserveCurrentSelection });
}

static FolderNode? FindFolder(IEnumerable<FolderNode> folders, string name)
{
    foreach (var folder in folders)
    {
        if (folder.Name == name)
        {
            return folder;
        }

        var child = FindFolder(folder.Children, name);
        if (child is not null)
        {
            return child;
        }
    }

    return null;
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
