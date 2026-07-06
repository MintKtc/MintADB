using System.Diagnostics;
using System.IO;
using MintADB.Wpf.Models;

namespace MintADB.Wpf.Services;

public sealed class AdbToolsService(AdbService adb)
{
    public string AdbPath => adb.AdbPath;
    private static readonly string[] InfoProps =
    [
        "ro.product.manufacturer", "ro.product.brand", "ro.product.model",
        "ro.build.display.id", "ro.build.version.release", "ro.build.version.sdk",
        "ro.miui.ui.version.name", "ro.mi.os.version.name",
        "ro.miui.region", "ro.serialno", "ro.bootloader", "ro.debuggable",
    ];

    public static string MintAdbDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "MintADB");

    // ── Server / device ──
    public Task<ProcessResult> KillServerAsync(CancellationToken ct = default)
        => adb.RunGlobalAsync(["kill-server"], ct);

    public Task<ProcessResult> StartServerAsync(CancellationToken ct = default)
        => adb.RunGlobalAsync(["start-server"], ct);

    public Task<ProcessResult> WaitForDeviceAsync(string? serial = null, CancellationToken ct = default)
        => adb.RunAsync(["wait-for-device"], serial, ct);

    public Task<ProcessResult> RebootAsync(string serial, RebootMode mode, CancellationToken ct = default)
        => adb.ShellAsync(mode.AdbArg(), serial, ct);

    // ── Apps ──
    public async Task<ProcessResult> InstallApkAsync(string serial, string apkPath, CancellationToken ct = default)
    {
        var streamed = await adb.RunAsync(["install", "-r", "-d", apkPath], serial, ct);
        if (streamed.Ok || !IsUserRestrictedInstall(streamed.Combined))
            return streamed;

        var remote = BundledApkService.RemotePath(Path.GetFileName(apkPath));
        var pushed = await PushBundledApkAsync(serial, apkPath, ct);
        if (!pushed.Ok)
            return AppendInstallHint(pushed, streamed.Combined);

        foreach (var cmd in new[]
                 {
                     $"pm install -r -d \"{remote}\"",
                     $"cmd package install -r -d \"{remote}\"",
                 })
        {
            var shell = await adb.ShellAsync(cmd, serial, ct);
            if (shell.Ok || shell.Combined.Contains("Success", StringComparison.OrdinalIgnoreCase))
                return shell;
        }

        return AppendInstallHint(pushed, streamed.Combined);
    }

    public async Task<ProcessResult> PushBundledApkAsync(string serial, string localPath, CancellationToken ct = default)
    {
        await adb.ShellAsync($"mkdir -p {BundledApkService.DeviceFolder}", serial, ct);
        return await PushAsync(serial, localPath, BundledApkService.RemotePath(Path.GetFileName(localPath)), ct);
    }

    private static bool IsUserRestrictedInstall(string text) =>
        text.Contains("INSTALL_FAILED_USER_RESTRICTED", StringComparison.OrdinalIgnoreCase)
        || text.Contains("Install canceled by user", StringComparison.OrdinalIgnoreCase);

    private static ProcessResult AppendInstallHint(ProcessResult result, string streamedError)
    {
        var hint = "MIUI chặn cài qua ADB — bật «Cài đặt qua USB» trong Tùy chọn nhà phát triển, "
                   + "hoặc mở APK đã đẩy trong Tệp / Download / MintADB trên máy.";
        var detail = string.IsNullOrWhiteSpace(result.Combined) ? streamedError : result.Combined;
        return new ProcessResult(result.ExitCode, result.Output, $"{detail}\n{hint}");
    }

    public Task<ProcessResult> InstallSplitApkAsync(string serial, IEnumerable<string> apkPaths, CancellationToken ct = default)
        => adb.RunAsync(["install-multiple", "-r", .. apkPaths], serial, ct);

    public Task<ProcessResult> SideloadApkAsync(string apkPath, CancellationToken ct = default)
        => adb.RunGlobalAsync(["sideload", apkPath], ct);

    public async Task<PackageRemoveResult> UninstallAsync(
        string serial, string package, bool keepData = false, CancellationToken ct = default)
    {
        static bool IsPmSuccess(ProcessResult r) =>
            r.Ok || r.Combined.Contains("Success", StringComparison.OrdinalIgnoreCase);

        static bool IsShellFailure(string text) =>
            text.Contains("Error", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Exception", StringComparison.OrdinalIgnoreCase)
            || text.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Failure", StringComparison.OrdinalIgnoreCase);

        // App hệ thống / rác ROM Xiaomi: gỡ khỏi user 0 (không cần root)
        var user0 = keepData
            ? $"pm uninstall -k --user 0 {package}"
            : $"pm uninstall --user 0 {package}";
        var r = await adb.ShellAsync(user0, serial, ct);
        if (IsPmSuccess(r))
            return new PackageRemoveResult(PackageRemoveOutcome.Uninstalled, r.Combined);

        // App cài từ Play / APK người dùng
        var args = keepData
            ? new[] { "shell", "pm", "uninstall", "-k", package }
            : new[] { "uninstall", package };
        r = await adb.RunAsync(args, serial, ct);
        if (IsPmSuccess(r))
            return new PackageRemoveResult(PackageRemoveOutcome.Uninstalled, r.Combined);

        // App lõi không gỡ được → vô hiệu hóa
        var disabled = await adb.ShellAsync($"pm disable-user --user 0 {package}", serial, ct);
        if (disabled.Ok && !IsShellFailure(disabled.Combined))
            return new PackageRemoveResult(PackageRemoveOutcome.Disabled, disabled.Combined);

        // Fallback cuối: ẩn khỏi launcher
        var hidden = await adb.ShellAsync($"pm hide --user 0 {package}", serial, ct);
        if (hidden.Ok && !IsShellFailure(hidden.Combined))
            return new PackageRemoveResult(PackageRemoveOutcome.Hidden, hidden.Combined);

        return new PackageRemoveResult(PackageRemoveOutcome.Failed, r.Combined);
    }

    public Task<ProcessResult> DisablePackageAsync(string serial, string package, CancellationToken ct = default)
        => adb.ShellAsync($"pm disable-user --user 0 {package}", serial, ct);

    public Task<ProcessResult> EnablePackageAsync(string serial, string package, CancellationToken ct = default)
        => adb.ShellAsync($"pm enable {package}", serial, ct);

    public Task<ProcessResult> UnhidePackageAsync(string serial, string package, CancellationToken ct = default)
        => adb.ShellAsync($"pm unhide --user 0 {package}", serial, ct);

    public Task<ProcessResult> ReinstallPackageAsync(string serial, string package, CancellationToken ct = default)
        => adb.ShellAsync($"pm install-existing --user 0 {package}", serial, ct);

    public async Task<ProcessResult> RestoreInactivePackageAsync(
        string serial, string package, InactiveAppState state, CancellationToken ct = default)
    {
        return state switch
        {
            InactiveAppState.Uninstalled => await ReinstallPackageAsync(serial, package, ct),
            InactiveAppState.Hidden => await RestoreHiddenPackageAsync(serial, package, ct),
            _ => await EnablePackageAsync(serial, package, ct),
        };
    }

    private async Task<ProcessResult> RestoreHiddenPackageAsync(
        string serial, string package, CancellationToken ct)
    {
        await adb.ShellAsync($"pm unhide --user 0 {package}", serial, ct);
        return await EnablePackageAsync(serial, package, ct);
    }

    public Task<ProcessResult> ClearAppDataAsync(string serial, string package, CancellationToken ct = default)
        => adb.ShellAsync($"pm clear {package}", serial, ct);

    public Task<ProcessResult> ForceStopAsync(string serial, string package, CancellationToken ct = default)
        => adb.ShellAsync($"am force-stop {package}", serial, ct);

    public Task<ProcessResult> LaunchAppAsync(string serial, string package, CancellationToken ct = default)
        => adb.ShellAsync($"monkey -p {package} -c android.intent.category.LAUNCHER 1", serial, ct);

    public Task<ProcessResult> GrantPermissionAsync(string serial, string package, string permission, CancellationToken ct = default)
        => adb.ShellAsync($"pm grant {package} {permission}", serial, ct);

    public Task<ProcessResult> RevokePermissionAsync(string serial, string package, string permission, CancellationToken ct = default)
        => adb.ShellAsync($"pm revoke {package} {permission}", serial, ct);

    public Task<ProcessResult> SetAppOpsAsync(string serial, string package, string op, string mode, CancellationToken ct = default)
        => adb.ShellAsync($"cmd appops set {package} {op} {mode}", serial, ct);

    public Task<ProcessResult> DumpPackageAsync(string serial, string package, CancellationToken ct = default)
        => adb.ShellAsync($"dumpsys package {package}", serial, ct);

    public async Task<string?> BackupApkAsync(string serial, string package, string? saveDir = null, CancellationToken ct = default)
    {
        saveDir ??= Path.Combine(MintAdbDir, "Backups");
        Directory.CreateDirectory(saveDir);

        var pathR = await adb.ShellAsync($"pm path {package}", serial, ct);
        if (!pathR.Ok) return null;

        var remote = pathR.Output.Split('\n')
            .Select(l => l.Trim())
            .FirstOrDefault(l => l.StartsWith("package:", StringComparison.Ordinal))
            ?["package:".Length..];
        if (string.IsNullOrEmpty(remote)) return null;

        var local = Path.Combine(saveDir, $"{package}_{DateTime.Now:yyyyMMdd_HHmmss}.apk");
        var pull = await adb.RunAsync(["pull", remote, local], serial, ct);
        return pull.Ok && File.Exists(local) ? local : null;
    }

    // ── Files ──
    public Task<ProcessResult> PushAsync(string serial, string localPath, string remotePath, CancellationToken ct = default)
        => adb.RunAsync(["push", localPath, remotePath], serial, ct);

    public Task<ProcessResult> PullAsync(string serial, string remotePath, string localPath, CancellationToken ct = default)
        => adb.RunAsync(["pull", remotePath, localPath], serial, ct);

    public Task<ProcessResult> ListRemoteAsync(string serial, string remotePath, CancellationToken ct = default)
        => adb.ShellAsync($"ls -la {remotePath}", serial, ct);

    // ── Screen ──
    public async Task<string?> CaptureScreenshotAsync(string serial, string? saveDir = null, CancellationToken ct = default)
    {
        saveDir ??= Path.Combine(MintAdbDir, "Screenshots");
        Directory.CreateDirectory(saveDir);

        var remote = "/sdcard/mintadb_screen.png";
        var local = Path.Combine(saveDir, $"screen_{DateTime.Now:yyyyMMdd_HHmmss}.png");

        var cap = await adb.ShellAsync($"screencap -p {remote}", serial, ct);
        if (!cap.Ok) return null;

        var pull = await adb.RunAsync(["pull", remote, local], serial, ct);
        await adb.ShellAsync($"rm -f {remote}", serial, ct);
        return pull.Ok && File.Exists(local) ? local : null;
    }

    public async Task<string?> RecordScreenAsync(string serial, int seconds = 15, string? saveDir = null, CancellationToken ct = default)
    {
        saveDir ??= Path.Combine(MintAdbDir, "Recordings");
        Directory.CreateDirectory(saveDir);

        var remote = $"/sdcard/mintadb_rec_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
        var local = Path.Combine(saveDir, Path.GetFileName(remote));

        await adb.ShellAsync($"screenrecord --time-limit {seconds} {remote}", serial, ct);
        var pull = await adb.RunAsync(["pull", remote, local], serial, ct);
        await adb.ShellAsync($"rm -f {remote}", serial, ct);
        return pull.Ok && File.Exists(local) ? local : null;
    }

    public Task<ProcessResult> SetDensityAsync(string serial, int dpi, CancellationToken ct = default)
        => adb.ShellAsync($"wm density {dpi}", serial, ct);

    public Task<ProcessResult> ResetDensityAsync(string serial, CancellationToken ct = default)
        => adb.ShellAsync("wm density reset", serial, ct);

    public Task<ProcessResult> SetSizeAsync(string serial, int w, int h, CancellationToken ct = default)
        => adb.ShellAsync($"wm size {w}x{h}", serial, ct);

    public Task<ProcessResult> ResetSizeAsync(string serial, CancellationToken ct = default)
        => adb.ShellAsync("wm size reset", serial, ct);

    public Task<ProcessResult> OpenUrlAsync(string serial, string url, CancellationToken ct = default)
        => adb.ShellAsync($"am start -a android.intent.action.VIEW -d \"{url}\"", serial, ct);

    public Task<byte[]?> CaptureScreenshotBytesAsync(string serial, CancellationToken ct = default)
        => adb.ExecOutAsync(["exec-out", "screencap", "-p"], serial, ct);

    public Task<byte[]?> CaptureScreenshotRawAsync(string serial, CancellationToken ct = default)
        => adb.ExecOutAsync(["exec-out", "screencap"], serial, ct);

    public async Task<string> GetScreenInfoAsync(string serial, CancellationToken ct = default)
    {
        var lines = new List<string>();
        foreach (var cmd in new[] { "wm size", "wm density" })
        {
            var r = await adb.ShellAsync(cmd, serial, ct);
            if (r.Ok && !string.IsNullOrWhiteSpace(r.Output))
                lines.Add(r.Output.Trim());
        }

        return lines.Count > 0 ? string.Join(" · ", lines) : "Không đọc được thông tin màn hình";
    }

    // ── Network ──
    public Task<ProcessResult> TcpipAsync(string serial, int port = 5555, CancellationToken ct = default)
        => adb.RunAsync(["tcpip", port.ToString()], serial, ct);

    public Task<ProcessResult> ConnectAsync(string hostPort, CancellationToken ct = default)
        => adb.RunGlobalAsync(["connect", hostPort], ct);

    public Task<ProcessResult> PairAsync(string hostPort, string code, CancellationToken ct = default)
        => adb.RunGlobalAsync(["pair", hostPort, code], ct);

    public Task<ProcessResult> DisconnectAsync(string? hostPort = null, CancellationToken ct = default)
        => hostPort is null
            ? adb.RunGlobalAsync(["disconnect"], ct)
            : adb.RunGlobalAsync(["disconnect", hostPort], ct);

    public Task<ProcessResult> ForwardAsync(string spec, string? serial = null, CancellationToken ct = default)
        => adb.RunAsync(["forward", spec], serial, ct);

    public Task<ProcessResult> ForwardListAsync(string? serial = null, CancellationToken ct = default)
        => adb.RunAsync(["forward", "--list"], serial, ct);

    public Task<ProcessResult> ForwardRemoveAsync(string spec, string? serial = null, CancellationToken ct = default)
        => adb.RunAsync(["forward", "--remove", spec], serial, ct);

    public async Task<ProcessResult> SetPrivateDnsAsync(string serial, string hostname, CancellationToken ct = default)
    {
        var mode = await adb.ShellAsync("settings put global private_dns_mode hostname", serial, ct);
        if (!mode.Ok) return mode;
        return await adb.ShellAsync($"settings put global private_dns_specifier {hostname}", serial, ct);
    }

    public async Task<ProcessResult> ClearPrivateDnsAsync(string serial, CancellationToken ct = default)
    {
        await adb.ShellAsync("settings put global private_dns_specifier \"\"", serial, ct);
        return await adb.ShellAsync("settings put global private_dns_mode off", serial, ct);
    }

    public async Task<string> GetPrivateDnsStatusAsync(string serial, CancellationToken ct = default)
    {
        var mode = (await adb.ShellAsync("settings get global private_dns_mode", serial, ct)).Output.Trim();
        var host = (await adb.ShellAsync("settings get global private_dns_specifier", serial, ct)).Output.Trim();
        if (mode is "null" or "") mode = "off";
        if (host is "null" or "") host = "—";
        return $"mode={mode} · host={host}";
    }

    public async Task<ProcessResult> SetPreferredNetworkModeAsync(
        string serial, int mode, int simSlot = 0, CancellationToken ct = default)
    {
        var key = MobileNetworkMode.SettingsKey(simSlot);
        var r = await adb.ShellAsync($"settings put global {key} {mode}", serial, ct);
        if (!r.Ok) return r;

        if (simSlot == 0)
            await adb.ShellAsync($"settings put global preferred_network_mode1 {mode}", serial, ct);

        await adb.ShellAsync("svc data disable", serial, ct);
        await Task.Delay(800, ct);
        var enable = await adb.ShellAsync("svc data enable", serial, ct);
        return enable.Ok ? r : enable;
    }

    public async Task<string> GetPreferredNetworkModeStatusAsync(
        string serial, int simSlot = 0, CancellationToken ct = default)
    {
        var key = MobileNetworkMode.SettingsKey(simSlot);
        var raw = (await adb.ShellAsync($"settings get global {key}", serial, ct)).Output.Trim();
        if (raw is "null" or "" && simSlot != 0)
        {
            raw = (await adb.ShellAsync("settings get global preferred_network_mode", serial, ct)).Output.Trim();
            key = "preferred_network_mode";
        }

        if (raw is "null" or "" || !int.TryParse(raw, out var mode))
            return $"{key}=không đọc được";

        return $"{key}={mode} · {MobileNetworkMode.Describe(mode)}";
    }

    public async Task<ProcessResult> SetSystemLocaleAsync(string serial, string locale, CancellationToken ct = default)
    {
        if (!TryNormalizeLocale(locale, out var normalized))
            return new ProcessResult(1, "", "Locale không hợp lệ (ví dụ: vi-VN, en-US)");

        var parts = normalized.Split('-');
        var lang = parts[0];
        var country = parts[1];

        var primary = await adb.ShellAsync($"settings put system system_locales {normalized}", serial, ct);

        await adb.ShellAsync($"setprop persist.sys.locale {normalized}", serial, ct);
        await adb.ShellAsync($"setprop persist.sys.language {lang}", serial, ct);
        await adb.ShellAsync($"setprop persist.sys.country {country}", serial, ct);
        await adb.ShellAsync($"cmd locale set-locale {normalized}", serial, ct);
        await adb.ShellAsync("am broadcast -a android.intent.action.LOCALE_CHANGED", serial, ct);

        return primary;
    }

    public async Task<string> GetSystemLocaleStatusAsync(string serial, CancellationToken ct = default)
    {
        var systemLocales = (await adb.ShellAsync("settings get system system_locales", serial, ct)).Output.Trim();
        var persistLocale = await adb.GetPropAsync(serial, "persist.sys.locale", ct);
        var language = await adb.GetPropAsync(serial, "persist.sys.language", ct);
        var country = await adb.GetPropAsync(serial, "persist.sys.country", ct);

        if (systemLocales is "null" or "") systemLocales = "—";
        if (persistLocale is "null" or "") persistLocale = "—";
        if (language is "null" or "") language = "—";
        if (country is "null" or "") country = "—";

        return $"system_locales={systemLocales} · persist.sys.locale={persistLocale} · lang={language} · country={country}";
    }

    public static bool TryNormalizeLocale(string raw, out string locale)
    {
        locale = "";
        raw = raw.Trim();
        if (raw.Length == 0) return false;

        var m = System.Text.RegularExpressions.Regex.Match(raw, @"^([a-zA-Z]{2,3})-([a-zA-Z]{2,4})$");
        if (!m.Success) return false;

        locale = $"{m.Groups[1].Value.ToLowerInvariant()}-{m.Groups[2].Value.ToUpperInvariant()}";
        return true;
    }

    public Task<ProcessResult> ReverseAsync(string spec, string? serial = null, CancellationToken ct = default)
        => adb.RunAsync(["reverse", spec], serial, ct);

    // ── Input ──
    public Task<ProcessResult> InputTextAsync(string serial, string text, CancellationToken ct = default)
        => adb.ShellAsync($"input text {AdbService.ShellSingleQuote(text.Replace(" ", "%s"))}", serial, ct);

    public Task<ProcessResult> InputKeyeventAsync(string serial, int keyCode, CancellationToken ct = default)
        => adb.ShellAsync($"input keyevent {keyCode}", serial, ct);

    public Task<ProcessResult> InputTapAsync(string serial, int x, int y, CancellationToken ct = default)
        => adb.ShellAsync($"input tap {x} {y}", serial, ct);

    public Task<ProcessResult> InputSwipeAsync(string serial, int x1, int y1, int x2, int y2, int ms, CancellationToken ct = default)
        => adb.ShellAsync($"input swipe {x1} {y1} {x2} {y2} {ms}", serial, ct);

    // ── System / dumpsys ──
    public Task<ProcessResult> GetLogcatAsync(string serial, int lines = 200, CancellationToken ct = default)
        => adb.ShellAsync($"logcat -d -t {lines}", serial, ct);

    public Task<ProcessResult> ClearLogcatAsync(string serial, CancellationToken ct = default)
        => adb.ShellAsync("logcat -c", serial, ct);

    public Task<ProcessResult> DumpsysAsync(string serial, string service, CancellationToken ct = default)
        => adb.ShellAsync($"dumpsys {service}", serial, ct);

    public Task<ProcessResult> GetCurrentActivityAsync(string serial, CancellationToken ct = default)
        => adb.ShellAsync("dumpsys activity activities | grep mResumedActivity", serial, ct);

    public Task<ProcessResult> RootCheckAsync(string serial, CancellationToken ct = default)
        => adb.ShellAsync("su -c id", serial, ct);

    public Task<ProcessResult> RemountAsync(string serial, CancellationToken ct = default)
        => adb.RunAsync(["remount"], serial, ct);

    public Task<ProcessResult> TrimCachesAsync(string serial, CancellationToken ct = default)
        => adb.ShellAsync("pm trim-caches 999G", serial, ct);

    public Task<ProcessResult> ListUsersAsync(string serial, CancellationToken ct = default)
        => adb.ShellAsync("pm list users", serial, ct);

    public async Task<ProcessResult> SettingsGetAsync(string serial, string ns, string key, CancellationToken ct = default)
    {
        var val = await adb.SettingsGetAsync(serial, ns, key, ct);
        return new ProcessResult(0, val, "");
    }

    public Task<ProcessResult> SettingsPutAsync(string serial, string ns, string key, string value, CancellationToken ct = default)
        => adb.SettingsPutAsync(serial, ns, key, value, ct);

    public Task<ProcessResult> SetHiddenApiPolicyAsync(string serial, int policy, CancellationToken ct = default)
        => adb.ShellAsync($"settings put global hidden_api_policy {policy}", serial, ct);

    public async Task<string?> SaveBugreportAsync(string serial, string? saveDir = null, CancellationToken ct = default)
    {
        saveDir ??= Path.Combine(MintAdbDir, "Bugreports");
        Directory.CreateDirectory(saveDir);
        var local = Path.Combine(saveDir, $"bugreport_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        var r = await adb.ShellAsync("bugreport", serial, ct);
        if (!string.IsNullOrWhiteSpace(r.Combined))
            await File.WriteAllTextAsync(local, r.Combined, ct);
        return File.Exists(local) ? local : null;
    }

    public Task<ProcessResult> RunShellAsync(string serial, string command, CancellationToken ct = default)
        => adb.ShellAsync(command, serial, ct);

    public async Task<string> GetDeviceInfoAsync(string serial, CancellationToken ct = default)
    {
        var lines = new List<string>();
        foreach (var prop in InfoProps)
        {
            var val = await adb.GetPropAsync(serial, prop, ct);
            if (!string.IsNullOrWhiteSpace(val))
                lines.Add($"{prop}: {val}");
        }

        foreach (var cmd in new[] { "wm size", "wm density" })
        {
            var r = await adb.ShellAsync(cmd, serial, ct);
            if (r.Ok && !string.IsNullOrWhiteSpace(r.Output))
                lines.Add($"{cmd}: {r.Output.Trim()}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    public bool TryLaunchScrcpy(string serial, int maxSize, bool stayAwake, out string message)
    {
        var scrcpy = ScrcpyLocator.Find(adb.AdbPath);
        if (scrcpy is null)
        {
            message = "Không tìm thấy scrcpy.exe.\n\n"
                      + "Gợi ý:\n"
                      + "· Đặt biến SCRCPY_PATH trỏ tới scrcpy.exe\n"
                      + "· Hoặc copy scrcpy vào cạnh adb.exe\n\n"
                      + "Đã tìm:\n"
                      + ScrcpyLocator.SearchSummary(adb.AdbPath);
            return false;
        }

        try
        {
            var dir = Path.GetDirectoryName(scrcpy)!;
            var args = $"-s {serial} --no-audio --max-fps=120 -b 25M --max-size={maxSize} --window-title=\"MintADB Scrcpy\"";
            if (stayAwake)
                args += " --stay-awake";

            var psi = new ProcessStartInfo
            {
                FileName = scrcpy,
                Arguments = args,
                WorkingDirectory = dir,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.Environment["ADB"] = adb.AdbPath;
            psi.Environment["ANDROID_SERIAL"] = serial;
            Process.Start(psi);
            message = stayAwake
                ? $"Scrcpy {maxSize}p · 120 FPS · giữ màn hình sáng\n{scrcpy}"
                : $"Scrcpy {maxSize}p · 120 FPS\n{scrcpy}";
            return true;
        }
        catch (Exception ex)
        {
            message = $"Không chạy được scrcpy:\n{ex.Message}";
            return false;
        }
    }

    public bool TryLaunchScrcpy(string serial, out string message)
        => TryLaunchScrcpy(serial, 1080, stayAwake: true, out message);
}

public enum PackageRemoveOutcome { Uninstalled, Disabled, Hidden, Failed }

public readonly record struct PackageRemoveResult(PackageRemoveOutcome Outcome, string Detail)
{
    public bool Ok => Outcome != PackageRemoveOutcome.Failed;
}