using System.IO;

namespace MintADB.Wpf.Services;

public static class ScrcpyLocator
{
    private static string? _cached;

    /// <summary>Forget cached path (after reinstall / redeploy).</summary>
    public static void ClearCache() => _cached = null;

    public static string? Find(string? adbPath)
    {
        if (_cached is not null && IsUsable(_cached))
            return _cached;

        foreach (var candidate in EnumerateCandidates(adbPath))
        {
            if (IsUsable(candidate))
                return _cached = candidate;
        }

        return null;
    }

    public static string SearchSummary(string? adbPath)
    {
        var lines = EnumerateCandidates(adbPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .Select(p => $"  · {p}");
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// scrcpy needs its DLLs + scrcpy-server in the same folder as the exe.
    /// </summary>
    public static bool IsUsable(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        var dir = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(dir))
            return false;

        // Official win64 package always ships scrcpy-server next to the exe.
        var server = Path.Combine(dir, "scrcpy-server");
        return File.Exists(server);
    }

    private static IEnumerable<string> EnumerateCandidates(string? adbPath)
    {
        // 1) Always prefer tools shipped with the running app (survives reinstall).
        var baseDir = AppContext.BaseDirectory;
        if (!string.IsNullOrEmpty(baseDir))
        {
            yield return Path.Combine(baseDir, "PlatformTools", "scrcpy", "scrcpy.exe");
            yield return Path.Combine(baseDir, "PlatformTools", "scrcpy.exe");
            yield return Path.Combine(baseDir, "scrcpy", "scrcpy.exe");
            yield return Path.Combine(baseDir, "scrcpy.exe");
        }

        // 2) Local deploy copy (bootstrap → %LocalAppData%\MintADB\platform-tools)
        var installed = PlatformToolsLocator.InstalledToolsDir;
        yield return Path.Combine(installed, "scrcpy", "scrcpy.exe");
        yield return Path.Combine(installed, "scrcpy.exe");

        // 3) Explicit env (only if set)
        var env = Environment.GetEnvironmentVariable("SCRCPY_PATH");
        if (!string.IsNullOrWhiteSpace(env))
            yield return env.Trim().Trim('"');

        // 4) Next to the resolved adb.exe
        if (!string.IsNullOrWhiteSpace(adbPath)
            && !string.Equals(adbPath, "adb", StringComparison.OrdinalIgnoreCase))
        {
            var adbDir = Path.GetDirectoryName(adbPath);
            if (!string.IsNullOrEmpty(adbDir))
            {
                yield return Path.Combine(adbDir, "scrcpy", "scrcpy.exe");
                yield return Path.Combine(adbDir, "scrcpy.exe");

                var parent = Directory.GetParent(adbDir)?.FullName;
                if (parent is not null)
                {
                    yield return Path.Combine(parent, "scrcpy", "scrcpy.exe");
                    yield return Path.Combine(parent, "scrcpy.exe");
                    yield return Path.Combine(parent, "scrcpy-win64", "scrcpy.exe");
                }
            }
        }

        // 5) Desktop fallbacks (dev machines)
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        yield return Path.Combine(desktop, "MintADB", "scrcpy", "scrcpy.exe");
        yield return Path.Combine(desktop, "scrcpy", "scrcpy.exe");

        // 6) LocalAppData\MintADB\scrcpy
        var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(localApp, "MintADB", "scrcpy", "scrcpy.exe");

        foreach (var hit in FindUnderRoots(@"D:\Miui", @"C:\Miui", @"D:\tools", @"C:\tools"))
            yield return hit;

        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "")
                     .Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = dir.Trim().Trim('"');
            if (trimmed.Length > 0)
            {
                yield return Path.Combine(trimmed, "scrcpy", "scrcpy.exe");
                yield return Path.Combine(trimmed, "scrcpy.exe");
            }
        }
    }

    private static IEnumerable<string> FindUnderRoots(params string[] roots)
    {
        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) continue;
            IEnumerable<string> hits;
            try
            {
                hits = Directory.EnumerateFiles(root, "scrcpy.exe", SearchOption.AllDirectories);
            }
            catch
            {
                continue;
            }

            foreach (var hit in hits)
                yield return hit;
        }
    }
}
