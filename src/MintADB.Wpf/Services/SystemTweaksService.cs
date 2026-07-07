using System.Globalization;
using System.Text.RegularExpressions;

namespace MintADB.Wpf.Services;

public sealed class SystemTweaksService(AdbService adb)
{
    // ===== DPI =====
    public async Task<int> GetCurrentDensityAsync(string serial, CancellationToken ct = default)
    {
        var r = await adb.ShellAsync("wm density", serial, ct);
        var m = Regex.Match(r.Output, @"Physical density:\s*(\d+)");
        if (m.Success && int.TryParse(m.Groups[1].Value, out var dpi))
            return dpi;
        var m2 = Regex.Match(r.Output, @"\d+");
        return m2.Success && int.TryParse(m2.Value, out var dpi2) ? dpi2 : 0;
    }

    public async Task<string> GetDensityStatusAsync(string serial, CancellationToken ct = default)
    {
        var r = await adb.ShellAsync("wm density", serial, ct);
        var lines = r.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" · ", lines.Select(l => l.Trim()));
    }

    public Task<ProcessResult> SetDensityAsync(string serial, int dpi, CancellationToken ct = default)
        => adb.ShellAsync($"wm density {dpi}", serial, ct);

    public Task<ProcessResult> ResetDensityAsync(string serial, CancellationToken ct = default)
        => adb.ShellAsync("wm density reset", serial, ct);

    // ===== Animation Speed =====
    public async Task<string> GetAnimationStatusAsync(string serial, CancellationToken ct = default)
    {
        var keys = new[] { "window_animation_scale", "transition_animation_scale", "animator_duration_scale" };
        var parts = new List<string>();
        foreach (var key in keys)
        {
            var val = await adb.SettingsGetAsync(serial, "global", key, ct);
            parts.Add($"{key}={val}");
        }
        return string.Join(" · ", parts);
    }

    public async Task<(int Ok, int Fail)> SetAnimationSpeedAsync(
        string serial, string scale, Action<string>? log = null, CancellationToken ct = default)
    {
        var commands = new List<(string Label, string Ns, string Key, string Value)>
        {
            ($"window_animation_scale={scale}", "global", "window_animation_scale", scale),
            ($"transition_animation_scale={scale}", "global", "transition_animation_scale", scale),
            ($"animator_duration_scale={scale}", "global", "animator_duration_scale", scale),
        };
        if (scale != "0")
        {
            commands.Add(("disable_animations=0", "global", "disable_animations", "0"));
        }
        return await adb.ApplySettingsBatchAsync(serial, commands, log, ct);
    }

    public Task<ProcessResult> ToggleAnimationsAsync(string serial, bool on, CancellationToken ct = default)
        => adb.SettingsPutAsync(serial, "global", "disable_animations", on ? "0" : "1", ct);

    // ===== Navigation Mode =====
    public async Task<string> GetNavigationStatusAsync(string serial, CancellationToken ct = default)
    {
        var mode = await adb.SettingsGetAsync(serial, "secure", "navigation_mode", ct);
        var gesture = await adb.SettingsGetAsync(serial, "global", "force_fsg_nav_bar", ct);
        var immersive = await adb.SettingsGetAsync(serial, "global", "policy_control", ct);
        var hints = await adb.SettingsGetAsync(serial, "secure", "gesture_navigation_assist", ct);

        if (string.IsNullOrWhiteSpace(mode)) mode = "?";
        if (string.IsNullOrWhiteSpace(gesture)) gesture = "?";
        if (string.IsNullOrWhiteSpace(immersive)) immersive = "-";
        if (string.IsNullOrWhiteSpace(hints)) hints = "-";

        var navLabel = mode switch
        {
            "0" or "1" when gesture is "1" => "Cử chỉ (full)",
            "0" => "Cử chỉ",
            "1" => "3 nút",
            "2" => "2 nút",
            _ => $"mode={mode}",
        };
        return $"mode={navLabel} · gesture={gesture} · hints={hints}";
    }

    public async Task<ProcessResult> SetGestureNavigationAsync(string serial, CancellationToken ct = default)
    {
        await adb.SettingsPutAsync(serial, "global", "force_fsg_nav_bar", "1", ct);
        return await adb.SettingsPutAsync(serial, "secure", "navigation_mode", "0", ct);
    }

    public async Task<ProcessResult> SetThreeButtonNavigationAsync(string serial, CancellationToken ct = default)
    {
        await adb.SettingsPutAsync(serial, "global", "force_fsg_nav_bar", "0", ct);
        return await adb.SettingsPutAsync(serial, "secure", "navigation_mode", "1", ct);
    }

    public async Task<ProcessResult> SetImmersiveModeAsync(string serial, bool enable, CancellationToken ct = default)
        => await adb.SettingsPutAsync(serial, "global", "policy_control",
            enable ? "immersive.preconfirms=*" : "", ct);

    // ===== OTA Blocker =====
    public static readonly string[] OtaPackages =
    [
        "com.android.updater",
        "com.xiaomi.soar",
        "com.miui.updater",
        "com.miui.sysbase",
        "com.miui.securitycenter",
        "com.miui.securityadd",
        "com.xiaomi.finddevice",
        "com.xiaomi.micloud.sdk",
    ];

    public async Task<string> GetOtaBlockStatusAsync(string serial, CancellationToken ct = default)
    {
        var results = new List<string>();
        foreach (var pkg in OtaPackages)
        {
            var installed = await adb.PackageInstalledAsync(serial, pkg, ct);
            if (!installed) continue;
            var state = await adb.ShellAsync($"pm list packages -d | grep {pkg}", serial, ct);
            var disabled = state.Output.Contains(pkg);
            results.Add($"{pkg}={(disabled ? "đã tắt" : "đang hoạt động")}");
        }
        return results.Count > 0 ? string.Join(" · ", results) : "(không có gói OTA nào)";
    }

    public async Task<(int Ok, int Fail)> BlockOtaUpdatesAsync(
        string serial, Action<string>? log = null, CancellationToken ct = default)
    {
        log?.Invoke("--- Chặn cập nhật OTA ---");
        var ok = 0;
        var fail = 0;
        foreach (var pkg in OtaPackages)
        {
            var installed = await adb.PackageInstalledAsync(serial, pkg, ct);
            if (!installed) continue;
            var r = await adb.ShellAsync($"pm disable-user --user 0 {pkg}", serial, ct);
            if (r.Ok || r.Combined.Contains("already", StringComparison.OrdinalIgnoreCase))
            {
                log?.Invoke($"[OK] Đã tắt {pkg}");
                ok++;
            }
            else
            {
                log?.Invoke($"[WARN] {pkg}: {r.Combined}");
                fail++;
            }
        }
        var dns = await adb.SettingsPutAsync(serial, "global", "private_dns_mode", "hostname", ct);
        if (dns.Ok)
        {
            await adb.SettingsPutAsync(serial, "global", "private_dns_specifier", "dns.adguard.com", ct);
            log?.Invoke("[OK] Private DNS → dns.adguard.com (chặn OTA domain)");
            ok++;
        }
        return (ok, fail);
    }

    public async Task<(int Ok, int Fail)> UnblockOtaUpdatesAsync(
        string serial, Action<string>? log = null, CancellationToken ct = default)
    {
        log?.Invoke("--- Khôi phục OTA ---");
        var ok = 0;
        var fail = 0;
        foreach (var pkg in OtaPackages)
        {
            var installed = await adb.PackageInstalledAsync(serial, pkg, ct);
            if (!installed) continue;
            var r = await adb.ShellAsync($"pm enable {pkg}", serial, ct);
            if (r.Ok || r.Combined.Contains("already", StringComparison.OrdinalIgnoreCase))
            {
                log?.Invoke($"[OK] Đã bật {pkg}");
                ok++;
            }
            else
            {
                log?.Invoke($"[WARN] {pkg}: {r.Combined}");
                fail++;
            }
        }
        return (ok, fail);
    }

    // ===== Audio Tweaks =====
    public async Task<string> GetAudioStatusAsync(string serial, CancellationToken ct = default)
    {
        var codec = await adb.GetPropAsync(serial, "persist.bluetooth.codec", ct);
        var a2dpOffload = await adb.GetPropAsync(serial, "persist.bluetooth.a2dp_offload_cap", ct);
        var absVolume = await adb.SettingsGetAsync(serial, "global", "bluetooth_absolute_volume_enabled", ct);

        if (codec is "null" or "") codec = "mặc định";
        if (a2dpOffload is "null" or "") a2dpOffload = "?";
        if (absVolume is "null" or "") absVolume = "?";
        else absVolume = absVolume == "1" ? "bật" : "tắt";

        return $"codec={codec} · a2dp_offload={a2dpOffload} · abs_volume={absVolume}";
    }

    public static readonly Dictionary<string, string> BluetoothCodecs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SBC"] = "sbc",
        ["AAC"] = "aac",
        ["aptX"] = "aptx",
        ["aptX HD"] = "aptxhd",
        ["LDAC"] = "ldac",
        ["aptX Adaptive"] = "aptxadaptive",
        ["LC3"] = "lc3",
        ["Mặc định"] = "",
    };

    public async Task<ProcessResult> SetBluetoothCodecAsync(
        string serial, string codecValue, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(codecValue))
            return await adb.ShellAsync("setprop persist.bluetooth.codec \"\"", serial, ct);
        return await adb.ShellAsync($"setprop persist.bluetooth.codec {codecValue}", serial, ct);
    }

    public async Task<ProcessResult> ToggleAbsoluteVolumeAsync(
        string serial, bool enable, CancellationToken ct = default)
        => await adb.SettingsPutAsync(serial, "global", "bluetooth_absolute_volume_enabled",
            enable ? "1" : "0", ct);

    public async Task<ProcessResult> ToggleA2dpOffloadAsync(
        string serial, bool disable, CancellationToken ct = default)
        => await adb.ShellAsync(
            $"settings put global bluetooth_a2dp_offload_capability_supported {(disable ? "false" : "true")}",
            serial, ct);

    // ===== Google Services Fix =====
    public static readonly string[] GoogleServicesPackages =
    [
        "com.google.android.gms",
        "com.google.android.gsf",
        "com.android.vending",
        "com.google.android.gsf.login",
        "com.google.android.syncadapters.contacts",
        "com.google.android.syncadapters.calendar",
    ];

    public async Task<string> GetGoogleServicesStatusAsync(string serial, CancellationToken ct = default)
    {
        var results = new List<string>();
        foreach (var pkg in GoogleServicesPackages)
        {
            var installed = await adb.PackageInstalledAsync(serial, pkg, ct);
            if (installed)
            {
                var r = await adb.ShellAsync($"dumpsys package {pkg} | grep \"enabled=\"", serial, ct);
                var enabled = r.Output.Contains("enabled=true") || r.Output.Contains("enabled=1");
                results.Add($"{pkg.Split('.').Last()}={(enabled ? "OK" : "tắt")}");
            }
        }
        return results.Count > 0
            ? string.Join(" · ", results)
            : "(không có dịch vụ Google — cần cài Google Services trước)";
    }

    public async Task<(int Ok, int Fail)> EnableGoogleServicesAsync(
        string serial, Action<string>? log = null, CancellationToken ct = default)
    {
        log?.Invoke("--- Kiểm tra dịch vụ Google ---");
        var ok = 0;
        var fail = 0;
        foreach (var pkg in GoogleServicesPackages)
        {
            var installed = await adb.PackageInstalledAsync(serial, pkg, ct);
            if (!installed) continue;
            var r = await adb.ShellAsync($"pm enable {pkg}", serial, ct);
            if (r.Ok || r.Combined.Contains("already", StringComparison.OrdinalIgnoreCase))
            {
                log?.Invoke($"[OK] {pkg.Split('.').Last()}");
                ok++;
            }
            else
            {
                log?.Invoke($"[WARN] {pkg}: {r.Combined}");
                fail++;
            }

            await GrantGooglePermissionsAsync(serial, pkg, ct);
        }
        return (ok, fail);
    }

    public async Task GrantGooglePermissionsAsync(string serial, string pkg, CancellationToken ct = default)
    {
        var perms = new[]
        {
            "android.permission.POST_NOTIFICATIONS",
            "android.permission.ACCESS_NETWORK_STATE",
            "android.permission.RECEIVE_BOOT_COMPLETED",
        };
        foreach (var perm in perms)
        {
            await adb.ShellAsync($"pm grant {pkg} {perm}", serial, ct);
        }
    }
}
