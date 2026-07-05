using System.Globalization;
using System.IO;
using System.Text.Json;

namespace MintADB.Wpf.Services;

public sealed class DisplayPerformanceService(AdbService adb)
{
    private static string BackupDir => Path.Combine(AdbToolsService.MintAdbDir, "DisplayHz");

    private static string BackupPath(string serial) =>
        Path.Combine(BackupDir, $"backup_{SanitizeSerial(serial)}.json");

    private static string LegacyBackupPath =>
        Path.Combine(AdbToolsService.MintAdbDir, "DisplayHzBackup.json");

    public async Task<string> ReadHzStatusAsync(string serial, CancellationToken ct = default)
    {
        var keys = new (string Ns, string Key, string Label)[]
        {
            ("system", "peak_refresh_rate", "peak (system)"),
            ("system", "min_refresh_rate", "min (system)"),
            ("global", "peak_refresh_rate", "peak (global)"),
            ("secure", "refresh_rate_mode", "mode"),
            ("secure", "adaptive_refresh_rate", "adaptive"),
            ("system", "miui_refresh_rate", "miui_hz"),
        };

        var lines = new List<string>();
        foreach (var (ns, key, label) in keys)
        {
            var val = await adb.SettingsGetAsync(serial, ns, key, ct);
            if (!string.IsNullOrWhiteSpace(val))
                lines.Add($"{label}={val}");
        }

        var hw = await adb.ShellAsync("dumpsys display | grep -i refresh", serial, ct);
        if (hw.Ok)
        {
            var m = System.Text.RegularExpressions.Regex.Match(
                hw.Combined, @"(\d+(?:\.\d+)?)\s*Hz", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m.Success)
                lines.Add($"active≈{m.Groups[1].Value}Hz");
        }

        return lines.Count > 0 ? string.Join(Environment.NewLine, lines) : "(không đọc được — thử Khóa Hz)";
    }

    public async Task<(int Ok, int Fail)> ApplyLockHzAsync(
        string serial, int targetHz, bool miuiTweaks, Action<string>? log = null, CancellationToken ct = default)
    {
        await SaveBackupAsync(serial, ct);

        var hz = targetHz.ToString(CultureInfo.InvariantCulture);
        log?.Invoke($"--- Khóa Hz → {targetHz} (min = max = peak) ---");

        var commands = new List<(string Label, string Ns, string Key, string Value)>
        {
            ("peak_refresh_rate (system)", "system", "peak_refresh_rate", hz),
            ("min_refresh_rate (system)", "system", "min_refresh_rate", hz),
            ("peak_refresh_rate (global)", "global", "peak_refresh_rate", hz),
            ("min_refresh_rate (global)", "global", "min_refresh_rate", hz),
            ("peak_refresh_rate (secure)", "secure", "peak_refresh_rate", hz),
            ("min_refresh_rate (secure)", "secure", "min_refresh_rate", hz),
            ("screen_refresh_rate", "system", "screen_refresh_rate", hz),
            ("refresh_rate (secure)", "secure", "refresh_rate", hz),
            ("refresh_rate_mode high", "secure", "refresh_rate_mode", "1"),
            ("adaptive_refresh off", "secure", "adaptive_refresh_rate", "0"),
            ("adaptive_refresh off (sys)", "system", "adaptive_refresh_rate", "0"),
            ("game_driver_all_apps", "global", "game_driver_all_apps", "1"),
            ("disable_game_mode", "global", "game_mode_intervention_enabled", "0"),
        };

        if (miuiTweaks)
        {
            commands.AddRange(
            [
                ("miui_refresh_rate", "system", "miui_refresh_rate", hz),
                ("user_refresh_rate (system)", "system", "user_refresh_rate", hz),
                ("user_refresh_rate (secure)", "secure", "user_refresh_rate", hz),
                ("MIUI smart fps off", "global", "miui_smart_fps_mode", "0"),
                ("power_save_120hz off", "global", "power_save_120hz_mode", "0"),
                ("MIUI game booster off", "global", "miui_game_booster", "0"),
                ("enhanced_mode", "global", "enhanced_mode", "1"),
            ]);
        }

        return await adb.ApplySettingsBatchAsync(serial, commands, log, ct);
    }

    public async Task<(int Ok, int Fail)> ApplySmoothUiAsync(
        string serial, bool boostGpu, Action<string>? log = null, CancellationToken ct = default)
    {
        log?.Invoke("--- UI mượt & hiệu suất ---");

        var commands = new List<(string Label, string Ns, string Key, string Value)>
        {
            ("window_animation 1x", "global", "window_animation_scale", "1.0"),
            ("transition_animation 1x", "global", "transition_animation_scale", "1.0"),
            ("animator_duration 1x", "global", "animator_duration_scale", "1.0"),
            ("animations on", "global", "disable_animations", "0"),
            ("touch_response", "system", "touch_response", "1"),
            ("hide refresh overlay", "secure", "show_refresh_rate", "0"),
        };

        if (boostGpu)
        {
            commands.AddRange(
            [
                ("force_gpu_rendering", "global", "force_gpu_rendering", "1"),
                ("hwui renderer skiagl", "global", "hwui.renderer", "skiagl"),
                ("game_driver_all_apps", "global", "game_driver_all_apps", "1"),
            ]);
        }

        return await adb.ApplySettingsBatchAsync(serial, commands, log, ct);
    }

    public async Task<(int Ok, int Fail)> ApplyFullAsync(
        string serial,
        int targetHz,
        bool miuiTweaks,
        bool smoothUi,
        bool boostGpu,
        Action<string>? log = null,
        CancellationToken ct = default)
    {
        var (ok1, fail1) = await ApplyLockHzAsync(serial, targetHz, miuiTweaks, log, ct);
        if (!smoothUi)
            return (ok1, fail1);

        var (ok2, fail2) = await ApplySmoothUiAsync(serial, boostGpu, log, ct);
        log?.Invoke($"[Hz] Khóa xong — cuộn UI/game mượt hơn ở {targetHz} Hz (tốn pin hơn).");
        return (ok1 + ok2, fail1 + fail2);
    }

    public async Task<(int Ok, int Fail)> RestoreAdaptiveAsync(
        string serial, Action<string>? log = null, CancellationToken ct = default)
    {
        log?.Invoke("--- Khôi phục Hz thích ứng ---");

        var backupPath = BackupPath(serial);
        if (!File.Exists(backupPath) && File.Exists(LegacyBackupPath))
            backupPath = LegacyBackupPath;

        if (File.Exists(backupPath))
        {
            await using var fs = File.OpenRead(backupPath);
            var backup = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(fs, cancellationToken: ct);
            if (backup is { Count: > 0 })
            {
                var ok = 0;
                var fail = 0;
                foreach (var (compound, value) in backup)
                {
                    var parts = compound.Split(':', 2);
                    if (parts.Length != 2) continue;
                    var r = await adb.SettingsPutAsync(serial, parts[0], parts[1], value, ct);
                    if (r.Ok) { ok++; log?.Invoke($"[OK] {parts[1]}={value}"); }
                    else fail++;
                }
                log?.Invoke("Đã khôi phục từ backup.");
                return (ok, fail);
            }
        }

        var defaults = new List<(string Label, string Ns, string Key, string Value)>
        {
            ("adaptive on", "secure", "adaptive_refresh_rate", "1"),
            ("refresh_rate_mode auto", "secure", "refresh_rate_mode", "0"),
            ("min_refresh 60", "system", "min_refresh_rate", "60"),
            ("min_refresh 60 (global)", "global", "min_refresh_rate", "60"),
        };
        return await adb.ApplySettingsBatchAsync(serial, defaults, log, ct);
    }

    private async Task SaveBackupAsync(string serial, CancellationToken ct)
    {
        var keys = new (string Ns, string Key)[]
        {
            ("system", "peak_refresh_rate"), ("system", "min_refresh_rate"),
            ("global", "peak_refresh_rate"), ("global", "min_refresh_rate"),
            ("secure", "refresh_rate_mode"), ("secure", "adaptive_refresh_rate"),
            ("system", "miui_refresh_rate"),
        };

        var backup = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (ns, key) in keys)
        {
            var val = await adb.SettingsGetAsync(serial, ns, key, ct);
            if (!string.IsNullOrWhiteSpace(val) && val is not "null")
                backup[$"{ns}:{key}"] = val;
        }

        if (backup.Count == 0) return;
        Directory.CreateDirectory(BackupDir);
        await File.WriteAllTextAsync(
            BackupPath(serial),
            JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true }),
            ct);
    }

    private static string SanitizeSerial(string serial) =>
        string.Concat(serial.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}