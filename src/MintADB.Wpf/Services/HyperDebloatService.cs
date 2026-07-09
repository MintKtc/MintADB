using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using MintADB.Wpf.Resources;

namespace MintADB.Wpf.Services;

/// <summary>
/// Script Hyper debloat (embedded). Log gọn: 1 dòng / package, bỏ qua im lặng app đã gỡ / bị chặn soft.
/// </summary>
public sealed partial class HyperDebloatService(AdbService adb, AdbToolsService tools)
{
    private const string EmbeddedName = "MintADB.HyperDebloat.txt";

    public static string BundledScriptPath =>
        Path.Combine(AppContext.BaseDirectory, "Assets", "HyperDebloat.txt");

    public static string BackupDir =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "MintADB",
            "DebloatBackup");

    public static IReadOnlyList<string> LoadDefaultScriptLines()
    {
        var text = ReadEmbeddedScript();
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException(
                "Script Hyper debloat không có trong app (EmbeddedResource thiếu).");

        try
        {
            var dir = Path.GetDirectoryName(BundledScriptPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            if (!File.Exists(BundledScriptPath)
                || File.ReadAllText(BundledScriptPath, Encoding.UTF8) != text)
            {
                File.WriteAllText(BundledScriptPath, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
        }
        catch { /* ignore */ }

        return ParseLines(text);
    }

    public static int CountRemoveCommands()
    {
        try { return LoadDefaultScriptLines().Count(IsRemoveCommand); }
        catch { return 0; }
    }

    public static int CountUniquePackages()
    {
        try
        {
            return LoadDefaultScriptLines()
                .Where(IsRemoveCommand)
                .Select(l => ExtractPackage(l["shell ".Length..].Trim()))
                .Where(p => p.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Count();
        }
        catch { return 0; }
    }

    public static string ScriptSourceLabel => $"built-in ({EmbeddedName})";

    /// <param name="progress">current, total, statusText — cập nhật thanh tiến trình UI.</param>
    public async Task<(int Ok, int Fail, int Skip)> RunAsync(
        string serial,
        Action<string>? log = null,
        bool backupApk = true,
        IProgress<(int Current, int Total, string Status)>? progress = null,
        CancellationToken ct = default)
    {
        var lines = LoadDefaultScriptLines();
        if (lines.Count == 0)
            throw new InvalidOperationException("Script Hyper debloat trống.");

        // Các bước thực thi (để progress mượt)
        var steps = lines.Where(IsProgressStep).ToList();
        var total = Math.Max(steps.Count, 1);
        var step = 0;

        var pkgCount = CountUniquePackages();
        log?.Invoke($"[Debloat Hyper] {pkgCount} · {Loc.Get("DebloatHyperRunning")}");
        progress?.Report((0, total, Loc.Get("DebloatHyperRunning")));

        if (backupApk)
        {
            try { Directory.CreateDirectory(BackupDir); }
            catch { /* ignore */ }
        }

        var outcomes = new Dictionary<string, PkgOutcome>(StringComparer.Ordinal);

        foreach (var line in lines)
        {
            ct.ThrowIfCancellationRequested();

            if (!IsProgressStep(line))
                continue;

            step++;
            var status = ProgressStatus(line);
            progress?.Report((step, total, status));

            if (line.StartsWith("pull ", StringComparison.OrdinalIgnoreCase))
            {
                if (!backupApk) continue;
                if (!TryParsePull(line, out var remote, out _)) continue;
                var fileName = Path.GetFileName(remote.Trim('"').Replace('/', Path.DirectorySeparatorChar));
                if (string.IsNullOrWhiteSpace(fileName)) fileName = "backup.apk";
                var local = Path.Combine(BackupDir, fileName);
                await tools.PullAsync(serial, remote.Trim('"'), local, ct);
                continue;
            }

            if (!line.StartsWith("shell ", StringComparison.OrdinalIgnoreCase))
                continue;

            var shellCmd = line["shell ".Length..].Trim();
            if (shellCmd.Length == 0 || !IsShellDebloatCommand(shellCmd))
                continue;

            if (IsBackupShellCommand(shellCmd))
            {
                if (backupApk)
                    await adb.ShellAsync(shellCmd, serial, ct);
                continue;
            }

            var pkg = ExtractPackage(shellCmd);
            if (pkg.Length == 0) continue;

            if (!outcomes.TryGetValue(pkg, out var state))
            {
                state = new PkgOutcome(pkg);
                outcomes[pkg] = state;
            }

            if (state.Uninstalled) continue;

            var r = await adb.ShellAsync(shellCmd, serial, ct);
            ApplyResult(state, shellCmd, r);
        }

        progress?.Report((total, total, Loc.Get("DebloatHyperDoneStatus")));

        var ok = 0;
        var fail = 0;
        var skip = 0;

        foreach (var state in outcomes.Values.OrderBy(s => s.Package, StringComparer.Ordinal))
        {
            if (state.Uninstalled)
            {
                ok++;
                log?.Invoke($"[OK] Đã gỡ {state.Package}");
            }
            else if (state.Disabled)
            {
                ok++;
                log?.Invoke($"[OK] Đã tắt {state.Package}");
            }
            else if (state.Cleared && !state.HadHardFail)
            {
                ok++;
                log?.Invoke($"[OK] Đã xóa data {state.Package}");
            }
            else if (state.AlreadyGone || state.OnlySoftFail)
            {
                skip++;
            }
            else if (state.HadHardFail)
            {
                fail++;
                log?.Invoke($"[FAIL] {state.Package}: {Short(state.LastError)}");
            }
            else
            {
                skip++;
            }
        }

        progress?.Report((total, total, Loc.Get("DebloatHyperDoneStatus")));
        log?.Invoke(Loc.Format("DebloatHyperDone", ok, skip, fail).Replace('\n', ' '));
        return (ok, fail, skip);
    }

    private static bool IsProgressStep(string line)
    {
        if (line.StartsWith("pull ", StringComparison.OrdinalIgnoreCase))
            return true;
        if (!line.StartsWith("shell ", StringComparison.OrdinalIgnoreCase))
            return false;
        var cmd = line["shell ".Length..].Trim();
        return IsShellDebloatCommand(cmd);
    }

    private static string ProgressStatus(string line)
    {
        if (line.StartsWith("pull ", StringComparison.OrdinalIgnoreCase))
            return Loc.Get("DebloatProgressBackup", "Backup…");
        if (!line.StartsWith("shell ", StringComparison.OrdinalIgnoreCase))
            return Loc.Get("DebloatHyperRunning");

        var cmd = line["shell ".Length..].Trim();
        var pkg = ExtractPackage(cmd);
        if (cmd.StartsWith("pm uninstall", StringComparison.OrdinalIgnoreCase))
            return pkg.Length > 0
                ? Loc.Format("DebloatProgressUninstall", pkg)
                : Loc.Get("DebloatHyperRunning");
        if (cmd.StartsWith("pm disable", StringComparison.OrdinalIgnoreCase))
            return pkg.Length > 0
                ? Loc.Format("DebloatProgressDisable", pkg)
                : Loc.Get("DebloatHyperRunning");
        if (cmd.StartsWith("pm clear", StringComparison.OrdinalIgnoreCase))
            return pkg.Length > 0
                ? Loc.Format("DebloatProgressClear", pkg)
                : Loc.Get("DebloatHyperRunning");
        if (IsBackupShellCommand(cmd))
            return Loc.Get("DebloatProgressBackup", "Backup…");
        return pkg.Length > 0 ? pkg : Loc.Get("DebloatHyperRunning");
    }

    private static void ApplyResult(PkgOutcome state, string shellCmd, ProcessResult r)
    {
        if (IsAlreadyGone(r, shellCmd))
        {
            state.AlreadyGone = true;
            return;
        }

        if (IsSoftBlocked(r, shellCmd))
        {
            state.SoftFail = true;
            state.LastError = r.Combined;
            return;
        }

        if (IsPmSuccess(r, shellCmd))
        {
            if (shellCmd.StartsWith("pm uninstall", StringComparison.OrdinalIgnoreCase))
                state.Uninstalled = true;
            else if (shellCmd.StartsWith("pm disable", StringComparison.OrdinalIgnoreCase))
                state.Disabled = true;
            else if (shellCmd.StartsWith("pm clear", StringComparison.OrdinalIgnoreCase))
                state.Cleared = true;
            return;
        }

        // Lỗi thật
        state.HadHardFail = true;
        state.LastError = r.Combined;
    }

    private sealed class PkgOutcome(string package)
    {
        public string Package { get; } = package;
        public bool Uninstalled { get; set; }
        public bool Disabled { get; set; }
        public bool Cleared { get; set; }
        public bool AlreadyGone { get; set; }
        public bool SoftFail { get; set; }
        public bool HadHardFail { get; set; }
        public string LastError { get; set; } = "";

        /// <summary>Chỉ soft-fail / already-gone, không hard fail còn lại.</summary>
        public bool OnlySoftFail =>
            SoftFail && !Uninstalled && !Disabled && !Cleared && !HadHardFail;
    }

    private static string ReadEmbeddedScript()
    {
        var asm = Assembly.GetExecutingAssembly();

        using (var s = asm.GetManifestResourceStream(EmbeddedName))
        {
            if (s is not null)
            {
                using var r = new StreamReader(s, Encoding.UTF8);
                return r.ReadToEnd();
            }
        }

        foreach (var name in asm.GetManifestResourceNames())
        {
            if (name.EndsWith("HyperDebloat.txt", StringComparison.OrdinalIgnoreCase)
                || name.Contains("HyperDebloat", StringComparison.OrdinalIgnoreCase))
            {
                using var s = asm.GetManifestResourceStream(name);
                if (s is null) continue;
                using var r = new StreamReader(s, Encoding.UTF8);
                return r.ReadToEnd();
            }
        }

        if (File.Exists(BundledScriptPath))
            return File.ReadAllText(BundledScriptPath, Encoding.UTF8);

        return "";
    }

    private static List<string> ParseLines(string text) =>
        text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#') && !l.StartsWith("//"))
            .ToList();

    private static bool IsRemoveCommand(string line)
    {
        if (!line.StartsWith("shell ", StringComparison.OrdinalIgnoreCase))
            return false;
        var cmd = line["shell ".Length..].Trim();
        return cmd.StartsWith("pm uninstall", StringComparison.OrdinalIgnoreCase)
               || cmd.StartsWith("pm disable", StringComparison.OrdinalIgnoreCase)
               || cmd.StartsWith("pm clear", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsShellDebloatCommand(string shellCmd) =>
        shellCmd.StartsWith("pm uninstall", StringComparison.OrdinalIgnoreCase)
        || shellCmd.StartsWith("pm disable", StringComparison.OrdinalIgnoreCase)
        || shellCmd.StartsWith("pm clear", StringComparison.OrdinalIgnoreCase)
        || shellCmd.StartsWith("pm path", StringComparison.OrdinalIgnoreCase)
        || shellCmd.StartsWith("cp ", StringComparison.OrdinalIgnoreCase)
        || shellCmd.StartsWith("rm ", StringComparison.OrdinalIgnoreCase);

    private static bool IsBackupShellCommand(string shellCmd) =>
        shellCmd.StartsWith("pm path", StringComparison.OrdinalIgnoreCase)
        || shellCmd.StartsWith("cp ", StringComparison.OrdinalIgnoreCase)
        || shellCmd.StartsWith("rm ", StringComparison.OrdinalIgnoreCase);

    private static string ExtractPackage(string shellCmd)
    {
        var parts = shellCmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = parts.Length - 1; i >= 0; i--)
        {
            var p = parts[i];
            if (p.StartsWith('-')) continue;
            if (p is "pm" or "uninstall" or "disable" or "disable-user" or "clear" or "--user")
                continue;
            if (int.TryParse(p, out _)) continue;
            if (p.Contains('.', StringComparison.Ordinal) || p.Contains('_', StringComparison.Ordinal))
                return p;
        }
        return "";
    }

    private static bool TryParsePull(string line, out string remote, out string local)
    {
        remote = "";
        local = "";
        var m = PullLineRegex().Match(line);
        if (!m.Success) return false;
        remote = m.Groups[1].Value;
        local = m.Groups[2].Value;
        return remote.Length > 0;
    }

    private static bool IsPmSuccess(ProcessResult r, string shellCmd)
    {
        if (r.Combined.Contains("Success", StringComparison.OrdinalIgnoreCase))
            return true;
        if (shellCmd.StartsWith("pm disable", StringComparison.OrdinalIgnoreCase)
            && (r.Ok || r.Combined.Contains("new state", StringComparison.OrdinalIgnoreCase)))
            return true;
        if (shellCmd.StartsWith("pm clear", StringComparison.OrdinalIgnoreCase) && r.Ok
            && !r.Combined.Contains("Exception", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private static bool IsAlreadyGone(ProcessResult r, string shellCmd)
    {
        var t = r.Combined;
        return t.Contains("not installed", StringComparison.OrdinalIgnoreCase)
               || t.Contains("Unknown package", StringComparison.OrdinalIgnoreCase)
               || t.Contains("Package does not exist", StringComparison.OrdinalIgnoreCase)
               || t.Contains("Failure [not installed", StringComparison.OrdinalIgnoreCase)
               || t.Contains("does not exist for user", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Lỗi “không làm được” nhưng không cần báo đỏ: clear bị chặn, disable protected, Failure -1000…
    /// </summary>
    private static bool IsSoftBlocked(ProcessResult r, string shellCmd)
    {
        var t = r.Combined;
        if (t.Contains("SecurityException", StringComparison.OrdinalIgnoreCase))
            return true;
        if (t.Contains("Cannot disable", StringComparison.OrdinalIgnoreCase))
            return true;
        if (t.Contains("Shell cannot change", StringComparison.OrdinalIgnoreCase))
            return true;
        if (t.Contains("Permission denial", StringComparison.OrdinalIgnoreCase)
            || t.Contains("Permission Denial", StringComparison.OrdinalIgnoreCase))
            return true;
        if (t.Contains("Failure [-1000]", StringComparison.OrdinalIgnoreCase)
            || t.Contains("DELETE_FAILED", StringComparison.OrdinalIgnoreCase))
            return true;
        if (t.Contains("DELETE_FAILED_INTERNAL_ERROR", StringComparison.OrdinalIgnoreCase))
            return true;
        // clear thường bị chặn trên system app — không phải lỗi nghiêm trọng
        if (shellCmd.StartsWith("pm clear", StringComparison.OrdinalIgnoreCase)
            && (t.Contains("Exception", StringComparison.OrdinalIgnoreCase) || !r.Ok))
            return true;
        return false;
    }

    private static string Short(string text)
    {
        text = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        // Rút gọn SecurityException dài
        if (text.Contains("SecurityException", StringComparison.OrdinalIgnoreCase))
            return "bị chặn (SecurityException)";
        if (text.Contains("Failure [-1000]", StringComparison.OrdinalIgnoreCase))
            return "không gỡ được (-1000)";
        if (text.Contains("Cannot disable", StringComparison.OrdinalIgnoreCase))
            return "không tắt được (protected)";
        return text.Length <= 80 ? text : text[..77] + "...";
    }

    [GeneratedRegex("""^pull\s+"([^"]+)"\s+"([^"]+)"\s*$""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PullLineRegex();
}
