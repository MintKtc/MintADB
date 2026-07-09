using System.Windows;
using MintADB.Wpf.Resources;

namespace MintADB.Wpf;

public partial class App : Application
{
    public static bool BootstrapOnly { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        BootstrapOnly = false;
        base.OnStartup(e);

        // Áp dụng VI/EN đã lưu trước khi tạo cửa sổ (dictionary + culture + resx)
        LanguageManager.ApplySavedLanguage();

        var main = new MainWindow();
        main.Show();
    }
}