using System.IO;
using System.Text.Json;
using MintADB.Wpf.Models;

namespace MintADB.Wpf.Services;

public sealed class DeviceSpoofService(AdbService adb, ShizukuService? shizuku = null)
{
    private const string MagiskResetProp = "/data/adb/magisk/resetprop";

    private static string BackupDir => Path.Combine(AdbToolsService.MintAdbDir, "DeviceSpoof");

    public async Task<DeviceSpoofCapability> DetectCapabilitiesAsync(string serial, CancellationToken ct = default)
    {
        var hasRoot = await HasRootAccessAsync(serial, ct);
        var resetPropPath = await FindResetPropPathAsync(serial, hasRoot, ct);
        var hasResetProp = resetPropPath is not null;

        var debug = await adb.GetPropAsync(serial, "ro.debuggable", ct);
        var isDebuggable = debug is "1";

        var shizukuInstalled = false;
        var shizukuRunning = false;
        if (shizuku is not null)
        {
            var status = await shizuku.GetStatusAsync(serial, ct);
            shizukuInstalled = status.Installed;
            shizukuRunning = status.Running;
        }

        var summary = hasResetProp
            ? "Magisk resetprop — có thể fake ro.* (cần reboot)"
            : hasRoot
                ? "Root — thử setprop/resetprop"
                : isDebuggable
                    ? "Debuggable — một số prop có thể đổi"
                    : shizukuRunning
                        ? "Shizuku đang chạy — unlock FPS OK; fake ro.* vẫn cần Magisk root"
                        : "Không root — chỉ unlock FPS/Hz (fake thiết bị cần Magisk)";

        return new DeviceSpoofCapability(
            hasRoot, hasResetProp, isDebuggable, shizukuInstalled, shizukuRunning, summary);
    }

    public async Task<Dictionary<string, string>> ReadPropsAsync(
        string serial, IEnumerable<string> keys, CancellationToken ct = default)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            var val = await adb.GetPropAsync(serial, key, ct);
            if (!string.IsNullOrWhiteSpace(val) && val is not "null")
                result[key] = val;
        }
        return result;
    }

    public async Task<string> FormatCurrentDeviceAsync(string serial, CancellationToken ct = default)
    {
        var props = await ReadPropsAsync(serial, DeviceSpoofCatalog.SpoofPropKeys, ct);
        var lines = props.Select(kv => $"{kv.Key}={kv.Value}").ToList();
        return lines.Count > 0 ? string.Join(Environment.NewLine, lines) : "(không đọc được)";
    }

    public async Task<DeviceSpoofBackup?> LoadBackupAsync(string serial, CancellationToken ct = default)
    {
        var path = BackupPath(serial);
        if (!File.Exists(path)) return null;

        await using var fs = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<DeviceSpoofBackup>(fs, cancellationToken: ct);
    }

    public async Task SaveBackupAsync(string serial, Dictionary<string, string> props, CancellationToken ct = default)
    {
        Directory.CreateDirectory(BackupDir);
        var backup = new DeviceSpoofBackup
        {
            Serial = serial,
            SavedAt = DateTime.Now,
            Props = props,
        };
        await File.WriteAllTextAsync(BackupPath(serial),
            JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true }), ct);
    }

    public async Task<(int Ok, int Fail)> ApplyProfileAsync(
        string serial,
        DeviceSpoofProfile profile,
        DeviceSpoofCapability cap,
        Action<string>? log = null,
        CancellationToken ct = default)
    {
        if (!cap.CanFakeProps)
        {
            log?.Invoke("[FAIL] Fake thiết bị cần Magisk + resetprop (root). Shizuku/ADB không đổi được ro.product.*");
            return (0, profile.Props.Count);
        }

        var current = await ReadPropsAsync(serial, DeviceSpoofCatalog.SpoofPropKeys, ct);
        if (current.Count > 0)
            await SaveBackupAsync(serial, current, ct);

        var ok = 0;
        var fail = 0;
        log?.Invoke($"--- Fake thiết bị → {profile.DisplayName} ---");

        foreach (var (key, value) in profile.Props)
        {
            if (await TrySetPropAsync(serial, key, value, cap, ct))
            {
                log?.Invoke($"[OK] {key}={value}");
                ok++;
            }
            else
            {
                log?.Invoke($"[FAIL] {key} — cần cấp quyền SU trong Magisk, rồi thử lại");
                fail++;
            }
        }

        if (ok > 0)
            log?.Invoke("Reboot máy để game đọc thông tin thiết bị mới.");
        return (ok, fail);
    }

    public async Task<(int Ok, int Fail)> RestoreBackupAsync(
        string serial, DeviceSpoofCapability cap, Action<string>? log = null, CancellationToken ct = default)
    {
        var backup = await LoadBackupAsync(serial, ct);
        if (backup is null || backup.Props.Count == 0)
        {
            log?.Invoke("[FAIL] Không có backup — áp dụng profile trước hoặc backup thủ công");
            return (0, 1);
        }

        if (!cap.CanFakeProps)
        {
            log?.Invoke("[FAIL] Khôi phục cần Magisk resetprop (root)");
            return (0, backup.Props.Count);
        }

        log?.Invoke($"--- Khôi phục thiết bị ({backup.SavedAt:yyyy-MM-dd HH:mm}) ---");
        var ok = 0;
        var fail = 0;

        foreach (var (key, value) in backup.Props)
        {
            if (await TrySetPropAsync(serial, key, value, cap, ct))
            {
                log?.Invoke($"[OK] {key}");
                ok++;
            }
            else
                fail++;
        }

        if (ok > 0)
            log?.Invoke("Reboot máy để khôi phục hoàn toàn.");
        return (ok, fail);
    }

    private async Task<bool> TrySetPropAsync(
        string serial, string key, string value, DeviceSpoofCapability cap, CancellationToken ct)
    {
        foreach (var cmd in BuildPropCommands(key, value, cap))
        {
            var r = await adb.ShellAsync(cmd, serial, ct);
            if (IsPropCommandError(r))
                continue;

            var actual = await adb.GetPropAsync(serial, key, ct);
            if (actual == value)
                return true;
        }
        return false;
    }

    private async Task<bool> HasRootAccessAsync(string serial, CancellationToken ct)
    {
        foreach (var cmd in new[] { "su -c id", "su 0 id", "su -c 'id'" })
        {
            var r = await adb.ShellAsync(cmd, serial, ct);
            if (r.Ok && r.Combined.Contains("uid=0", StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private async Task<string?> FindResetPropPathAsync(string serial, bool hasRoot, CancellationToken ct)
    {
        var direct = await adb.ShellAsync("which resetprop 2>/dev/null", serial, ct);
        if (direct.Ok)
        {
            var path = direct.Output.Trim();
            if (path.Length > 0 && !path.Contains("not found", StringComparison.OrdinalIgnoreCase))
                return path;
        }

        if (hasRoot)
        {
            foreach (var cmd in new[]
                     {
                         "su -c 'which resetprop 2>/dev/null'",
                         $"su -c 'test -x {MagiskResetProp} && echo {MagiskResetProp}'",
                         "su -c 'magisk --path 2>/dev/null'",
                     })
            {
                var r = await adb.ShellAsync(cmd, serial, ct);
                var line = r.Output.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                if (line == MagiskResetProp || line.EndsWith("/resetprop", StringComparison.Ordinal))
                    return line == MagiskResetProp ? MagiskResetProp : line;

                if (line.StartsWith('/'))
                {
                    var candidate = $"{line.TrimEnd('/')}/resetprop";
                    var test = await adb.ShellAsync($"su -c 'test -x {candidate} && echo ok'", serial, ct);
                    if (test.Ok && test.Output.Contains("ok", StringComparison.Ordinal))
                        return candidate;
                }
            }
        }

        var exists = await adb.ShellAsync($"su -c 'test -x {MagiskResetProp} && echo ok'", serial, ct);
        return exists.Ok && exists.Output.Contains("ok", StringComparison.Ordinal) ? MagiskResetProp : null;
    }

    private static IEnumerable<string> BuildPropCommands(string key, string escapedValue, DeviceSpoofCapability cap)
    {
        var dq = EscapeDoubleQuoted(escapedValue);
        var sq = EscapeSingleQuoted(escapedValue);

        if (cap.HasResetProp || cap.HasRoot)
        {
            yield return $"su -c \"{MagiskResetProp} -n {key} {dq}\"";
            yield return $"su -c \"{MagiskResetProp} {key} {dq}\"";
            yield return $"su -c \"resetprop -n {key} {dq}\"";
            yield return $"su -c \"resetprop {key} {dq}\"";
        }
        if (cap.HasResetProp)
        {
            yield return $"resetprop -n {key} '{sq}'";
            yield return $"resetprop {key} '{sq}'";
        }
        if (cap.HasRoot)
            yield return $"su -c \"setprop {key} {dq}\"";

        yield return $"setprop {key} '{sq}'";
    }

    private static bool IsPropCommandError(ProcessResult r)
    {
        if (!r.Ok) return true;
        var text = r.Combined;
        if (string.IsNullOrWhiteSpace(text)) return false;
        return text.Contains("Permission denied", StringComparison.OrdinalIgnoreCase)
               || text.Contains("not found", StringComparison.OrdinalIgnoreCase)
               || text.Contains("inaccessible", StringComparison.OrdinalIgnoreCase)
               || text.Contains("failed", StringComparison.OrdinalIgnoreCase)
               || text.Contains("denied", StringComparison.OrdinalIgnoreCase);
    }

    private static string EscapeSingleQuoted(string value) => value.Replace("'", "'\\''");

    private static string EscapeDoubleQuoted(string value) =>
        "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("$", "\\$").Replace("`", "\\`") + "\"";

    private static string BackupPath(string serial) =>
        Path.Combine(BackupDir, $"backup_{SanitizeSerial(serial)}.json");

    private static string SanitizeSerial(string serial) =>
        string.Concat(serial.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}