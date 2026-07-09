using System.IO;

namespace MintADB.Wpf.Services;

public sealed record PlatformToolsStatus(
    bool BundledToolsPresent,
    bool BundledDriverPresent,
    bool AdbFound,
    bool FastbootFound,
    bool UsingBundled,
    string AdbPath,
    string FastbootPath,
    string? AdbVersion);

public static class PlatformToolsLocator
{
    public static string BundledToolsDir => Path.Combine(AppContext.BaseDirectory, "PlatformTools");

    public static string InstalledToolsDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MintADB", "platform-tools");

    public static string BundledDriverDir => Path.Combine(AppContext.BaseDirectory, "Drivers", "usb_driver");

    public static string BundledDriverInf => Path.Combine(BundledDriverDir, "android_winusb.inf");

    public static string BundledQualcommDriverDir => Path.Combine(AppContext.BaseDirectory, "Drivers", "qualcomm");

    public static string BundledQualcommDriverInf => Path.Combine(BundledQualcommDriverDir, "qdloadUSB.inf");

    public static string BundledAllInOneDir => Path.Combine(AppContext.BaseDirectory, "Drivers", "all_in_one");

    public static string[] AllDriverInfs =>
    [
        BundledDriverInf,
        BundledQualcommDriverInf,
        Path.Combine(BundledAllInOneDir, "qcser.inf"),
    ];

    public static string ResolveAdbPath()
    {
        foreach (var dir in EnumerateToolDirs())
        {
            var adb = Path.Combine(dir, "adb.exe");
            if (File.Exists(adb))
                return adb;
        }

        return FindOnPath("adb.exe") ?? "adb";
    }

    public static string ResolveFastbootPath(string? adbPath = null)
    {
        if (!string.IsNullOrWhiteSpace(adbPath) && adbPath != "adb")
        {
            var sibling = Path.Combine(Path.GetDirectoryName(adbPath) ?? "", "fastboot.exe");
            if (File.Exists(sibling))
                return sibling;
        }

        foreach (var dir in EnumerateToolDirs())
        {
            var fb = Path.Combine(dir, "fastboot.exe");
            if (File.Exists(fb))
                return fb;
        }

        return FindOnPath("fastboot.exe") ?? "fastboot";
    }

    public static PlatformToolsStatus GetStatus(string adbPath)
    {
        var bundledAdb = Path.Combine(BundledToolsDir, "adb.exe");
        var bundledFb = Path.Combine(BundledToolsDir, "fastboot.exe");
        var fastbootPath = ResolveFastbootPath(adbPath);
        var adbFound = adbPath != "adb" && File.Exists(adbPath);
        var fbFound = fastbootPath != "fastboot" && File.Exists(fastbootPath);
        var usingBundled = adbFound && Path.GetFullPath(adbPath)
            .StartsWith(Path.GetFullPath(BundledToolsDir), StringComparison.OrdinalIgnoreCase);

        return new PlatformToolsStatus(
            File.Exists(bundledAdb) && File.Exists(bundledFb),
            File.Exists(BundledDriverInf),
            adbFound,
            fbFound,
            usingBundled,
            adbPath,
            fastbootPath,
            null);
    }

    public static async Task<(bool Ok, string Message)> DeployToolsToLocalAsync(CancellationToken ct = default)
    {
        var bundledAdb = Path.Combine(BundledToolsDir, "adb.exe");
        if (!File.Exists(bundledAdb))
            return (false, "Không có bộ platform-tools đi kèm app (thư mục PlatformTools).");

        try
        {
            Directory.CreateDirectory(InstalledToolsDir);
            CopyDirectory(BundledToolsDir, InstalledToolsDir);

            // Ensure scrcpy package is present after deploy (mirror needs it next to tools).
            EnsureScrcpyDeployed();

            var marker = Path.Combine(InstalledToolsDir, ".mintadb-installed.txt");
            await File.WriteAllTextAsync(marker,
                $"MintADB platform-tools\nInstalled: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n", ct);

            ScrcpyLocator.ClearCache();
            var scrcpyOk = ScrcpyLocator.Find(Path.Combine(InstalledToolsDir, "adb.exe")) is not null;
            var msg = $"Đã triển khai ADB/Fastboot vào:\n{InstalledToolsDir}";
            if (!scrcpyOk)
                msg += "\n[WARN] scrcpy chưa có — mirror màn hình sẽ lỗi cho đến khi cài lại bản có scrcpy.";

            return (true, msg);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Copy PlatformTools\scrcpy (or flat scrcpy.exe package) into the local deploy dir if missing.
    /// </summary>
    public static void EnsureScrcpyDeployed()
    {
        var destSub = Path.Combine(InstalledToolsDir, "scrcpy");
        var destFlat = Path.Combine(InstalledToolsDir, "scrcpy.exe");
        if (ScrcpyLocator.IsUsable(Path.Combine(destSub, "scrcpy.exe"))
            || ScrcpyLocator.IsUsable(destFlat))
            return;

        var sources = new[]
        {
            Path.Combine(BundledToolsDir, "scrcpy"),
            Path.Combine(AppContext.BaseDirectory, "PlatformTools", "scrcpy"),
            Path.Combine(AppContext.BaseDirectory, "scrcpy"),
        };

        foreach (var src in sources)
        {
            var srcExe = Path.Combine(src, "scrcpy.exe");
            if (!File.Exists(srcExe)) continue;
            Directory.CreateDirectory(destSub);
            CopyDirectory(src, destSub);
            // Also place a usable flat copy next to adb for older locators.
            if (ScrcpyLocator.IsUsable(Path.Combine(destSub, "scrcpy.exe")))
            {
                try
                {
                    foreach (var file in Directory.EnumerateFiles(destSub))
                    {
                        var name = Path.GetFileName(file);
                        if (name.Equals("adb.exe", StringComparison.OrdinalIgnoreCase)
                            || name.StartsWith("AdbWin", StringComparison.OrdinalIgnoreCase))
                            continue; // do not overwrite platform-tools adb with scrcpy's bundled adb
                        File.Copy(file, Path.Combine(InstalledToolsDir, name), overwrite: true);
                    }
                }
                catch
                {
                    // Subfolder copy is enough.
                }
            }

            ScrcpyLocator.ClearCache();
            return;
        }

        // Flat layout: PlatformTools\scrcpy.exe + scrcpy-server
        var flatBundled = Path.Combine(BundledToolsDir, "scrcpy.exe");
        if (ScrcpyLocator.IsUsable(flatBundled))
        {
            foreach (var name in new[] { "scrcpy.exe", "scrcpy-server", "SDL3.dll",
                         "avcodec-62.dll", "avformat-62.dll", "avutil-60.dll",
                         "swresample-6.dll", "libusb-1.0.dll" })
            {
                var f = Path.Combine(BundledToolsDir, name);
                if (File.Exists(f))
                    File.Copy(f, Path.Combine(InstalledToolsDir, name), overwrite: true);
            }
            ScrcpyLocator.ClearCache();
        }
    }

    private static IEnumerable<string> EnumerateToolDirs()
    {
        yield return BundledToolsDir;
        yield return InstalledToolsDir;
        yield return Path.Combine(AdbToolsService.MintAdbDir, "platform-tools");

        var env = Environment.GetEnvironmentVariable("ADB_PATH")
                  ?? Environment.GetEnvironmentVariable("ANDROID_ADB");
        if (!string.IsNullOrWhiteSpace(env))
        {
            var dir = File.Exists(env) ? Path.GetDirectoryName(env) : env;
            if (!string.IsNullOrEmpty(dir))
                yield return dir!;
        }

        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "")
                     .Split(';', StringSplitOptions.RemoveEmptyEntries))
            yield return dir.Trim().Trim('"');

        yield return @"D:\Miui\platform-tools";
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Android\Sdk\platform-tools");
    }

    private static string? FindOnPath(string fileName)
    {
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "")
                     .Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir.Trim().Trim('"'), fileName);
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    private static void CopyDirectory(string source, string dest)
    {
        foreach (var dir in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(source, dest, StringComparison.OrdinalIgnoreCase));

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = file.Replace(source, dest, StringComparison.OrdinalIgnoreCase);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }
}