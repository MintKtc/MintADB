namespace MintADB.Wpf.Services;

public sealed class AdBlockService(AdbService adb, AdbToolsService tools)
{
    public static readonly (string Package, string Label)[] MiuiAdPackages =
    [
        ("com.miui.systemAdSolution", "MIUI System Ads"),
        ("com.miui.msa", "MSA CN"),
        ("com.miui.msa.global", "MSA Global"),
        ("com.miui.android.fashiongallery", "Carousel màn hình khóa"),
        ("com.xiaomi.mipicks", "GetApps / Mi Picks"),
        ("com.xiaomi.market", "GetApps market CN"),
        ("com.xiaomi.discover", "Xiaomi Discover"),
        ("com.miui.newhome", "MIUI New Home feed"),
        ("com.miui.analytics", "MIUI Analytics"),
        ("com.miui.yellowpage", "Yellow pages CN"),
        ("com.miui.personalassistant", "App vault / personal CN"),
    ];

    public static readonly (string Package, string Label)[] GoogleAdPackages =
    [
        ("com.google.android.adservices.api", "Android Ad Services"),
        ("com.google.android.partnersetup", "Google Partner Setup"),
    ];

    private static readonly (string Ns, string Key, string Value, string Label)[] MiuiAdSettings =
    [
        ("global", "ad_switch_enable", "0", "Tắt ad switch"),
        ("system", "MiuiAdSwitch", "0", "MiuiAdSwitch"),
        ("global", "miui_ad_enable", "0", "miui_ad_enable"),
        ("secure", "personalized_ad_enabled", "0", "Quảng cáo cá nhân hóa"),
        ("system", "personalized_ad_enabled", "0", "Quảng cáo cá nhân (system)"),
        ("global", "recommended_apps_enable", "0", "Gợi ý app"),
        ("secure", "upload_log_pref", "0", "Upload log quảng cáo"),
    ];

    private static readonly (string Ns, string Key, string Value, string Label)[] GoogleAdSettings =
    [
        ("secure", "ad_tracking_enabled", "0", "Ad tracking"),
        ("secure", "limit_ad_tracking", "1", "Limit ad tracking"),
        ("global", "ads_enabled", "0", "ads_enabled"),
        ("global", "ads_personalization_enabled", "0", "Cá nhân hóa quảng cáo"),
    ];

    public async Task<int> BlockMiuiAdsAsync(string serial, Action<string>? log = null, CancellationToken ct = default)
    {
        log?.Invoke("--- Chặn quảng cáo MIUI ---");
        var ok = await ApplySettingsAsync(serial, MiuiAdSettings, log, ct);
        ok += await SetPackagesEnabledAsync(serial, MiuiAdPackages, enable: false, log, ct);
        log?.Invoke($"[MIUI] Hoàn tất ({ok} thao tác OK)");
        return ok;
    }

    public async Task<int> BlockGoogleAdsAsync(string serial, Action<string>? log = null, CancellationToken ct = default)
    {
        log?.Invoke("--- Chặn quảng cáo Google ---");
        var ok = await ApplySettingsAsync(serial, GoogleAdSettings, log, ct);
        ok += await SetPackagesEnabledAsync(serial, GoogleAdPackages, enable: false, log, ct);
        log?.Invoke($"[Google] Hoàn tất ({ok} thao tác OK)");
        return ok;
    }

    public async Task<int> BlockAllAdsAsync(
        string serial, string? dnsHost, Action<string>? log = null, CancellationToken ct = default)
    {
        var ok = await BlockMiuiAdsAsync(serial, log, ct);
        ok += await BlockGoogleAdsAsync(serial, log, ct);

        if (!string.IsNullOrWhiteSpace(dnsHost))
        {
            log?.Invoke($"--- Private DNS → {dnsHost} ---");
            var dns = await tools.SetPrivateDnsAsync(serial, dnsHost, ct);
            if (dns.Ok)
            {
                log?.Invoke("[OK] Private DNS");
                ok++;
            }
            else
                log?.Invoke($"[WARN] Private DNS: {dns.Combined}");
        }

        return ok;
    }

    public async Task<int> RestoreMiuiAdsAsync(string serial, Action<string>? log = null, CancellationToken ct = default)
    {
        log?.Invoke("--- Khôi phục gói MIUI ---");
        return await SetPackagesEnabledAsync(serial, MiuiAdPackages, enable: true, log, ct);
    }

    public async Task<int> RestoreGoogleAdsAsync(string serial, Action<string>? log = null, CancellationToken ct = default)
    {
        log?.Invoke("--- Khôi phục gói Google ---");
        return await SetPackagesEnabledAsync(serial, GoogleAdPackages, enable: true, log, ct);
    }

    public async Task<string> GetStatusAsync(string serial, CancellationToken ct = default)
    {
        var lines = new List<string>();

        foreach (var (pkg, label) in MiuiAdPackages)
        {
            if (!await adb.PackageInstalledAsync(serial, pkg, ct)) continue;
            var state = await GetPackageStateAsync(serial, pkg, ct);
            lines.Add($"MIUI · {label}: {state}");
        }

        foreach (var (pkg, label) in GoogleAdPackages)
        {
            if (!await adb.PackageInstalledAsync(serial, pkg, ct)) continue;
            var state = await GetPackageStateAsync(serial, pkg, ct);
            lines.Add($"Google · {label}: {state}");
        }

        var dns = await tools.GetPrivateDnsStatusAsync(serial, ct);
        lines.Add($"DNS · {dns}");

        var adTrack = (await adb.ShellAsync("settings get secure ad_tracking_enabled", serial, ct)).Output.Trim();
        if (adTrack is not "null" and not "")
            lines.Add($"Google · ad_tracking_enabled={adTrack}");

        return lines.Count > 0 ? string.Join(Environment.NewLine, lines) : "Không đọc được trạng thái";
    }

    private async Task<int> ApplySettingsAsync(
        string serial,
        IEnumerable<(string Ns, string Key, string Value, string Label)> settings,
        Action<string>? log,
        CancellationToken ct)
    {
        var ok = 0;
        foreach (var (ns, key, val, label) in settings)
        {
            var r = await adb.SettingsPutAsync(serial, ns, key, val, ct);
            if (r.Ok)
            {
                log?.Invoke($"[OK] {label}");
                ok++;
            }
            else
                log?.Invoke($"[WARN] {label}");
        }
        return ok;
    }

    private async Task<int> SetPackagesEnabledAsync(
        string serial,
        IEnumerable<(string Package, string Label)> packages,
        bool enable,
        Action<string>? log,
        CancellationToken ct)
    {
        var ok = 0;
        foreach (var (pkg, label) in packages)
        {
            if (!await adb.PackageInstalledAsync(serial, pkg, ct))
            {
                log?.Invoke($"[SKIP] {label} — chưa cài");
                continue;
            }

            ProcessResult r;
            if (enable)
                r = await tools.EnablePackageAsync(serial, pkg, ct);
            else
                r = await tools.DisablePackageAsync(serial, pkg, ct);

            if (r.Ok || r.Combined.Contains("new state", StringComparison.OrdinalIgnoreCase)
                     || r.Combined.Contains("already", StringComparison.OrdinalIgnoreCase))
            {
                log?.Invoke($"[OK] {(enable ? "Bật" : "Tắt")} {label}");
                ok++;
            }
            else
                log?.Invoke($"[WARN] {label}: {r.Combined}");
        }
        return ok;
    }

    private async Task<string> GetPackageStateAsync(string serial, string package, CancellationToken ct)
    {
        var q = AdbService.ShellSingleQuote(package);
        var r = await adb.ShellAsync($"pm list packages -d | grep -F {q}", serial, ct);
        if (r.Combined.Contains(package, StringComparison.Ordinal))
            return "đã tắt";

        r = await adb.ShellAsync($"pm list packages -e | grep -F {q}", serial, ct);
        if (r.Combined.Contains(package, StringComparison.Ordinal))
            return "đang bật";

        return "không rõ";
    }
}