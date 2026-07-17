using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PromptManager;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        LoadLogo();
    }

    private void LoadLogo()
    {
        var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "ProtoCall_logo.png");
        if (!File.Exists(logoPath))
        {
            return;
        }

        LogoImage.Source = new BitmapImage(new Uri(logoPath, UriKind.Absolute));
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
