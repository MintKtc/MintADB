using System.IO;
using System.Windows;
using MintADB.Wpf.Services;

namespace MintADB.Wpf;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var cli = Environment.GetCommandLineArgs();
        if (args.Contains(InstallBootstrapService.BootstrapOnlyArg, StringComparer.OrdinalIgnoreCase)
            || cli.Contains(InstallBootstrapService.BootstrapOnlyArg, StringComparer.OrdinalIgnoreCase))
        {
            Environment.Exit(RunBootstrapOnly());
        }

        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }

    private static int RunBootstrapOnly()
    {
        try
        {
            var adb = new AdbService();
            adb.Shizuku = new ShizukuService(adb);
            var bootstrap = new InstallBootstrapService(adb);
            Task.Run(() => bootstrap.RunAsync(offerDriverInstall: false))
                .GetAwaiter()
                .GetResult();
            return 0;
        }
        catch (Exception ex)
        {
            WriteBootstrapError(ex);
            return 1;
        }
    }

    private static void WriteBootstrapError(Exception ex)
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MintADB");
            Directory.CreateDirectory(logDir);
            File.WriteAllText(
                Path.Combine(logDir, "bootstrap-error.log"),
                $"{DateTime.Now:u}{Environment.NewLine}{ex}");
        }
        catch
        {
            // Best-effort logging during headless bootstrap.
        }
    }
}