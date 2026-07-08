using System.IO;
using System.Windows;

namespace MintADB.Wpf.Windows;

public partial class WelcomeWindow : Window
{
    private static string StateDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MintADB");

    private static string WelcomeShownPath => Path.Combine(StateDir, "welcome-shown.flag");

    public bool DontShowAgainChecked => DontShowAgain.IsChecked == true;

    public WelcomeWindow()
    {
        InitializeComponent();
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        if (DontShowAgain.IsChecked == true)
        {
            Directory.CreateDirectory(StateDir);
            File.WriteAllText(WelcomeShownPath, DateTime.Now.ToString("o"));
        }
        DialogResult = true;
        Close();
    }

    public static bool ShouldShow()
    {
        if (File.Exists(WelcomeShownPath))
        {
            try
            {
                var content = File.ReadAllText(WelcomeShownPath);
                return string.IsNullOrEmpty(content);
            }
            catch
            {
                return true;
            }
        }
        return true;
    }
}
