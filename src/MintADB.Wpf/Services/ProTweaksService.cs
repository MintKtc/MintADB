namespace MintADB.Wpf.Services;

/// <summary>
/// Pro hub: audit + one-shot optimize + balanced debloat (no-root community packs).
/// Khác Optimize/Tweaks: một pipeline, điểm số, profile debloat sâu hơn.
/// </summary>
public sealed class ProTweaksService(AdbService adb, AdbToolsService tools)
{
    /// <summary>Balanced debloat for HyperOS <b>China</b> — disable-user first.</summary>
    public static readonly (string Package, string Label)[] BalancedBloat =
    [
        // Ads / feed CN
        ("com.miui.analytics", "Analytics"),
        ("com.miui.systemAdSolution", "System Ads"),
        ("com.miui.msa", "MSA CN"),
        ("com.miui.msa.global", "MSA Global"),
        ("com.xiaomi.mipicks", "GetApps"),
        ("com.xiaomi.market", "GetApps market CN"),
        ("com.xiaomi.discover", "Discover"),
        ("com.miui.newhome", "New Home"),
        ("com.miui.yellowpage", "Yellow pages"),
        ("com.miui.personalassistant", "App vault"),
        // Telemetry CN
        ("com.miui.daemon", "Daemon"),
        ("com.xiaomi.joyose", "Joyose"),
        ("com.xiaomi.ab", "AB experiments"),
        ("com.miui.bugreport", "Bug report"),
        ("com.miui.miservice", "Services"),
        ("com.miui.greenguard", "Greenguard"),
        ("com.miui.guardprovider", "Guard provider"),
        // Hybrid / content
        ("com.miui.hybrid", "Hybrid"),
        ("com.miui.hybrid.accessory", "Hybrid accessory"),
        ("com.miui.contentcatcher", "Content catcher"),
        // Partners CN
        ("com.baidu.input_mi", "Baidu IME"),
        ("com.baidu.searchbox", "Baidu App"),
        // Games / media
        ("com.xiaomi.gamecenter", "Game Center"),
        ("com.xiaomi.gamecenter.sdk.service", "Game Center SDK"),
        ("com.xiaomi.glgm", "Games"),
        ("com.android.browser", "MIUI Browser"),
        ("com.miui.player", "Mi Music"),
        ("com.miui.videoplayer", "Mi Video"),
        ("com.miui.video", "Mi Video CN"),
        ("com.milink.service", "MiLink"),
        // Translation
        ("com.miui.translation.kingsoft", "Kingsoft"),
        ("com.miui.translation.youdao", "Youdao"),
        ("com.miui.translationservice", "Translation svc"),
        // Optional
        ("com.miui.virtualsim", "Virtual SIM"),
        ("com.xiaomi.mirecycle", "Mi Recycle"),
        ("com.xiaomi.simactivate.service", "SIM activate"),
    ];

    // ── Audit ────────────────────────────────────────────────────────

    public sealed class ProAuditResult
    {
        public int Score { get; set; }
        public int MaxScore { get; set; } = 100;
        public List<string> Goods { get; } = [];
        public List<string> Issues { get; } = [];
        public List<string> Suggestions { get; } = [];

        public string SummaryText
        {
            get
            {
                var lines = new List<string>
                {
                    $"Pro score: {Score}/{MaxScore}",
                    "",
                };
                if (Goods.Count > 0)
                {
                    lines.Add("OK:");
                    lines.AddRange(Goods.Select(g => "  · " + g));
                    lines.Add("");
                }

                if (Issues.Count > 0)
                {
                    lines.Add("Issues:");
                    lines.AddRange(Issues.Select(i => "  · " + i));
                    lines.Add("");
                }

                if (Suggestions.Count > 0)
                {
                    lines.Add("Suggested:");
                    lines.AddRange(Suggestions.Select(s => "  · " + s));
                }

                return string.Join(Environment.NewLine, lines);
            }
        }
    }

    public async Task<ProAuditResult> AuditAsync(string serial, CancellationToken ct = default)
    {
        var r = new ProAuditResult();
        var score = 100;

        void Issue(int penalty, string msg, string? tip = null)
        {
            score = Math.Max(0, score - penalty);
            r.Issues.Add(msg);
            if (tip is not null) r.Suggestions.Add(tip);
        }

        void Good(string msg) => r.Goods.Add(msg);

        // Shell
        if (!(await adb.ShellAsync("echo ok", serial, ct)).Ok)
            Issue(40, "ADB shell failed", "Reconnect USB / accept debugging prompt");
        else
            Good("ADB shell OK");

        var rom = await new XiaomiCnOptimizer(adb).DetectRomAsync(serial, ct);
        var brand = (await adb.GetPropAsync(serial, "ro.product.brand", ct)).ToLowerInvariant();
        var model = await adb.GetPropAsync(serial, "ro.product.model", ct);
        Good($"Device: {brand} {model}");
        Good(rom.Summary);
        if (rom.IsChina)
            Good("Profile: Xiaomi China (CN packages)");
        else if (rom.IsXiaomi)
            Good($"Region: {rom.Region}");

        // MIUI optimization
        var miuiOpt = await adb.SettingsGetAsync(serial, "global", "miui_optimization", ct);
        if (miuiOpt is "true" or "1")
            Issue(12, "MIUI/Hyper optimization still ON", "Run Full Pro Optimize");
        else if (miuiOpt is "false" or "0")
            Good("MIUI optimization OFF");
        else
            Issue(4, "MIUI optimization unknown", "Full Pro Optimize sets miui_optimization=false");

        // Ads packages still present (CN + Global names)
        var adPkgs = new[]
        {
            "com.miui.systemAdSolution", "com.miui.msa", "com.miui.msa.global",
            "com.miui.analytics", "com.xiaomi.mipicks", "com.miui.newhome",
            "com.xiaomi.market", "com.miui.yellowpage",
        };
        var adStill = 0;
        foreach (var pkg in adPkgs)
        {
            if (await adb.PackageInstalledAsync(serial, pkg, ct))
                adStill++;
        }

        if (adStill >= 3)
            Issue(15, $"{adStill} ad/telemetry packages still installed", "Full Pro Optimize or Balanced debloat");
        else if (adStill > 0)
            Issue(8, $"{adStill} ad packages remain", "Ad block + Balanced debloat");
        else
            Good("Main ad packages removed/disabled");

        // Private DNS
        var dns = await adb.SettingsGetAsync(serial, "global", "private_dns_mode", ct);
        if (dns is "hostname")
            Good("Private DNS (ad-block style) ON");
        else
            Issue(8, "Private DNS not set to hostname", "Full Pro Optimize sets AdGuard DNS");

        // Animation
        var anim = await adb.SettingsGetAsync(serial, "global", "animator_duration_scale", ct);
        if (anim is "0" or "0.0" or "0.5")
            Good($"Animations snappy ({anim})");
        else
            Issue(5, $"Animations default/heavy ({anim})", "Battery preset or Full Pro");

        // Refresh
        var peak = await adb.SettingsGetAsync(serial, "system", "peak_refresh_rate", ct);
        if (!string.IsNullOrWhiteSpace(peak))
            Good($"peak_refresh_rate={peak}");

        // Standby / freezer
        var freezer = await adb.SettingsGetAsync(serial, "global", "cached_apps_freezer", ct);
        if (freezer is "disabled" or "0" or "false")
            Good("Cached apps freezer relaxed");
        else
            Issue(6, "App freezer / aggressive standby may kill apps", "Full Pro Optimize · Global relax");

        // Bloat count (balanced list still installed)
        var bloatLeft = 0;
        foreach (var (pkg, _) in BalancedBloat)
        {
            if (await adb.PackageInstalledAsync(serial, pkg, ct))
                bloatLeft++;
        }

        if (bloatLeft > 15)
            Issue(12, $"{bloatLeft} balanced-bloat packages still present", "Run Balanced debloat");
        else if (bloatLeft > 5)
            Issue(6, $"{bloatLeft} optional bloat packages left", "Balanced debloat if desired");
        else
            Good($"Bloat residual low ({bloatLeft})");

        r.Score = Math.Clamp(score, 0, 100);
        if (r.Score >= 85)
            r.Suggestions.Add("Machine looks solid — keep Undo backups after big debloat");
        else if (r.Score >= 60)
            r.Suggestions.Add("Run «Full Pro Optimize» then re-audit");
        else
            r.Suggestions.Add("Run Full Pro Optimize + Balanced debloat, then reboot");

        return r;
    }

    // ── Full Pro Optimize (one shot) ─────────────────────────────────

    public async Task<(int Ok, int Fail)> RunFullProOptimizeAsync(
        string serial,
        bool preferBattery,
        bool blockAds,
        bool setDns,
        bool disableMiuiOpt,
        Action<string>? log = null,
        CancellationToken ct = default)
    {
        log?.Invoke("══════════════════════════════════════");
        log?.Invoke(preferBattery
            ? "=== FULL PRO OPTIMIZE · Battery bias ==="
            : "=== FULL PRO OPTIMIZE · Smooth bias ===");
        log?.Invoke("══════════════════════════════════════");

        var ok = 0;
        var fail = 0;

        void Acc((int Ok, int Fail) t)
        {
            ok += t.Ok;
            fail += t.Fail;
        }

        var opt = new XiaomiCnOptimizer(adb);
        var rom = await opt.DetectRomAsync(serial, ct);
        log?.Invoke($"ROM: {rom.Summary}");

        // 1) Global relax + China unlock (CN only)
        log?.Invoke("[1/7] Global relax (standby / freezer)…");
        await opt.ApplyGlobalRelaxAsync(serial, log, ct);
        ok += 5;
        if (rom.IsChina)
        {
            log?.Invoke("[1b/7] China unlock (cloud strategy / autostart flags)…");
            await opt.ApplyChinaUnlockAsync(serial, log, ct);
            ok += 3;
            var hyper = new HyperOsServicesService(adb, tools);
            Acc(await hyper.ApplyChinaPrivacySettingsAsync(serial, log, ct));
        }

        // 2) MIUI optimization off
        if (disableMiuiOpt)
        {
            log?.Invoke("[2/7] Disable MIUI optimization…");
            var r = await adb.SettingsPutAsync(serial, "global", "miui_optimization", "false", ct);
            log?.Invoke(r.Ok ? "[OK] miui_optimization=false (reboot later)" : "[WARN] miui_optimization");
            if (r.Ok) ok++; else fail++;
        }

        // 3) Analytics
        log?.Invoke("[3/7] Disable MIUI analytics…");
        await opt.DisableMiuiAnalyticsAsync(serial, log, ct);
        ok += 3;

        // 4) Ads packages
        if (blockAds)
        {
            log?.Invoke("[4/7] Block ad packages (CN+Global)…");
            var ads = new AdBlockService(adb, tools);
            var n = await ads.BlockAllAdsAsync(serial, setDns ? "dns.adguard.com" : null, log, ct);
            ok += Math.Max(1, n);
        }
        else if (setDns)
        {
            log?.Invoke("[4/7] Private DNS only…");
            var dns = await tools.SetPrivateDnsAsync(serial, "dns.adguard.com", ct);
            if (dns.Ok) ok++; else fail++;
        }

        // 5) Display / power preset
        log?.Invoke(preferBattery ? "[5/7] Battery preset…" : "[5/7] Performance preset…");
        Acc(preferBattery
            ? await ApplyBatteryPresetAsync(serial, log, ct)
            : await ApplyPerformancePresetAsync(serial, log, ct));

        // 6) Touch + blur extras
        log?.Invoke("[6/7] Touch + reduce blur…");
        Acc(await ApplyTouchLatencyAsync(serial, log, ct));
        Acc(await ApplyDisplayExtrasAsync(serial, forceDark: false, reduceBlur: true, log, ct));

        // 7) China: optional device level flagship UI
        if (rom.IsChina)
        {
            log?.Invoke("[7/7] deviceLevelList flagship-style…");
            var hyper = new HyperOsServicesService(adb, tools);
            var (lvOk, detail) = await hyper.ApplyDeviceLevelFlagshipAsync(serial, log, ct);
            if (lvOk) ok++; else fail++;
            log?.Invoke($"deviceLevelList={detail}");
        }

        log?.Invoke("══════════════════════════════════════");
        log?.Invoke($"[FULL PRO] Done · {ok} OK · {fail} WARN");
        log?.Invoke("[Hint] Reboot once for MIUI optimization / Hz to settle");
        log?.Invoke("══════════════════════════════════════");
        return (ok, fail);
    }

    // ── Balanced debloat ─────────────────────────────────────────────

    public async Task<(int Ok, int Fail, int Skip)> RunBalancedDebloatAsync(
        string serial, Action<string>? log = null, CancellationToken ct = default)
    {
        log?.Invoke("--- Pro · Balanced debloat (disable-user, no root) ---");
        var ok = 0;
        var fail = 0;
        var skip = 0;

        foreach (var (pkg, label) in BalancedBloat)
        {
            ct.ThrowIfCancellationRequested();
            if (!await adb.PackageInstalledAsync(serial, pkg, ct))
            {
                skip++;
                continue;
            }

            // Prefer disable (safer / easier restore) then shell uninstall user 0
            var dis = await tools.DisablePackageAsync(serial, pkg, ct);
            if (dis.Ok || dis.Combined.Contains("already", StringComparison.OrdinalIgnoreCase))
            {
                log?.Invoke($"[OK] disable {label} ({pkg})");
                ok++;
                continue;
            }

            var rem = await tools.UninstallViaShellAsync(serial, pkg, keepData: false, allowRoot: false, fallbackDisable: true, ct);
            if (rem.Ok)
            {
                log?.Invoke($"[OK] {rem.Outcome} {label}");
                ok++;
            }
            else
            {
                log?.Invoke($"[FAIL] {label}: {rem.Detail}");
                fail++;
            }
        }

        log?.Invoke($"[Balanced] OK={ok} FAIL={fail} SKIP={skip}");
        return (ok, fail, skip);
    }

    // ── Presets (kept for advanced section) ──────────────────────────

    public async Task<(int Ok, int Fail)> ApplyPerformancePresetAsync(
        string serial, Action<string>? log = null, CancellationToken ct = default)
    {
        log?.Invoke("--- Performance preset ---");
        var commands = new List<(string Label, string Ns, string Key, string Value)>
        {
            ("peak_refresh_rate 120", "system", "peak_refresh_rate", "120"),
            ("min_refresh_rate 90", "system", "min_refresh_rate", "90"),
            ("user_refresh_rate 120", "system", "user_refresh_rate", "120"),
            ("adaptive_refresh off", "secure", "adaptive_refresh_rate", "0"),
            ("disable_window_blurs", "global", "disable_window_blurs", "1"),
            ("reduce_transparency", "global", "accessibility_reduce_transparency", "1"),
            ("intelligent_sleep off", "system", "intelligent_sleep_mode", "0"),
            ("adaptive_sleep off", "secure", "adaptive_sleep", "0"),
            ("adaptive_battery off", "secure", "adaptive_battery_management_enabled", "0"),
            ("app_standby off", "global", "app_standby_enabled", "0"),
            ("cached_freezer off", "global", "cached_apps_freezer", "disabled"),
            ("force_gpu_rendering", "global", "force_gpu_rendering", "1"),
            ("animator 1x", "global", "animator_duration_scale", "1.0"),
            ("window_anim 1x", "global", "window_animation_scale", "1.0"),
            ("transition 1x", "global", "transition_animation_scale", "1.0"),
        };

        var (ok, fail) = await adb.ApplySettingsBatchAsync(serial, commands, log, ct);
        try
        {
            var fp = await adb.ShellAsync("cmd power set-fixed-performance-mode-enabled true", serial, ct);
            log?.Invoke(fp.Ok ? "[OK] fixed-performance-mode" : "[WARN] fixed-performance-mode");
            if (fp.Ok) ok++; else fail++;
        }
        catch { fail++; }

        return (ok, fail);
    }

    public async Task<(int Ok, int Fail)> ApplyBatteryPresetAsync(
        string serial, Action<string>? log = null, CancellationToken ct = default)
    {
        log?.Invoke("--- Battery preset ---");
        var commands = new List<(string Label, string Ns, string Key, string Value)>
        {
            ("peak_refresh_rate 60", "system", "peak_refresh_rate", "60"),
            ("min_refresh_rate 60", "system", "min_refresh_rate", "60"),
            ("user_refresh_rate 60", "system", "user_refresh_rate", "60"),
            ("adaptive_refresh on", "secure", "adaptive_refresh_rate", "1"),
            ("window_blurs on", "global", "disable_window_blurs", "0"),
            ("animator 0.5x", "global", "animator_duration_scale", "0.5"),
            ("window_anim 0.5x", "global", "window_animation_scale", "0.5"),
            ("transition 0.5x", "global", "transition_animation_scale", "0.5"),
            ("adaptive_battery on", "secure", "adaptive_battery_management_enabled", "1"),
            ("force_gpu off", "global", "force_gpu_rendering", "0"),
            ("fixed_perf off via settings", "global", "low_power", "0"),
        };

        var (ok, fail) = await adb.ApplySettingsBatchAsync(serial, commands, log, ct);
        try
        {
            var fp = await adb.ShellAsync("cmd power set-fixed-performance-mode-enabled false", serial, ct);
            log?.Invoke(fp.Ok ? "[OK] fixed-performance-mode off" : "[WARN] fixed-performance-mode");
            if (fp.Ok) ok++; else fail++;
        }
        catch { fail++; }

        return (ok, fail);
    }

    public async Task<(int Ok, int Fail)> ApplyDisplayExtrasAsync(
        string serial, bool forceDark, bool reduceBlur, Action<string>? log = null, CancellationToken ct = default)
    {
        var commands = new List<(string Label, string Ns, string Key, string Value)>();
        if (forceDark)
        {
            commands.Add(("ui_night_mode", "secure", "ui_night_mode", "2"));
            commands.Add(("dark_mode_enable", "system", "dark_mode_enable", "1"));
        }

        if (reduceBlur)
        {
            commands.Add(("disable_window_blurs", "global", "disable_window_blurs", "1"));
            commands.Add(("reduce_transparency", "global", "accessibility_reduce_transparency", "1"));
        }

        if (commands.Count == 0) return (0, 0);
        return await adb.ApplySettingsBatchAsync(serial, commands, log, ct);
    }

    public Task<(int Ok, int Fail)> ApplyTouchLatencyAsync(
        string serial, Action<string>? log = null, CancellationToken ct = default)
        => adb.ApplySettingsBatchAsync(serial,
        [
            ("long_press_timeout 250", "secure", "long_press_timeout", "250"),
            ("multi_press_timeout 250", "secure", "multi_press_timeout", "250"),
        ], log, ct);

    public async Task<(int Ok, int Fail)> ApplyUnrestrictedDataAsync(
        string serial, string package, Action<string>? log = null, CancellationToken ct = default)
    {
        if (!AdbService.IsValidPackage(package))
        {
            log?.Invoke("[FAIL] Invalid package");
            return (0, 1);
        }

        log?.Invoke($"--- Unrestricted data · {package} ---");
        var ok = 0;
        var fail = 0;

        foreach (var cmd in new[]
                 {
                     $"cmd netpolicy add restrict-background-whitelist {package}",
                     $"cmd netpolicy set app-policy {package} 0",
                 })
        {
            var r = await adb.ShellAsync(cmd, serial, ct);
            if (r.Ok || r.Combined.Contains("already", StringComparison.OrdinalIgnoreCase))
            {
                log?.Invoke($"[OK] {cmd.Split(' ')[1]}");
                ok++;
            }
            else
            {
                log?.Invoke($"[WARN] {cmd}");
                fail++;
            }
        }

        foreach (var op in new[] { "RUN_IN_BACKGROUND", "RUN_ANY_IN_BACKGROUND", "IGNORE_BATTERY_OPTIMIZATIONS" })
        {
            var r = await adb.AppOpsSetAsync(serial, package, op, "allow", ct);
            if (r.Ok) ok++; else fail++;
        }

        await adb.ShellAsync($"cmd deviceidle whitelist +{package}", serial, ct);
        return (ok, fail);
    }

    public async Task ApplyNotificationProPackAsync(
        string serial, string package, Action<string>? log = null, CancellationToken ct = default)
    {
        if (!AdbService.IsValidPackage(package))
        {
            log?.Invoke("[FAIL] Invalid package");
            return;
        }

        log?.Invoke($"--- Notification pro pack · {package} ---");
        await adb.ShellAsync($"pm enable {package}", serial, ct);
        await adb.ShellAsync($"pm unsuspend {package}", serial, ct);

        foreach (var perm in new[]
                 {
                     "android.permission.POST_NOTIFICATIONS",
                     "android.permission.RECEIVE_BOOT_COMPLETED",
                     "android.permission.REQUEST_IGNORE_BATTERY_OPTIMIZATIONS",
                 })
        {
            var r = await adb.PmGrantAsync(serial, package, perm, ct);
            log?.Invoke($"[{(r.Ok || r.Combined.Contains("already", StringComparison.OrdinalIgnoreCase) ? "OK" : "WARN")}] {perm.Split('.')[^1]}");
        }

        foreach (var mode in new[]
                 {
                     "POST_NOTIFICATION", "RUN_IN_BACKGROUND", "RUN_ANY_IN_BACKGROUND",
                     "IGNORE_BATTERY_OPTIMIZATIONS", "WAKE_LOCK", "START_FOREGROUND",
                 })
            await adb.AppOpsSetAsync(serial, package, mode, "allow", ct);

        await adb.ShellAsync($"cmd deviceidle whitelist +{package}", serial, ct);
        await adb.ShellAsync($"am set-standby-bucket {package} active", serial, ct);
        await ApplyUnrestrictedDataAsync(serial, package, log, ct);

        foreach (var cmd in new[]
                 {
                     $"am broadcast -a miui.intent.action.OP_AUTO_START --es auto_start_service_pkg {package} --ez allow true",
                     $"am broadcast -a miui.intent.action.POWER_HIDE_MODE_APP_LIST --es package_name {package} --ez enable true",
                 })
            await adb.ShellAsync(cmd, serial, ct);

        log?.Invoke($"[Done] {package} — also set Autostart + No restrictions on phone");
    }

    // Wireless ADB
    public Task<ProcessResult> EnableTcpipAsync(string serial, int port = 5555, CancellationToken ct = default)
        => adb.RunAsync(["tcpip", port.ToString()], serial, ct);

    public Task<ProcessResult> PairWirelessAsync(string hostPort, string code, CancellationToken ct = default)
        => adb.RunGlobalAsync(["pair", hostPort, code], ct);

    public Task<ProcessResult> ConnectWirelessAsync(string hostPort, CancellationToken ct = default)
        => adb.RunGlobalAsync(["connect", hostPort], ct);

    public Task<ProcessResult> DisconnectWirelessAsync(string? hostPort = null, CancellationToken ct = default)
        => string.IsNullOrEmpty(hostPort)
            ? adb.RunGlobalAsync(["disconnect"], ct)
            : adb.RunGlobalAsync(["disconnect", hostPort], ct);

    public Task<string> GetWlanIpAsync(string serial, CancellationToken ct = default)
        => adb.GetWlanIpAsync(serial, ct);

    public Task<ProcessResult> OpenDeveloperOptionsAsync(string serial, CancellationToken ct = default)
        => adb.ShellAsync("am start -a android.settings.APPLICATION_DEVELOPMENT_SETTINGS", serial, ct);
}
