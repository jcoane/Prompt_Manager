using System.Windows;
using PromptManager.Data;
using PromptManager.Services;
using PromptManager.ViewModels;

namespace PromptManager;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var repository = new PromptRepository();
        repository.Initialize();

        var exportService = new PromptFileService(repository);
        var viewModel = new MainViewModel(repository, exportService);

        MainWindow = new MainWindow
        {
            DataContext = viewModel
        };
        MainWindow.Show();
    }
}

