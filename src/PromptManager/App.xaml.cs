using System.Windows;
using PromptManager.Data;
using PromptManager.Services;
using PromptManager.ViewModels;

namespace PromptManager;

public partial class App : Application
{
    private System.Threading.Mutex? _instanceMutex;
    private readonly bool _openLibraryOnStartup;

    public App() : this(true) { }
    public App(bool openLibraryOnStartup) { _openLibraryOnStartup = openLibraryOnStartup; }

    protected override void OnStartup(StartupEventArgs e)
    {
        if (!_openLibraryOnStartup) return;
        base.OnStartup(e);

        _instanceMutex = new System.Threading.Mutex(true, "Local\\PromptManager-" + System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value, out var firstInstance);
        if (!firstInstance)
        {
            MessageBox.Show("Prompt Manager is already running. Open its existing window from the taskbar.", "Prompt Manager", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(); return;
        }

        try
        {
            var repository = new PromptRepository();
            if (repository.DetachedHistoryCount() > 0)
            {
                var answer = MessageBox.Show("The library contains detached version history. Preserve those entries in a recovery table and create a safety backup before opening? Current prompts will not be changed.", "Library Recovery", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (answer != MessageBoxResult.Yes) { Shutdown(); return; }
                repository.RecoverDetachedHistory();
            }
            repository.Initialize();
            var exportService = new PromptFileService(repository);
            var viewModel = new MainViewModel(repository, exportService);

            MainWindow = new MainWindow { DataContext = viewModel };
            MainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Prompt Manager could not open the library. No library was deleted.\n\n" + ex.Message, "Prompt Manager", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e) { _instanceMutex?.Dispose(); base.OnExit(e); }
}

