using MintADB.Wpf.Models;

namespace MintADB.Wpf.Services;

public sealed class XiaomiCnOptimizer(AdbService adb)
{
    // MIUI/HyperOS power whitelists (forums: XDA, Voz, Mobile01, GitHub gists)
    private static readonly string[] MiuiPkgWhitelists =
    [
        "rt_pkg_white_list", "power_pkg_white_list", "power_alarm_white_list",
        "power_broadcast_white_list", "perf_proc_protect_list", "frozen_new_whitelist",
        "doze_whitelist_apps", "cluster_whitelist", "msystem_whitelist",
        "battery_optimization_whitelist_apps",
        "cloud_lowlatency_whitelist", // HyperOS 3 CN — push latency fix (llelectronics gist)
    ];

    private static readonly (string Ns, string Key, string Value)[] GlobalRelax =
    [
        ("global", "app_standby_enabled", "0"),
        ("secure", "adaptive_battery_management_enabled", "0"),
        ("global", "power_supersave_mode_enabled", "0"),
        ("global", "notification_listener_timeout", "0"),
        ("global", "miui_restricted_mode_enabled", "0"),
        ("global", "cached_apps_freezer_enabled", "0"),
        ("global", "app_hibernation_enabled", "0"),
        ("global", "settings_enable_monitor_phantom_procs", "false"),
    ];

    private static readonly (string Ns, string Key, string Value)[] ChinaUnlock =
    [
        ("global", "tn_disable_cloud_strategy", "1"),
        ("system", "POWER_CLOUD_INTERCEPT_ENABLE", "1"),
        ("global", "app_auto_startup_switch", "1"),
        ("global", "app_force_stop_behavior", "0"),
        ("secure", "forced_app_standby_enabled", "0"),
        ("global", "app_auto_revive_enabled", "1"),
        ("global", "app_kill_protection_enabled", "1"),
        ("global", "miui_optimization_whitelist_enabled", "1"),
        ("global", "miui_app_control_enabled", "0"),
    ];

    private static readonly string[] AppOpsModes =
    [
        "RUN_IN_BACKGROUND", "RUN_ANY_IN_BACKGROUND", "WAKE_LOCK", "START_FOREGROUND",
        "POST_NOTIFICATION", "IGNORE_BATTERY_OPTIMIZATIONS",
        "SYSTEM_ALERT_WINDOW", "COARSE_LOCATION", "FINE_LOCATION",
        "CAMERA", "RECORD_AUDIO", "READ_CONTACTS", "WRITE_CONTACTS",
        "READ_SMS", "SEND_SMS", "READ_PHONE_STATE",
        "READ_MEDIA_IMAGES", "READ_MEDIA_VIDEO", "READ_MEDIA_AUDIO",
    ];

    private static readonly string[] GrantPermissions =
    [
        "android.permission.POST_NOTIFICATIONS",
        "android.permission.RECEIVE_BOOT_COMPLETED",
        "android.permission.VIBRATE",
        "android.permission.ACCESS_NETWORK_STATE",
        "android.permission.ACCESS_WIFI_STATE",
        "android.permission.INTERNET",
        "android.permission.ACCESS_FINE_LOCATION",
        "android.permission.ACCESS_COARSE_LOCATION",
        "android.permission.ACCESS_BACKGROUND_LOCATION",
        "android.permission.CAMERA",
        "android.permission.READ_MEDIA_IMAGES",
        "android.permission.READ_CONTACTS",
        "android.permission.WRITE_CONTACTS",
        "android.permission.READ_PHONE_STATE",
        "android.permission.READ_PHONE_NUMBERS",
        "android.permission.CALL_PHONE",
        "android.permission.ANSWER_PHONE_CALLS",
        "android.permission.READ_CALL_LOG",
        "android.permission.SEND_SMS",
        "android.permission.READ_SMS",
        "android.permission.READ_CALENDAR",
        "android.permission.WRITE_CALENDAR",
        "android.permission.READ_EXTERNAL_STORAGE",
        "android.permission.WRITE_EXTERNAL_STORAGE",
        "android.permission.READ_MEDIA_VIDEO",
        "android.permission.READ_MEDIA_AUDIO",
        "android.permission.MANAGE_EXTERNAL_STORAGE",
        "android.permission.ACCESS_MEDIA_LOCATION",
        "android.permission.RECORD_AUDIO",
        "android.permission.FOREGROUND_SERVICE",
        "android.permission.FOREGROUND_SERVICE_DATA_SYNC",
        "android.permission.FOREGROUND_SERVICE_MEDIA_PLAYBACK",
        "android.permission.FOREGROUND_SERVICE_LOCATION",
        "android.permission.FOREGROUND_SERVICE_CAMERA",
        "android.permission.FOREGROUND_SERVICE_MICROPHONE",
        "android.permission.SYSTEM_ALERT_WINDOW",
        "android.permission.REQUEST_IGNORE_BATTERY_OPTIMIZATIONS",
        "android.permission.BLUETOOTH_CONNECT",
        "android.permission.NFC",
        "android.permission.SCHEDULE_EXACT_ALARM",
        "android.permission.USE_BIOMETRIC",
        "android.permission.USE_FINGERPRINT",
    ];

    public async Task<RomInfo> DetectRomAsync(string serial, CancellationToken ct = default)
    {
        var manufacturer = (await adb.GetPropAsync(serial, "ro.product.manufacturer", ct)).ToLowerInvariant();
        var brand = (await adb.GetPropAsync(serial, "ro.product.brand", ct)).ToLowerInvariant();
        var isXiaomi = manufacturer is "xiaomi" or "redmi" or "poco" || brand is "xiaomi" or "redmi" or "poco";

        var region = await adb.GetPropAsync(serial, "ro.miui.region", ct);
        if (string.IsNullOrEmpty(region))
            region = await adb.GetPropAsync(serial, "ro.product.locale.region", ct);

        var locale = await adb.GetPropAsync(serial, "ro.product.locale", ct);
        var build = await adb.GetPropAsync(serial, "ro.build.display.id", ct);
        var hyper = await adb.GetPropAsync(serial, "ro.mi.os.version.name", ct);
        var miui = await adb.GetPropAsync(serial, "ro.miui.ui.version.name", ct);

        var blob = (region + locale + build).ToLowerInvariant();
        var isChina = blob.Contains("cn") || blob.Contains("china") || build.EndsWith("CNXM", StringComparison.OrdinalIgnoreCase);

        return new RomInfo
        {
            IsXiaomi = isXiaomi,
            IsChina = isChina,
            Region = region,
            Build = build,
            OsVersion = !string.IsNullOrEmpty(hyper) ? hyper : miui,
            IsHyperOs = !string.IsNullOrEmpty(hyper),
        };
    }

    public async Task ApplyGlobalRelaxAsync(string serial, Action<string>? log = null, CancellationToken ct = default)
    {
        log?.Invoke("== Tối ưu Android Global ==");
        foreach (var (ns, key, val) in GlobalRelax)
        {
            var r = await adb.SettingsPutAsync(serial, ns, key, val, ct);
            log?.Invoke($"[{(r.Ok ? "OK" : "FAIL")}] settings {ns} {key}={val}");
        }
    }

    public async Task ApplyChinaUnlockAsync(string serial, Action<string>? log = null, CancellationToken ct = default)
    {
        log?.Invoke("== Mở khóa kiểm soát Xiaomi China ==");
        foreach (var (ns, key, val) in ChinaUnlock)
        {
            var r = await adb.SettingsPutAsync(serial, ns, key, val, ct);
            log?.Invoke($"[{(r.Ok ? "OK" : "FAIL")}] settings {ns} {key}={val}");
        }
    }

    public async Task FixAppNotificationsAsync(
        string serial, AppPreset app, Action<string>? log = null, CancellationToken ct = default)
    {
        if (!await adb.PackageInstalledAsync(serial, app.Package, ct))
        {
            log?.Invoke($"[SKIP] {app.Name} ({app.Package}) — chưa cài");
            return;
        }

        log?.Invoke($"--- Fix thông báo: {app.Name} ({app.Package}) ---");
        var pkg = app.Package;

        // ── PHASE 1: Reset state ──
        log?.Invoke("[Phase 1] Reset state...");

        var freeze = await adb.ShellAsync($"pm unsuspend {pkg}", serial, ct);
        log?.Invoke($"  [{(freeze.Ok ? "OK" : "SKIP")}] unfreeze");

        var enable = await adb.ShellAsync($"pm enable {pkg}", serial, ct);
        log?.Invoke($"  [{(enable.Ok ? "OK" : "SKIP")}] enable");

        var stopped = await adb.ShellAsync($"monkey -p {pkg} -c android.intent.category.LAUNCHER 1", serial, ct);
        log?.Invoke($"  [{(stopped.Ok ? "OK" : "SKIP")}] clear stopped");

        // ── PHASE 2: Grant permissions ──
        log?.Invoke("[Phase 2] Grant permissions...");
        foreach (var perm in GrantPermissions)
        {
            var r = await adb.ShellAsync($"pm grant {pkg} {perm}", serial, ct);
            if (r.Ok || r.Combined.Contains("already", StringComparison.OrdinalIgnoreCase))
                log?.Invoke($"  [OK] {perm.Split('.')[^1]}");
        }

        // ── PHASE 3: MIUI whitelists ──
        log?.Invoke("[Phase 3] MIUI whitelists...");
        foreach (var key in MiuiPkgWhitelists)
            await AppendWhitelistAsync(serial, key, [pkg], log, ct);

        if (app.Processes.Length > 0)
            await AppendWhitelistAsync(serial, "power_proc_white_list", app.Processes, log, ct);
        if (app.Services.Length > 0)
            await AppendWhitelistAsync(serial, "power_service_white_list", app.Services, log, ct);

        // ── PHASE 4: DeviceIdle + Standby ──
        log?.Invoke("[Phase 4] DeviceIdle + Standby...");
        var idle = await adb.ShellAsync($"cmd deviceidle whitelist +{pkg}", serial, ct);
        log?.Invoke($"  [{(idle.Ok ? "OK" : "FAIL")}] deviceidle whitelist");

        var bucket = await adb.ShellAsync($"am set-standby-bucket {pkg} active", serial, ct);
        log?.Invoke($"  [{(bucket.Ok ? "OK" : "FAIL")}] standby-bucket active");

        // ── PHASE 5: AppOps ──
        log?.Invoke("[Phase 5] AppOps...");
        foreach (var mode in AppOpsModes)
        {
            var r = await adb.ShellAsync($"cmd appops set {pkg} {mode} allow", serial, ct);
            log?.Invoke($"  [{(r.Ok ? "OK" : "WARN")}] {mode}");
        }

        // ── PHASE 6: MIUI Autostart ──
        log?.Invoke("[Phase 6] MIUI autostart...");
        var broadcasts = new (string Label, string Cmd)[]
        {
            ("POWER_HIDE_MODE", $"am broadcast -a miui.intent.action.POWER_HIDE_MODE_APP_LIST --es package_name {pkg} --ez enable true"),
            ("OP_AUTO_START allow", $"am broadcast -a miui.intent.action.OP_AUTO_START --es auto_start_service_pkg {pkg} --ez allow true"),
            ("OP_AUTO_START enable", $"am broadcast -a miui.intent.action.OP_AUTO_START --es auto_start_service_pkg {pkg} --ez enable true"),
        };
        foreach (var (label, cmd) in broadcasts)
        {
            var r = await adb.ShellAsync(cmd, serial, ct);
            log?.Invoke($"  [{(r.Ok ? "OK" : "WARN")}] {label}");
        }

        // ── PHASE 7: Tắt MIUI restrictions ──
        log?.Invoke("[Phase 7] Tắt MIUI restrictions...");
        foreach (var (ns, key, val) in GlobalRelax)
        {
            var r = await adb.SettingsPutAsync(serial, ns, key, val, ct);
            log?.Invoke($"  [{(r.Ok ? "OK" : "FAIL")}] {key}={val}");
        }

        log?.Invoke($"--- Done: {app.Name} ---");
    }

    public async Task EnableMiuiAutoStartAsync(
        string serial, string package, Action<string>? log = null, CancellationToken ct = default)
    {
        if (!await adb.PackageInstalledAsync(serial, package, ct))
        {
            log?.Invoke($"[SKIP] {package} — chưa cài");
            return;
        }

        log?.Invoke($"--- MIUI Auto Start: {package} ---");
        await ApplyMiuiAutoStartAsync(serial, package, log, ct);
    }

    public async Task DisableBatteryOptimizationAsync(
        string serial, string package, Action<string>? log = null, CancellationToken ct = default)
    {
        if (!await adb.PackageInstalledAsync(serial, package, ct))
        {
            log?.Invoke($"[SKIP] {package} — chưa cài");
            return;
        }

        log?.Invoke($"--- Tắt tối ưu pin: {package} ---");

        foreach (var key in MiuiPkgWhitelists)
            await AppendWhitelistAsync(serial, key, [package], log, ct);

        var idle = await adb.ShellAsync($"dumpsys deviceidle whitelist +{package}", serial, ct);
        log?.Invoke($"[{(idle.Ok ? "OK" : "FAIL")}] deviceidle whitelist");

        var idleCmd = await adb.ShellAsync($"cmd deviceidle whitelist +{package}", serial, ct);
        log?.Invoke($"[{(idleCmd.Ok ? "OK" : "WARN")}] cmd deviceidle whitelist");

        var bucket = await adb.ShellAsync($"am set-standby-bucket {package} active", serial, ct);
        log?.Invoke($"[{(bucket.Ok ? "OK" : "FAIL")}] standby-bucket active");

        foreach (var mode in new[] { "IGNORE_BATTERY_OPTIMIZATIONS", "RUN_IN_BACKGROUND", "RUN_ANY_IN_BACKGROUND", "WAKE_LOCK" })
        {
            var r = await adb.ShellAsync($"cmd appops set {package} {mode} allow", serial, ct);
            log?.Invoke($"[{(r.Ok ? "OK" : "WARN")}] appops {mode}");
        }

        await adb.ShellAsync($"pm grant {package} android.permission.REQUEST_IGNORE_BATTERY_OPTIMIZATIONS", serial, ct);
        log?.Invoke("[OK] REQUEST_IGNORE_BATTERY_OPTIMIZATIONS");

        await ApplyMiuiAutoStartAsync(serial, package, log, ct);
    }

    private async Task ApplyMiuiAutoStartAsync(
        string serial, string package, Action<string>? log, CancellationToken ct)
    {
        foreach (var key in MiuiPkgWhitelists)
            await AppendWhitelistAsync(serial, key, [package], log, ct);

        var boot = await adb.ShellAsync(
            $"pm grant {package} android.permission.RECEIVE_BOOT_COMPLETED", serial, ct);
        log?.Invoke(boot.Ok || boot.Combined.Contains("already", StringComparison.OrdinalIgnoreCase)
            ? "[OK] RECEIVE_BOOT_COMPLETED"
            : "[WARN] RECEIVE_BOOT_COMPLETED");

        var broadcasts = new (string Label, string Cmd)[]
        {
            ("POWER_HIDE_MODE", $"am broadcast -a miui.intent.action.POWER_HIDE_MODE_APP_LIST --es package_name {package} --ez enable true"),
            ("OP_AUTO_START allow", $"am broadcast -a miui.intent.action.OP_AUTO_START --es auto_start_service_pkg {package} --ez allow true"),
            ("OP_AUTO_START enable", $"am broadcast -a miui.intent.action.OP_AUTO_START --es auto_start_service_pkg {package} --ez enable true"),
        };

        foreach (var (label, cmd) in broadcasts)
        {
            var r = await adb.ShellAsync(cmd, serial, ct);
            log?.Invoke($"[{(r.Ok ? "OK" : "WARN")}] MIUI autostart · {label}");
        }

        var bucket = await adb.ShellAsync($"am set-standby-bucket {package} active", serial, ct);
        log?.Invoke($"[{(bucket.Ok ? "OK" : "FAIL")}] standby-bucket active");

        var idleCmd = await adb.ShellAsync($"cmd deviceidle whitelist +{package}", serial, ct);
        log?.Invoke($"[{(idleCmd.Ok ? "OK" : "WARN")}] deviceidle whitelist");
    }

    public async Task GrantPermissionsAsync(
        string serial, string package, Action<string>? log = null, CancellationToken ct = default)
    {
        if (!await adb.PackageInstalledAsync(serial, package, ct))
        {
            log?.Invoke($"[SKIP] {package} — chưa cài");
            return;
        }

        log?.Invoke($"--- Cấp quyền: {package} ---");

        foreach (var perm in GrantPermissions)
        {
            var r = await adb.ShellAsync($"pm grant {package} {perm}", serial, ct);
            if (r.Ok || r.Combined.Contains("already", StringComparison.OrdinalIgnoreCase))
                log?.Invoke($"[OK] {perm.Split('.')[^1]}");
        }

        foreach (var mode in AppOpsModes)
        {
            await adb.ShellAsync($"cmd appops set {package} {mode} allow", serial, ct);
            log?.Invoke($"[OK] appops {mode}");
        }
    }

    public async Task FullOptimizeAsync(
        string serial,
        IEnumerable<AppPreset> apps,
        bool globalRelax,
        bool chinaUnlock,
        bool disableMiuiOpt,
        bool grantPerms,
        bool changeRegion = false,
        bool disableAnalytics = false,
        Action<string>? log = null,
        CancellationToken ct = default)
    {
        var rom = await DetectRomAsync(serial, ct);
        log?.Invoke($"ROM: {rom.Summary}");
        log?.Invoke($"Build: {rom.Build}");
        if (rom.IsChina)
            log?.Invoke("ROM China: áp dụng cloud_lowlatency_whitelist + MIUI power whitelists");
        log?.Invoke("");

        if (globalRelax)
        {
            await ApplyGlobalRelaxAsync(serial, log, ct);
            log?.Invoke("");
        }

        if (chinaUnlock)
        {
            await ApplyChinaUnlockAsync(serial, log, ct);
            log?.Invoke("");
        }

        if (disableMiuiOpt)
        {
            var r = await adb.SettingsPutAsync(serial, "global", "miui_optimization", "false", ct);
            log?.Invoke($"[{(r.Ok ? "OK" : "FAIL")}] Tắt MIUI optimization (cần reboot)");
            log?.Invoke("");
        }

        if (changeRegion)
        {
            await ChangeRegionToGlobalAsync(serial, log, ct);
            log?.Invoke("");
        }

        if (disableAnalytics)
        {
            await DisableMiuiAnalyticsAsync(serial, log, ct);
            log?.Invoke("");
        }

        foreach (var app in apps)
        {
            await FixAppNotificationsAsync(serial, app, log, ct);
            if (grantPerms)
                await GrantPermissionsAsync(serial, app.Package, log, ct);
            log?.Invoke("");
        }

        log?.Invoke("Hoàn tất.");
        if (disableMiuiOpt)
            log?.Invoke("Nhớ REBOOT máy để MIUI optimization có hiệu lực.");
        if (rom.IsChina)
            log?.Invoke("ROM China: nếu vẫn trễ thông báo khi tắt màn hình, bật Tự khởi động + Không tối ưu pin trong Cài đặt app.");
    }

    private async Task AppendWhitelistAsync(
        string serial, string key, IEnumerable<string> items, Action<string>? log, CancellationToken ct)
    {
        var current = await adb.SettingsGetAsync(serial, "system", key, ct);
        var set = current.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

        var added = 0;
        foreach (var item in items.Where(i => !string.IsNullOrWhiteSpace(i)))
        {
            if (set.Add(item)) added++;
        }

        if (added == 0) return;

        var merged = string.Join(",", set.Order());
        var r = await adb.SettingsPutAsync(serial, "system", key, merged, ct);
        log?.Invoke($"[{(r.Ok ? "OK" : "WARN")}] whitelist {key} (+{added})");
    }

    // ── Đổi region CN → Global ──

    public async Task ChangeRegionToGlobalAsync(string serial, Action<string>? log = null, CancellationToken ct = default)
    {
        log?.Invoke("== Đổi region CN → Global ==");

        var region = await adb.GetPropAsync(serial, "ro.miui.region", ct);
        log?.Invoke($"Region hiện tại: {region ?? "N/A"}");

        var r1 = await adb.SettingsPutAsync(serial, "system", "miui_region", "global", ct);
        log?.Invoke($"[{(r1.Ok ? "OK" : "FAIL")}] settings system miui_region = global");

        var r2 = await adb.SettingsPutAsync(serial, "global", "device_provisioned", "1", ct);
        log?.Invoke($"[{(r2.Ok ? "OK" : "FAIL")}] settings global device_provisioned = 1");

        var locale = await adb.GetPropAsync(serial, "ro.product.locale", ct);
        if (!string.IsNullOrEmpty(locale) && locale.Contains("zh"))
        {
            log?.Invoke($"Locale hiện tại: {locale} → en-US");
        }

        log?.Invoke("[INFO] Cần REBOOT để region có hiệu lực");
    }

    // ── Tắt MIUI Analytics ──

    public async Task DisableMiuiAnalyticsAsync(string serial, Action<string>? log = null, CancellationToken ct = default)
    {
        log?.Invoke("== Tắt MIUI Analytics & theo dõi ==");

        var analyticsPackages = new[]
        {
            "com.miui.analytics",
            "com.xiaomi.mipicks",
            "com.xiaomi.joyose",
            "com.miui.msa.global",
            "com.miui.systemAdSolution",
        };

        foreach (var pkg in analyticsPackages)
        {
            var installed = await adb.PackageInstalledAsync(serial, pkg, ct);
            if (!installed) continue;

            var r = await adb.ShellAsync($"pm disable-user --user 0 {pkg}", serial, ct);
            log?.Invoke($"[{(r.Ok ? "OK" : "FAIL")}] Tắt {pkg}");
        }

        var trackingSettings = new (string Ns, string Key, string Value)[]
        {
            ("global", "usage_stats_enabled", "0"),
            ("secure", "usage_stats_enabled", "0"),
            ("global", "advertising_id_opt_out", "1"),
            ("secure", "limited_ad_tracking", "1"),
            ("global", "miui_analytics_enabled", "0"),
            ("global", "miui_optimization_analytics", "0"),
            ("global", "power_usage_analytics", "0"),
            ("global", "battery_stats_analytics", "0"),
            ("system", "miui_analytics_upload", "0"),
        };

        foreach (var (ns, key, val) in trackingSettings)
        {
            var r = await adb.SettingsPutAsync(serial, ns, key, val, ct);
            log?.Invoke($"[{(r.Ok ? "OK" : "FAIL")}] {key}={val}");
        }

        log?.Invoke("[OK] Đã tắt MIUI analytics");
    }
}