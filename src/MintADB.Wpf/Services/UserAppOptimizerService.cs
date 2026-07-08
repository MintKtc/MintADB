using MintADB.Wpf.Models;

namespace MintADB.Wpf.Services;

public sealed class UserAppOptimizerService(AdbService adb, XiaomiCnOptimizer optimizer)
{
    private ShizukuService? _shizuku;
    private ShizukuService Shizuku => _shizuku ??= new ShizukuService(adb);

    public async Task GrantSelectedPermissionsAsync(
        string serial,
        string package,
        IEnumerable<AppPermissionOption> permissions,
        Action<string>? log = null,
        CancellationToken ct = default)
    {
        if (!await adb.PackageInstalledAsync(serial, package, ct))
        {
            log?.Invoke($"[SKIP] {package} — chưa cài");
            return;
        }

        foreach (var perm in permissions.Where(p => p.Selected))
        {
            if (perm.Kind == PermissionGrantKind.Miui)
            {
                await GrantMiuiPermissionAsync(serial, package, perm, log, ct);
                continue;
            }

            if (perm.Kind == PermissionGrantKind.Shizuku)
            {
                await GrantShizukuPermissionAsync(serial, package, perm, log, ct);
                continue;
            }

            if (perm.Kind == PermissionGrantKind.Runtime)
            {
                var r = await adb.ShellAsync($"pm grant {package} {perm.Value}", serial, ct);
                var ok = r.Ok || r.Combined.Contains("already", StringComparison.OrdinalIgnoreCase);
                var reason = "";
                if (!ok)
                {
                    if (r.Combined.Contains("not declared", StringComparison.OrdinalIgnoreCase))
                        reason = " — app không khai báo trong manifest";
                    else if (r.Combined.Contains("not a changeable permission type", StringComparison.OrdinalIgnoreCase))
                        reason = " — cần cấp thủ công trong Cài đặt";
                    else if (r.Combined.Contains("managed by role", StringComparison.OrdinalIgnoreCase))
                        reason = " — do hệ thống quản lý";
                    else if (r.Combined.Contains("Unknown permission", StringComparison.OrdinalIgnoreCase))
                        reason = " — quyền không tồn tại trên ROM này";
                }
                log?.Invoke($"[{(ok ? "OK" : "FAIL")}] {perm.Label}{reason}");
                continue;
            }

            if (perm.Kind == PermissionGrantKind.AppOp)
            {
                var r = await adb.ShellAsync($"cmd appops set {package} {perm.Value} allow", serial, ct);
                var ok = r.Ok || r.Combined.Contains("Success", StringComparison.OrdinalIgnoreCase);
                if (!ok)
                {
                    var r2 = await adb.ShellAsync($"appops set {package} {perm.Value} allow", serial, ct);
                    ok = r2.Ok || r2.Combined.Contains("Success", StringComparison.OrdinalIgnoreCase);
                }
                log?.Invoke($"[{(ok ? "OK" : "FAIL")}] {perm.Label} (AppOps){(ok ? "" : " — không hỗ trợ AppOps này")}");
                continue;
            }

            log?.Invoke($"[SKIP] {perm.Label} — loại không hỗ trợ");
        }
    }

    private async Task GrantMiuiPermissionAsync(
        string serial,
        string package,
        AppPermissionOption perm,
        Action<string>? log,
        CancellationToken ct)
    {
        switch (perm.Value)
        {
            case "autostart":
                await optimizer.EnableMiuiAutoStartAsync(serial, package, log, ct);
                log?.Invoke($"[OK] {perm.Group} · {perm.Label}");
                break;
            default:
                log?.Invoke($"[WARN] {perm.Group} · {perm.Label} — chưa hỗ trợ");
                break;
        }
    }

    private async Task GrantShizukuPermissionAsync(
        string serial,
        string package,
        AppPermissionOption perm,
        Action<string>? log,
        CancellationToken ct)
    {
        var isAppOp = perm.Value.StartsWith("android:", StringComparison.Ordinal)
                      || !perm.Value.Contains('.');
        if (isAppOp)
        {
            var op = perm.Value.Replace("android:", "");
            var r = await Shizuku.SetAppOpAsync(serial, package, op, "allow", ct);
            log?.Invoke($"[{(r.Ok ? "OK" : "WARN")}] {perm.Group} · {perm.Label} (AppOp)");
        }
        else
        {
            var r = await Shizuku.GrantPermissionAsync(serial, package, perm.Value, ct);
            log?.Invoke($"[{(r.Ok ? "OK" : "WARN")}] {perm.Group} · {perm.Label} (pm grant)");
        }
    }

    public Task OptimizeNotificationsAsync(
        string serial, AppPreset app, Action<string>? log = null, CancellationToken ct = default)
        => optimizer.FixAppNotificationsAsync(serial, app, log, ct);

    public Task DisableBatteryOptimizationAsync(
        string serial, string package, Action<string>? log = null, CancellationToken ct = default)
        => optimizer.DisableBatteryOptimizationAsync(serial, package, log, ct);

    public static AppPreset ToPreset(InstalledApp app)
    {
        var preset = AppPreset.Defaults.FirstOrDefault(p => p.Package == app.Package);
        return preset ?? new AppPreset { Name = app.Name, Package = app.Package };
    }
}