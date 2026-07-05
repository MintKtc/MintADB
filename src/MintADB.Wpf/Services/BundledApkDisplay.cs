using System.IO;
using System.Text.RegularExpressions;
using MintADB.Wpf.Models;

namespace MintADB.Wpf.Services;

internal static partial class BundledApkDisplay
{
    [GeneratedRegex(@"\s*\(\d+\)$", RegexOptions.Compiled)]
    private static partial Regex TrailingCopyRegex();

    [GeneratedRegex(@"_apkmirror.*$|_apkpure.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex MirrorSuffixRegex();

    [GeneratedRegex(@"v([\d.]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex VersionRegex();

    public static (string DisplayName, BundledApkKind Kind) Resolve(string fileName)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var lower = baseName.ToLowerInvariant();

        if (lower.Contains("inputmethod.latin") || lower.Contains("gboard"))
            return ("Gboard", BundledApkKind.Keyboard);

        if (lower.Contains("play store"))
        {
            if (baseName.Contains("(1)", StringComparison.Ordinal))
                return ("Play Store #2", BundledApkKind.Store);
            return ("Play Store", BundledApkKind.Store);
        }

        if (lower is "settings miui 13" or "settings miui13" or "settings miui 13 ")
            return ("Settings MIUI 13", BundledApkKind.Settings);

        if (lower is "settings miui 14" or "settings miui14" or "settings miui 14 ")
            return ("Settings MIUI 14", BundledApkKind.Settings);

        if (lower.Contains("settings_hyper") || lower.Contains("settings hyper"))
            return ("Settings Hyper", BundledApkKind.Settings);

        if (lower.StartsWith("shizuku", StringComparison.Ordinal))
        {
            var ver = VersionRegex().Match(baseName);
            return ver.Success
                ? ($"Shizuku {ver.Groups[1].Value}", BundledApkKind.Tool)
                : ("Shizuku", BundledApkKind.Tool);
        }

        var cleaned = MirrorSuffixRegex().Replace(baseName, "");
        cleaned = TrailingCopyRegex().Replace(cleaned, "").Trim();
        cleaned = Regex.Replace(cleaned, @"[_\[\]]+", " ").Trim();

        if (cleaned.Length > 32)
            cleaned = cleaned[..29] + "…";

        return (string.IsNullOrWhiteSpace(cleaned) ? baseName : cleaned, BundledApkKind.Other);
    }
}