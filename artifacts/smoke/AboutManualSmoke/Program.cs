using System.IO;
using System.Windows;
using System.Windows.Controls;
using PromptManager;

var thread = new Thread(() =>
{
    var app = new Application();
    AddResources(app);

    var about = new AboutWindow();
    about.ApplyTemplate();

    Assert(about.Title == "About Prompt Manager", "About window title was incorrect.");
    var authorTextBlock = (TextBlock?)about.FindName("AuthorTextBlock");
    var titleTextBlock = (TextBlock?)about.FindName("TitleTextBlock");
    var logoImage = (Image?)about.FindName("LogoImage");
    Assert(authorTextBlock?.Text == "Autored By John-Patrick Coane", "Author text was not found.");
    Assert(titleTextBlock?.Text == "Prompt Manager", "Prompt Manager text was not found.");
    Assert(logoImage?.Source is not null, "Logo image source was not loaded.");
    Assert(File.Exists(Path.Combine(AppContext.BaseDirectory, "USER_MANUAL.md")), "USER_MANUAL.md was not copied to output.");
    Assert(File.Exists(Path.Combine(AppContext.BaseDirectory, "Assets", "ProtoCall_logo.png")), "ProtoCall logo was not copied to output.");

    Console.WriteLine("About/manual smoke test passed.");
    Console.WriteLine("About dialog title, author text, software text: yes");
    Console.WriteLine("User manual copied to output: yes");
    Console.WriteLine("ProtoCall logo copied to output: yes");

    about.Close();
    app.Shutdown();
});
thread.SetApartmentState(ApartmentState.STA);
thread.Start();
thread.Join();


static void AddResources(Application app)
{
    app.Resources["PanelBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xF6, 0xDC));
    app.Resources["PanelInsetBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xFB, 0xEF));
    app.Resources["LineBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xC9, 0xD5, 0xD8));
    app.Resources["MutedInkBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x5D, 0x6E, 0x78));
}
static bool FindText(DependencyObject root, string expected)
{
    if (root is TextBlock textBlock && textBlock.Text == expected)
    {
        return true;
    }

    var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
    for (var i = 0; i < count; i++)
    {
        if (FindText(System.Windows.Media.VisualTreeHelper.GetChild(root, i), expected))
        {
            return true;
        }
    }

    if (root is ContentControl contentControl && contentControl.Content?.ToString() == expected)
    {
        return true;
    }

    return false;
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}







