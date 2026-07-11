using MintADB.Wpf.Models;

namespace MintADB.Wpf.Services;

/// <summary>
/// HyperOS / MIUI service groups — tuned for <b>Xiaomi China</b> ROM (CN packages + Baidu/partner).
/// Global packages kept so Global ROM still works. Never touches XMSF / Security / PowerKeeper.
/// </summary>
public sealed class HyperOsServicesService(AdbService adb, AdbToolsService tools)
{
    public sealed class ServicePackage
    {
        public required string Package { get; init; }
        public required string Label { get; init; }
        /// <summary>Primarily on China ROM (still scanned everywhere).</summary>
        public bool ChinaPrimary { get; init; }
    }

    public sealed class ServiceGroup
    {
        public required string Id { get; init; }
        public required string TitleKey { get; init; }
        public required string TitleFallback { get; init; }
        public required string DescKey { get; init; }
        public required string DescFallback { get; init; }
        public bool NeedsWarning { get; init; }
        public required ServicePackage[] Packages { get; init; }
    }

    public sealed class GroupStatus
    {
        public required ServiceGroup Group { get; init; }
        public int Present { get; set; }
        public int Enabled { get; set; }
        public int Disabled { get; set; }
        public List<string> DetailLines { get; } = [];

        public string StatusLine =>
            Present == 0
                ? "—"
                : $"ON {Enabled} · OFF {Disabled} · {Present} pkg";
    }

    /// <summary>China-first groups for HyperOS CN.</summary>
    public static readonly ServiceGroup[] Groups =
    [
        new()
        {
            Id = "ads",
            TitleKey = "HyperSvcAds",
            TitleFallback = "Ads & recommendations (CN)",
            DescKey = "HyperSvcAdsDesc",
            DescFallback = "MSA CN/Global, system ads, GetApps, New Home, yellow pages",
            Packages =
            [
                new() { Package = "com.miui.systemAdSolution", Label = "System Ads", ChinaPrimary = true },
                new() { Package = "com.miui.msa.global", Label = "MSA Global" },
                new() { Package = "com.miui.msa", Label = "MSA CN", ChinaPrimary = true },
                new() { Package = "com.miui.android.fashiongallery", Label = "Lock carousel", ChinaPrimary = true },
                new() { Package = "com.xiaomi.mipicks", Label = "GetApps", ChinaPrimary = true },
                new() { Package = "com.xiaomi.discover", Label = "Discover" },
                new() { Package = "com.miui.newhome", Label = "New Home feed", ChinaPrimary = true },
                new() { Package = "com.miui.yellowpage", Label = "Yellow pages", ChinaPrimary = true },
                new() { Package = "com.miui.personalassistant", Label = "App vault / personal", ChinaPrimary = true },
                new() { Package = "com.xiaomi.market", Label = "GetApps (market CN)", ChinaPrimary = true },
            ],
        },
        new()
        {
            Id = "analytics",
            TitleKey = "HyperSvcAnalytics",
            TitleFallback = "Analytics & telemetry (CN)",
            DescKey = "HyperSvcAnalyticsDesc",
            DescFallback = "Analytics, Joyose, daemon, greenguard, bugreport",
            Packages =
            [
                new() { Package = "com.miui.analytics", Label = "Analytics", ChinaPrimary = true },
                new() { Package = "com.xiaomi.joyose", Label = "Joyose", ChinaPrimary = true },
                new() { Package = "com.xiaomi.ab", Label = "AB experiments", ChinaPrimary = true },
                new() { Package = "com.miui.daemon", Label = "MIUI Daemon", ChinaPrimary = true },
                new() { Package = "com.miui.bugreport", Label = "Bug report", ChinaPrimary = true },
                new() { Package = "com.miui.miservice", Label = "Services & feedback", ChinaPrimary = true },
                new() { Package = "com.miui.greenguard", Label = "Greenguard", ChinaPrimary = true },
                new() { Package = "com.miui.guardprovider", Label = "Guard provider", ChinaPrimary = true },
                new() { Package = "com.xiaomi.mircs", Label = "Mi RCS", ChinaPrimary = true },
            ],
        },
        new()
        {
            Id = "partners",
            TitleKey = "HyperSvcPartners",
            TitleFallback = "China partners (Baidu / Alipay…)",
            DescKey = "HyperSvcPartnersDesc",
            DescFallback = "Preload Baidu / Alipay / partners on CN ROM — disable if unused",
            Packages =
            [
                new() { Package = "com.baidu.input_mi", Label = "Baidu IME", ChinaPrimary = true },
                new() { Package = "com.baidu.searchbox", Label = "Baidu App", ChinaPrimary = true },
                new() { Package = "com.eg.android.AlipayGphone", Label = "Alipay", ChinaPrimary = true },
                new() { Package = "com.taobao.taobao", Label = "Taobao (if preloaded)", ChinaPrimary = true },
                new() { Package = "com.sina.weibo", Label = "Weibo (if preloaded)", ChinaPrimary = true },
                new() { Package = "com.ss.android.ugc.aweme", Label = "Douyin (if preloaded)", ChinaPrimary = true },
                new() { Package = "com.tencent.mm", Label = "WeChat (if preloaded)", ChinaPrimary = true },
            ],
        },
        new()
        {
            Id = "hybrid",
            TitleKey = "HyperSvcHybrid",
            TitleFallback = "Quick Apps (Hybrid)",
            DescKey = "HyperSvcHybridDesc",
            DescFallback = "HyperOS CN quick-app / content catcher",
            Packages =
            [
                new() { Package = "com.miui.hybrid", Label = "Hybrid", ChinaPrimary = true },
                new() { Package = "com.miui.hybrid.accessory", Label = "Hybrid accessory", ChinaPrimary = true },
                new() { Package = "com.miui.contentcatcher", Label = "Content catcher", ChinaPrimary = true },
                new() { Package = "com.miui.systemAdSolution", Label = "Ad solution (hybrid feed)", ChinaPrimary = true },
            ],
        },
        new()
        {
            Id = "games",
            TitleKey = "HyperSvcGames",
            TitleFallback = "Game Center CN",
            DescKey = "HyperSvcGamesDesc",
            DescFallback = "Mi Game Center / mini games (not Game Turbo core)",
            Packages =
            [
                new() { Package = "com.xiaomi.gamecenter", Label = "Game Center", ChinaPrimary = true },
                new() { Package = "com.xiaomi.gamecenter.sdk.service", Label = "Game Center SDK", ChinaPrimary = true },
                new() { Package = "com.xiaomi.glgm", Label = "Games", ChinaPrimary = true },
                new() { Package = "com.xiaomi.migameservice", Label = "Mi Game service", ChinaPrimary = true },
                new() { Package = "com.minigamecenter", Label = "Mini game center", ChinaPrimary = true },
            ],
        },
        new()
        {
            Id = "media",
            TitleKey = "HyperSvcMedia",
            TitleFallback = "Media & browser CN",
            DescKey = "HyperSvcMediaDesc",
            DescFallback = "MIUI Browser, Music, Video, MiLink, Notes",
            Packages =
            [
                new() { Package = "com.android.browser", Label = "MIUI Browser", ChinaPrimary = true },
                new() { Package = "com.miui.player", Label = "Mi Music", ChinaPrimary = true },
                new() { Package = "com.miui.video", Label = "Mi Video CN", ChinaPrimary = true },
                new() { Package = "com.miui.videoplayer", Label = "Mi Video player", ChinaPrimary = true },
                new() { Package = "com.milink.service", Label = "MiLink cast", ChinaPrimary = true },
                new() { Package = "com.miui.notes", Label = "Notes", ChinaPrimary = true },
                new() { Package = "com.xiaomi.mirror", Label = "Mi Mirror", ChinaPrimary = true },
            ],
        },
        new()
        {
            Id = "assistant",
            TitleKey = "HyperSvcAssistant",
            TitleFallback = "Xiaoai / voice CN",
            DescKey = "HyperSvcAssistantDesc",
            DescFallback = "Xiaoai voice stack (CN)",
            Packages =
            [
                new() { Package = "com.miui.voiceassist", Label = "Voice assist", ChinaPrimary = true },
                new() { Package = "com.miui.voiceassistoverlay", Label = "Voice overlay", ChinaPrimary = true },
                new() { Package = "com.miui.voiceassistProxy", Label = "Voice proxy", ChinaPrimary = true },
                new() { Package = "com.miui.voicetrigger", Label = "Voice trigger", ChinaPrimary = true },
                new() { Package = "com.xiaomi.voiceassistant", Label = "Xiaoai", ChinaPrimary = true },
                new() { Package = "com.miui.voiceassist.mibrain", Label = "Mi Brain", ChinaPrimary = true },
            ],
        },
        new()
        {
            Id = "pay",
            TitleKey = "HyperSvcPay",
            TitleFallback = "Mi Pay / wallet CN",
            DescKey = "HyperSvcPayDesc",
            DescFallback = "Mi Pay / wallet — keep if you pay with NFC Mi Pay",
            NeedsWarning = true,
            Packages =
            [
                new() { Package = "com.mipay.wallet", Label = "Mi Pay wallet", ChinaPrimary = true },
                new() { Package = "com.mipay.wallet.cn", Label = "Mi Pay CN", ChinaPrimary = true },
                new() { Package = "com.tencent.soter.soterserver", Label = "Soter (pay)", ChinaPrimary = true },
                new() { Package = "org.ifaa.aidl.manager", Label = "IFAA", ChinaPrimary = true },
                new() { Package = "com.xiaomi.payment", Label = "Mi payment", ChinaPrimary = true },
            ],
        },
        new()
        {
            Id = "cloud",
            TitleKey = "HyperSvcCloud",
            TitleFallback = "Mi Cloud CN",
            DescKey = "HyperSvcCloudDesc",
            DescFallback = "Cloud / backup / sync — only if you do NOT use Mi Cloud / Find device",
            NeedsWarning = true,
            Packages =
            [
                new() { Package = "com.miui.cloudservice", Label = "Cloud service", ChinaPrimary = true },
                new() { Package = "com.miui.cloudbackup", Label = "Cloud backup", ChinaPrimary = true },
                new() { Package = "com.miui.micloudsync", Label = "Mi Cloud sync", ChinaPrimary = true },
                new() { Package = "com.xiaomi.micloud.sdk", Label = "Mi Cloud SDK", ChinaPrimary = true },
                new() { Package = "com.miui.huanji", Label = "Mi Mover", ChinaPrimary = true },
                new() { Package = "com.xiaomi.finddevice", Label = "Find device", ChinaPrimary = true },
            ],
        },
        new()
        {
            Id = "sim",
            TitleKey = "HyperSvcSim",
            TitleFallback = "SIM / carrier CN extras",
            DescKey = "HyperSvcSimDesc",
            DescFallback = "SIM activate, virtual SIM — only if unused",
            NeedsWarning = true,
            Packages =
            [
                new() { Package = "com.xiaomi.simactivate.service", Label = "SIM activate", ChinaPrimary = true },
                new() { Package = "com.miui.virtualsim", Label = "Virtual SIM", ChinaPrimary = true },
                new() { Package = "com.miui.dmregservice", Label = "DM reg", ChinaPrimary = true },
            ],
        },
        new()
        {
            Id = "translation",
            TitleKey = "HyperSvcTranslate",
            TitleFallback = "System translation CN",
            DescKey = "HyperSvcTranslateDesc",
            DescFallback = "Kingsoft / Youdao",
            Packages =
            [
                new() { Package = "com.miui.translation.kingsoft", Label = "Kingsoft", ChinaPrimary = true },
                new() { Package = "com.miui.translation.youdao", Label = "Youdao", ChinaPrimary = true },
                new() { Package = "com.miui.translationservice", Label = "Translation service", ChinaPrimary = true },
            ],
        },
    ];

    public async Task<RomInfo> DetectRomAsync(string serial, CancellationToken ct = default)
        => await new XiaomiCnOptimizer(adb).DetectRomAsync(serial, ct);

    public async Task<IReadOnlyList<GroupStatus>> ScanAllAsync(string serial, CancellationToken ct = default)
    {
        var rom = await DetectRomAsync(serial, ct);
        var list = new List<GroupStatus>();
        foreach (var g in Groups)
        {
            // On China ROM: scan all packages. On Global: still scan all (only present show).
            _ = rom;
            list.Add(await ScanGroupAsync(serial, g, ct));
        }

        return list;
    }

    public async Task<GroupStatus> ScanGroupAsync(string serial, ServiceGroup group, CancellationToken ct = default)
    {
        var st = new GroupStatus { Group = group };
        foreach (var p in group.Packages)
        {
            var installed = await adb.PackageInstalledAsync(serial, p.Package, ct);
            if (!installed)
            {
                st.DetailLines.Add($"{p.Label}: —");
                continue;
            }

            st.Present++;
            var enabled = await IsEnabledAsync(serial, p.Package, ct);
            if (enabled)
            {
                st.Enabled++;
                st.DetailLines.Add($"{p.Label}: ON");
            }
            else
            {
                st.Disabled++;
                st.DetailLines.Add($"{p.Label}: OFF");
            }
        }

        return st;
    }

    private async Task<bool> IsEnabledAsync(string serial, string package, CancellationToken ct)
    {
        var r = await adb.ShellAsync($"pm list packages -d | grep -F {package}", serial, ct);
        if (r.Output.Contains(package, StringComparison.Ordinal))
            return false;
        return await adb.PackageInstalledAsync(serial, package, ct);
    }

    public async Task<(int Ok, int Fail, int Skip)> SetGroupEnabledAsync(
        string serial,
        string groupId,
        bool enable,
        Action<string>? log = null,
        CancellationToken ct = default)
    {
        var group = Groups.FirstOrDefault(g => g.Id == groupId)
                    ?? throw new ArgumentException("Unknown group: " + groupId);

        log?.Invoke(enable
            ? $"--- Bật dịch vụ CN · {group.TitleFallback} ---"
            : $"--- Tắt dịch vụ CN · {group.TitleFallback} ---");

        var ok = 0;
        var fail = 0;
        var skip = 0;

        foreach (var p in group.Packages)
        {
            ct.ThrowIfCancellationRequested();
            if (!await adb.PackageInstalledAsync(serial, p.Package, ct))
            {
                skip++;
                continue;
            }

            ProcessResult r = enable
                ? await tools.EnablePackageAsync(serial, p.Package, ct)
                : await tools.DisablePackageAsync(serial, p.Package, ct);

            var success = r.Ok
                          || r.Combined.Contains("already", StringComparison.OrdinalIgnoreCase)
                          || r.Combined.Contains("new state", StringComparison.OrdinalIgnoreCase);
            if (success)
            {
                log?.Invoke($"[OK] {(enable ? "enable" : "disable")} {p.Label}");
                ok++;
                continue;
            }

            if (!enable)
            {
                var rem = await tools.UninstallViaShellAsync(
                    serial, p.Package, keepData: false, allowRoot: false, fallbackDisable: true, ct);
                if (rem.Ok)
                {
                    log?.Invoke($"[OK] {rem.Outcome} {p.Label}");
                    ok++;
                    continue;
                }
            }

            log?.Invoke($"[FAIL] {p.Label}: {r.Combined}");
            fail++;
        }

        log?.Invoke($"[{group.TitleFallback}] OK={ok} FAIL={fail} SKIP={skip}");
        return (ok, fail, skip);
    }

    /// <summary>China-oriented system settings (ads, recommendations, analytics flags).</summary>
    public async Task<(int Ok, int Fail)> ApplyChinaPrivacySettingsAsync(
        string serial, Action<string>? log = null, CancellationToken ct = default)
    {
        log?.Invoke("--- HyperOS CN · privacy / ads settings ---");
        var commands = new List<(string Label, string Ns, string Key, string Value)>
        {
            ("ad_switch_enable", "global", "ad_switch_enable", "0"),
            ("MiuiAdSwitch", "system", "MiuiAdSwitch", "0"),
            ("miui_ad_enable", "global", "miui_ad_enable", "0"),
            ("personalized_ad", "secure", "personalized_ad_enabled", "0"),
            ("recommended_apps", "global", "recommended_apps_enable", "0"),
            ("upload_log", "secure", "upload_log_pref", "0"),
            ("miui_analytics", "global", "miui_analytics_enabled", "0"),
            ("opt_analytics", "global", "miui_optimization_analytics", "0"),
            ("power_analytics", "global", "power_usage_analytics", "0"),
            ("http_invoke_app", "global", "http_invoke_app", "0"),
            ("cloud_strategy", "global", "tn_disable_cloud_strategy", "1"),
        };
        return await adb.ApplySettingsBatchAsync(serial, commands, log, ct);
    }

    public async Task<(bool Ok, string Detail)> ApplyDeviceLevelFlagshipAsync(
        string serial, Action<string>? log = null, CancellationToken ct = default)
    {
        log?.Invoke("--- deviceLevelList → flagship-style (v:1,c:3,g:3) ---");
        var before = await adb.SettingsGetAsync(serial, "system", "deviceLevelList", ct);
        log?.Invoke($"before: {before}");
        var r = await adb.SettingsPutAsync(serial, "system", "deviceLevelList", "v:1,c:3,g:3", ct);
        var after = await adb.SettingsGetAsync(serial, "system", "deviceLevelList", ct);
        await adb.SettingsPutAsync(serial, "system", "miui_home_animation_rate", "0", ct);
        log?.Invoke(r.Ok ? $"[OK] after: {after}" : $"[WARN] {r.Combined}");
        return (r.Ok, after);
    }

    public async Task<string> ReadDeviceLevelAsync(string serial, CancellationToken ct = default)
    {
        var v = await adb.SettingsGetAsync(serial, "system", "deviceLevelList", ct);
        return string.IsNullOrWhiteSpace(v) ? "(default)" : v;
    }

    public Task<ProcessResult> OpenAutostartSettingsAsync(string serial, CancellationToken ct = default)
        => adb.ShellAsync(
            "am start -n com.miui.securitycenter/com.miui.permcenter.autostart.AutoStartManagementActivity",
            serial, ct);

    public Task<ProcessResult> OpenAppBatterySettingsAsync(string serial, CancellationToken ct = default)
        => adb.ShellAsync(
            "am start -n com.miui.powerkeeper/.ui.HiddenAppsConfigActivity",
            serial, ct);

    public Task<ProcessResult> OpenSecurityCenterAsync(string serial, CancellationToken ct = default)
        => adb.ShellAsync(
            "am start -n com.miui.securitycenter/.MainActivity",
            serial, ct);

    public async Task<ProcessResult> SetGameModeAsync(
        string serial, string package, string mode, CancellationToken ct = default)
    {
        if (!AdbService.IsValidPackage(package))
            return new ProcessResult(1, "", "Invalid package");
        mode = mode.ToLowerInvariant() switch
        {
            "performance" or "perf" => "performance",
            "battery" or "bat" => "battery",
            _ => "standard",
        };
        return await adb.ShellAsync($"cmd game mode {mode} {package}", serial, ct);
    }
}
