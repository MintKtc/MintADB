using System.IO;

namespace MintADB.Wpf.Services;

public static class ScrcpyLocator
{
    private static string? _cached;

    public static string? Find(string? adbPath)
    {
        if (_cached is not null && File.Exists(_cached))
            return _cached;

        foreach (var candidate in EnumerateCandidates(adbPath))
        {
            if (File.Exists(candidate))
                return _cached = candidate;
        }

        return null;
    }

    public static string SearchSummary(string? adbPath)
    {
        var lines = EnumerateCandidates(adbPath).Take(8).Select(p => $"  · {p}");
        return string.Join(Environment.NewLine, lines);
    }

    private static IEnumerable<string> EnumerateCandidates(string? adbPath)
    {
        var env = Environment.GetEnvironmentVariable("SCRCPY_PATH");
        if (!string.IsNullOrWhiteSpace(env))
            yield return env.Trim().Trim('"');

        if (adbPath is not null)
        {
            var adbDir = Path.GetDirectoryName(adbPath);
            if (!string.IsNullOrEmpty(adbDir))
            {
                yield return Path.Combine(adbDir, "scrcpy.exe");
                yield return Path.Combine(adbDir, "scrcpy", "scrcpy.exe");

                var parent = Directory.GetParent(adbDir)?.FullName;
                if (parent is not null)
                {
                    yield return Path.Combine(parent, "scrcpy.exe");
                    yield return Path.Combine(parent, "scrcpy", "scrcpy.exe");
                    yield return Path.Combine(parent, "scrcpy-win64", "scrcpy.exe");
                }
            }
        }

        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        yield return Path.Combine(desktop, "MintADB", "scrcpy", "scrcpy.exe");
        yield return Path.Combine(desktop, "scrcpy", "scrcpy.exe");

        foreach (var hit in FindUnderRoots(@"D:\Miui", @"C:\Miui", @"D:\tools", @"C:\tools"))
            yield return hit;

        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "")
                     .Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = dir.Trim().Trim('"');
            if (trimmed.Length > 0)
                yield return Path.Combine(trimmed, "scrcpy.exe");
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