using System.Windows;

namespace MintADB.Wpf;

public partial class App : Application
{
    public static bool BootstrapOnly { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        BootstrapOnly = false;
        base.OnStartup(e);

        var main = new MainWindow();
        main.Show();
    }
}