using System.Text.RegularExpressions;
using MintADB.Wpf.Models;

namespace MintADB.Wpf.Services;

public sealed partial class AppScanService(AdbService adb)
{
    [GeneratedRegex(@"^package:(?<pkg>\S+)(?:\s+installer=(?<inst>\S+))?(?:\s+uid:(?<uid>\d+))?", RegexOptions.Compiled)]
    private static partial Regex PackageLineRegex();

    public async Task<IReadOnlyList<InstalledApp>> ScanAllAsync(string serial, CancellationToken ct = default)
    {
        var detailTask = adb.ShellAsync("pm list packages -i -U", serial, ct);
        var enabledTask = adb.ShellAsync("pm list packages -e", serial, ct);
        var systemTask = adb.ShellAsync("pm list packages -s", serial, ct);
        var disabledTask = adb.ShellAsync("pm list packages -d", serial, ct);
        var hiddenTask = adb.ShellAsync("pm list packages --hidden", serial, ct);
        await Task.WhenAll(detailTask, enabledTask, systemTask, disabledTask, hiddenTask);

        var systemPkgs = ParsePlainPackages(systemTask.Result.Combined);
        var enabledPkgs = ParsePlainPackages(enabledTask.Result.Combined);
        var disabledPkgs = ParsePlainPackages(disabledTask.Result.Combined);
        var hiddenPkgs = ParsePlainPackages(hiddenTask.Result.Combined);
        if (hiddenPkgs.Count == 0)
        {
            var hiddenFallback = await adb.ShellAsync("pm list packages -h", serial, ct);
            hiddenPkgs = ParsePlainPackages(hiddenFallback.Combined);
        }

        var activePkgs = ResolveActivePackages(
            ParsePlainPackages(detailTask.Result.Combined),
            enabledPkgs,
            disabledPkgs,
            hiddenPkgs);

        var apps = new List<InstalledApp>();

        foreach (var line in detailTask.Result.Combined.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (!TryParsePackageLine(trimmed, out var package, out var installer, out var uid))
                continue;

            if (!activePkgs.Contains(package))
                continue;

            var isSystem = systemPkgs.Contains(package);
            var category = AppClassifier.Classify(package, isSystem, installer);
            apps.Add(new InstalledApp
            {
                Package = package,
                Name = AppClassifier.DisplayName(package),
                Installer = installer,
                Uid = uid,
                IsSystem = isSystem,
                Category = category,
            });
        }

        return apps
            .OrderBy(a => a.CategorySortOrder)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<InactiveApp>> ScanInactiveAsync(string serial, CancellationToken ct = default)
    {
        var installedTask = adb.ShellAsync("pm list packages", serial, ct);
        var disabledTask = adb.ShellAsync("pm list packages -d", serial, ct);
        var uninstalledTask = adb.ShellAsync("pm list packages -u", serial, ct);
        var hiddenTask = adb.ShellAsync("pm list packages --hidden", serial, ct);
        await Task.WhenAll(installedTask, disabledTask, uninstalledTask, hiddenTask);

        var installed = ParsePlainPackages(installedTask.Result.Combined);
        var disabled = ParsePlainPackages(disabledTask.Result.Combined);
        var hidden = ParsePlainPackages(hiddenTask.Result.Combined);
        if (hidden.Count == 0)
        {
            var hiddenFallback = await adb.ShellAsync("pm list packages -h", serial, ct);
            hidden = ParsePlainPackages(hiddenFallback.Combined);
        }

        var uninstalled = ParsePlainPackages(uninstalledTask.Result.Combined)
            .Where(p => !installed.Contains(p))
            .ToHashSet(StringComparer.Ordinal);

        var states = new Dictionary<string, InactiveAppState>(StringComparer.Ordinal);

        foreach (var pkg in uninstalled)
            states[pkg] = InactiveAppState.Uninstalled;

        foreach (var pkg in disabled)
            states[pkg] = InactiveAppState.Disabled;

        foreach (var pkg in hidden)
        {
            if (!states.ContainsKey(pkg))
                states[pkg] = InactiveAppState.Hidden;
        }

        return states
            .Select(kv => new InactiveApp
            {
                Package = kv.Key,
                Name = AppClassifier.DisplayName(kv.Key),
                State = kv.Value,
            })
            .OrderBy(a => a.StateSortOrder)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static HashSet<string> ResolveActivePackages(
        HashSet<string> allPkgs,
        HashSet<string> enabledPkgs,
        HashSet<string> disabledPkgs,
        HashSet<string> hiddenPkgs)
    {
        if (enabledPkgs.Count > 0)
            return enabledPkgs.Where(p => !hiddenPkgs.Contains(p)).ToHashSet(StringComparer.Ordinal);

        var active = new HashSet<string>(allPkgs, StringComparer.Ordinal);
        active.ExceptWith(disabledPkgs);
        active.ExceptWith(hiddenPkgs);
        return active;
    }

    private static bool TryParsePackageLine(
        string line, out string package, out string installer, out int uid)
    {
        package = "";
        installer = "";
        uid = 0;

        if (!line.StartsWith("package:", StringComparison.Ordinal))
            return false;

        var match = PackageLineRegex().Match(line);
        if (match.Success)
        {
            package = match.Groups["pkg"].Value;
            installer = match.Groups["inst"].Success ? match.Groups["inst"].Value : "";
            if (match.Groups["uid"].Success)
                int.TryParse(match.Groups["uid"].Value, out uid);
            return true;
        }

        var rest = line["package:".Length..].Trim();
        var space = rest.IndexOf(' ');
        package = space > 0 ? rest[..space] : rest;
        return package.Length > 0;
    }

    private static HashSet<string> ParsePlainPackages(string output)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var pkg = ExtractPackageName(line);
            if (pkg.Length > 0)
                set.Add(pkg);
        }
        return set;
    }

    private static string ExtractPackageName(string line)
    {
        var trimmed = line.Trim();
        if (!trimmed.StartsWith("package:", StringComparison.Ordinal))
            return "";

        var rest = trimmed["package:".Length..].Trim();
        var space = rest.IndexOf(' ');
        return space > 0 ? rest[..space] : rest;
    }
}