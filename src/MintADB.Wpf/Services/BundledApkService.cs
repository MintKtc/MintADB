using System.IO;
using MintADB.Wpf.Models;

namespace MintADB.Wpf.Services;

public static class BundledApkService
{
    public const string DeviceFolder = "/sdcard/Download/MintADB";

    public static string RemotePath(string fileName) => $"{DeviceFolder}/{fileName}";

    public static string ResolveMiuiDir()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        foreach (var dir in new[]
                 {
                     Path.Combine(AppContext.BaseDirectory, "Miui"),
                     Path.Combine(desktop, "Miui"),
                     Path.Combine(AdbToolsService.MintAdbDir, "Miui"),
                     @"C:\Users\Mint\Desktop\Miui",
                 })
        {
            if (Directory.Exists(dir))
                return dir;
        }

        return Path.Combine(AppContext.BaseDirectory, "Miui");
    }

    public static IReadOnlyList<BundledApk> Scan(string? dir = null)
    {
        dir ??= ResolveMiuiDir();
        if (!Directory.Exists(dir))
            return [];

        return Directory.EnumerateFiles(dir, "*.apk", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Select(f =>
            {
                var (displayName, kind) = BundledApkDisplay.Resolve(f.Name);
                return new BundledApk
                {
                    FileName = f.Name,
                    FullPath = f.FullName,
                    DisplayName = displayName,
                    Kind = kind,
                    RemotePath = RemotePath(f.Name),
                    SizeBytes = f.Length,
                };
            })
            .ToList();
    }
}