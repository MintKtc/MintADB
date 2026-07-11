using System.Text.RegularExpressions;
using MintADB.Wpf.Models;

namespace MintADB.Wpf.Services;

public sealed class ShizukuService(AdbService adb)
{
    public const string Package = "moe.shizuku.privileged.api";
    public const string ApiPermission = "moe.shizuku.manager.permission.API_V23";

    public static readonly (string Label, string Permission)[] PrivilegedPermissions =
    [
        ("Quyền API Shizuku", ApiPermission),
        ("WRITE_SECURE_SETTINGS", "android.permission.WRITE_SECURE_SETTINGS"),
        ("WRITE_SETTINGS", "android.permission.WRITE_SETTINGS"),
        ("DUMP", "android.permission.DUMP"),
        ("READ_LOGS", "android.permission.READ_LOGS"),
        ("POST_NOTIFICATIONS", "android.permission.POST_NOTIFICATIONS"),
        ("Bỏ qua tối ưu pin", "android.permission.REQUEST_IGNORE_BATTERY_OPTIMIZATIONS"),
        ("MANAGE_EXTERNAL_STORAGE", "android.permission.MANAGE_EXTERNAL_STORAGE"),
        ("ACCESS_BACKGROUND_LOCATION", "android.permission.ACCESS_BACKGROUND_LOCATION"),
    ];

    private static readonly string[] StartScriptPaths =
    [
        "/sdcard/Android/data/moe.shizuku.privileged.api/start.sh",
        "/storage/emulated/0/Android/data/moe.shizuku.privileged.api/start.sh",
    ];

    public async Task<ShizukuStatusResult> GetStatusAsync(string serial, CancellationToken ct = default)
    {
        var installed = await adb.PackageInstalledAsync(serial, Package, ct);
        string? version = null;
        var running = false;

        if (installed)
        {
            version = await ReadVersionAsync(serial, ct);
            running = await IsRunningAsync(serial, ct);
        }

        var summary = !installed
            ? "Chưa cài Shizuku (moe.shizuku.privileged.api)"
            : running
                ? $"Đang chạy · v{version ?? "?"}"
                : $"Đã cài v{version ?? "?"} · chưa khởi động";

        return new ShizukuStatusResult
        {
            Installed = installed,
            Running = running,
            Version = version,
            SummaryText = summary,
        };
    }

    public async Task<ProcessResult> StartAsync(string serial, CancellationToken ct = default)
    {
        if (!await adb.PackageInstalledAsync(serial, Package, ct))
            return new ProcessResult(1, "", "Chưa cài Shizuku — cài app và mở một lần trước.");

        ProcessResult? last = null;
        foreach (var path in StartScriptPaths)
        {
            var r = await adb.ShellAsync($"sh {path}", serial, ct);
            last = r;
            if (IsStartSuccess(r))
                return r;
        }

        return last ?? new ProcessResult(1, "", "Không tìm thấy start.sh — mở app Shizuku trên máy một lần.");
    }

    public Task<ProcessResult> LaunchManagerAsync(string serial, CancellationToken ct = default)
        => adb.ShellAsync($"monkey -p {Package} -c android.intent.category.LAUNCHER 1", serial, ct);

    public Task<ProcessResult> GrantPermissionAsync(
        string serial, string targetPackage, string permission, CancellationToken ct = default)
        => adb.PmGrantAsync(serial, targetPackage, permission, ct);

    public Task<ProcessResult> RevokePermissionAsync(
        string serial, string targetPackage, string permission, CancellationToken ct = default)
        => adb.PmRevokeAsync(serial, targetPackage, permission, ct);

    public Task<ProcessResult> SetAppOpAsync(
        string serial, string targetPackage, string op, string mode, CancellationToken ct = default)
        => adb.AppOpsSetAsync(serial, targetPackage, op, mode, ct);

    public async Task<IReadOnlyList<(string Label, bool Ok, string Detail)>> GrantPrivilegedBundleAsync(
        string serial, string targetPackage, CancellationToken ct = default)
    {
        var results = new List<(string, bool, string)>();
        foreach (var (label, permission) in PrivilegedPermissions)
        {
            var r = await GrantPermissionAsync(serial, targetPackage, permission, ct);
            results.Add((label, r.Ok, r.Combined));
        }

        foreach (var (label, op) in AppOpGrants)
        {
            var r = await SetAppOpAsync(serial, targetPackage, op, "allow", ct);
            results.Add((label, r.Ok, r.Combined));
        }

        return results;
    }

    public static readonly (string Label, string Op)[] AppOpGrants =
    [
        ("Thống kê sử dụng", "android:get_usage_stats"),
        ("Hiển thị trên app khác", "android:system_alert_window"),
        ("Chạy nền", "RUN_IN_BACKGROUND"),
        ("Bỏ qua pin (AppOps)", "android:request_ignore_battery_optimizations"),
    ];

    private async Task<bool> IsRunningAsync(string serial, CancellationToken ct)
    {
        var ps = await adb.ShellAsync("ps -A | grep -i shizuku", serial, ct);
        if (ps.Ok && ps.Output.Contains("shizuku", StringComparison.OrdinalIgnoreCase))
            return true;

        var svc = await adb.ShellAsync($"dumpsys activity services {Package}", serial, ct);
        return svc.Ok
               && (svc.Combined.Contains("ShizukuService", StringComparison.OrdinalIgnoreCase)
                   || svc.Combined.Contains("running=true", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<string?> ReadVersionAsync(string serial, CancellationToken ct)
    {
        var dump = await adb.ShellAsync($"dumpsys package {Package}", serial, ct);
        if (!dump.Ok) return null;

        var m = Regex.Match(dump.Combined, @"versionName=([^\s]+)");
        return m.Success ? m.Groups[1].Value : null;
    }

    private static bool IsStartSuccess(ProcessResult r)
    {
        if (!r.Ok) return false;
        var text = r.Combined;
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (text.Contains("No such file", StringComparison.OrdinalIgnoreCase)) return false;
        if (text.Contains("not found", StringComparison.OrdinalIgnoreCase)) return false;
        if (text.Contains("error", StringComparison.OrdinalIgnoreCase)
            && !text.Contains("already", StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }
}