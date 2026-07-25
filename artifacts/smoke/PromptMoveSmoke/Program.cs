using System.Reflection;
using PromptManager.Data;
using PromptManager.Models;
using PromptManager.Services;
using PromptManager.ViewModels;

var repository = new PromptRepository();
repository.Initialize();
var viewModel = new MainViewModel(repository, new PromptFileService(repository));
var suffix = DateTime.Now.ToString("yyyyMMddHHmmss");
var parentName = "ZZZ Move Parent " + suffix;
var childName = "ZZZ Move Child " + suffix;
var promptTitle = "ZZZ Move Prompt " + suffix;
var parentId = 0;
var childId = 0;
var promptId = 0;

try
{
    parentId = repository.CreateFolder(null, parentName);
    childId = repository.CreateFolder(parentId, childName);
    RefreshFolders(viewModel, parentId, true);

    var childPath = parentName + " / " + childName;
    Assert(viewModel.FolderOptions.Any(option => option.Id == FolderOption.UnfiledId && option.Path == "Unfiled"), "Folder options did not include Unfiled.");
    Assert(viewModel.FolderOptions.Any(option => option.Id == parentId && option.Path == parentName), "Folder options did not include the parent path.");
    Assert(viewModel.FolderOptions.Any(option => option.Id == childId && option.Path == childPath), "Folder options did not include the nested child path.");

    promptId = repository.CreatePrompt(parentId);
    var prompt = repository.GetPromptById(promptId) ?? throw new InvalidOperationException("Created prompt could not be loaded.");
    prompt.Title = promptTitle;
    repository.SavePrompt(prompt);

    RefreshPrompts(viewModel, promptId);
    Assert(viewModel.SelectedPrompt?.Id == promptId, "Created prompt was not selected for move testing.");
    Assert(viewModel.SelectedPrompt!.FolderId == parentId, "Created prompt did not start in the parent folder.");

    viewModel.SelectedPrompt.FolderId = childId;
    viewModel.SaveCommand.Execute(null);

    var movedPrompt = repository.GetPromptById(promptId) ?? throw new InvalidOperationException("Moved prompt could not be loaded.");
    Assert(movedPrompt.FolderId == childId, "Saved prompt FolderId did not update to the child folder.");
    Assert(movedPrompt.FolderPath == childPath, "Saved prompt FolderPath did not resolve to the child path.");
    Assert(viewModel.SelectedFolder?.Id == childId, "View model did not select the destination folder after move.");
    Assert(viewModel.SelectedPrompt?.Id == promptId, "Moved prompt was not selected after refresh.");
    Assert(viewModel.SelectedPrompt!.FolderPath == childPath, "Selected prompt did not show the destination path after refresh.");

    viewModel.SelectedPrompt.FolderId = null;
    viewModel.SaveCommand.Execute(null);

    var unfiledPrompt = repository.GetPromptById(promptId) ?? throw new InvalidOperationException("Unfiled prompt could not be loaded.");
    Assert(unfiledPrompt.FolderId is null, "Saved prompt FolderId did not clear when moved to Unfiled.");
    Assert(unfiledPrompt.FolderPath == "Unfiled", "Saved prompt FolderPath did not resolve to Unfiled.");
    Assert(viewModel.SelectedFolder is null, "View model did not return to All Folders after moving prompt to Unfiled.");

    Console.WriteLine("Prompt move smoke test passed.");
    Console.WriteLine("Folder dropdown included Unfiled, parent, and child paths: yes");
    Console.WriteLine("Prompt moved to child folder and saved: yes");
    Console.WriteLine("Prompt moved to Unfiled and saved: yes");
    Console.WriteLine("Register/tree refreshed to the destination after moves: yes");
}
finally
{
    if (promptId != 0)
    {
        repository.DeletePrompt(promptId);
    }

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

static void RefreshPrompts(MainViewModel viewModel, int? preferredPromptId)
{
    var method = typeof(MainViewModel).GetMethod("RefreshPrompts", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("RefreshPrompts method was not found.");
    method.Invoke(viewModel, new object?[] { preferredPromptId });
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

