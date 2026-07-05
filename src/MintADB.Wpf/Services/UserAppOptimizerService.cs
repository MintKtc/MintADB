using MintADB.Wpf.Models;

namespace MintADB.Wpf.Services;

public sealed class UserAppOptimizerService(AdbService adb, XiaomiCnOptimizer optimizer)
{
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

            var r = perm.Kind switch
            {
                PermissionGrantKind.Runtime =>
                    await adb.ShellAsync($"pm grant {package} {perm.Value}", serial, ct),
                PermissionGrantKind.AppOp =>
                    await adb.ShellAsync($"cmd appops set {package} {perm.Value} allow", serial, ct),
                _ => new ProcessResult(1, "", "Không hỗ trợ"),
            };

            var ok = r.Ok
                     || r.Combined.Contains("already", StringComparison.OrdinalIgnoreCase)
                     || r.Combined.Contains("Success", StringComparison.OrdinalIgnoreCase);
            log?.Invoke($"[{(ok ? "OK" : "WARN")}] {perm.Group} · {perm.Label}");
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