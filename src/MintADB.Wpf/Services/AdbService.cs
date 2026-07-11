using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using MintADB.Wpf.Models;

namespace MintADB.Wpf.Services;

public sealed partial class AdbService
{
    // Android package / component names only contain these characters. Values
    // that don't match are rejected before being interpolated into an
    // `adb shell` command line, preventing shell command injection.
    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_.]*(/[A-Za-z0-9_.$]+)?$")]
    private static partial Regex PackageNameRegex();

    public static bool IsValidPackage(string? package) =>
        !string.IsNullOrEmpty(package) && PackageNameRegex().IsMatch(package);

    // Wrap an arbitrary value in single quotes for safe use inside an
    // `adb shell` command string (device shell is POSIX sh).
    public static string ShellSingleQuote(string value) =>
        "'" + value.Replace("'", "'\\''") + "'";

    private readonly HashSet<string> _settingsElevatedSerials = new(StringComparer.Ordinal);

    public string AdbPath { get; private set; } = "adb";
    public ShizukuService? Shizuku { get; set; }

    public AdbService() => ReloadPaths();

    public void ReloadPaths() => AdbPath = PlatformToolsLocator.ResolveAdbPath();

    public async Task<string?> GetVersionAsync(CancellationToken ct = default)
    {
        var r = await RunGlobalAsync(["version"], ct);
        if (!r.Ok) return null;
        var line = r.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return line?.Trim();
    }

    public async Task<IReadOnlyList<AdbDevice>> ListDevicesAsync(CancellationToken ct = default)
    {
        var result = await RunAsync(["devices", "-l"], ct: ct);
        var devices = new List<AdbDevice>();

        foreach (var line in result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("List of", StringComparison.Ordinal)) continue;

            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            var extras = new Dictionary<string, string>();
            foreach (var token in parts.Skip(2))
            {
                var idx = token.IndexOf(':');
                if (idx > 0) extras[token[..idx]] = token[(idx + 1)..];
            }

            devices.Add(new AdbDevice
            {
                Serial = parts[0],
                State = parts[1],
                Model = extras.GetValueOrDefault("model", ""),
                Product = extras.GetValueOrDefault("product", ""),
            });
        }

        return devices;
    }

    public Task<ProcessResult> ShellAsync(string command, string? serial = null, CancellationToken ct = default)
        => RunAsync(["shell", command], serial, ct);

    /// <summary>Shared shell for WLAN IPv4 (Network / quick info).</summary>
    public const string WlanIpShell =
        "ip -f inet addr show wlan0 2>/dev/null | grep inet | awk '{print $2}' | cut -d/ -f1";

    public Task<ProcessResult> KillServerAsync(CancellationToken ct = default)
        => RunGlobalAsync(["kill-server"], ct);

    public Task<ProcessResult> StartServerAsync(CancellationToken ct = default)
        => RunGlobalAsync(["start-server"], ct);

    public Task<ProcessResult> PmGrantAsync(
        string serial, string package, string permission, CancellationToken ct = default)
        => ShellAsync($"pm grant {package} {permission}", serial, ct);

    public Task<ProcessResult> PmRevokeAsync(
        string serial, string package, string permission, CancellationToken ct = default)
        => ShellAsync($"pm revoke {package} {permission}", serial, ct);

    public Task<ProcessResult> AppOpsSetAsync(
        string serial, string package, string op, string mode, CancellationToken ct = default)
        => ShellAsync($"cmd appops set {package} {op} {mode}", serial, ct);

    /// <summary>Legacy <c>appops set</c> (no <c>cmd</c>) for older ROMs.</summary>
    public Task<ProcessResult> AppOpsSetLegacyAsync(
        string serial, string package, string op, string mode, CancellationToken ct = default)
        => ShellAsync($"appops set {package} {op} {mode}", serial, ct);

    public async Task<string> GetWlanIpAsync(string serial, CancellationToken ct = default)
    {
        var r = await ShellAsync(WlanIpShell, serial, ct);
        return r.Output.Trim();
    }

    public async Task<string> GetPropAsync(string serial, string prop, CancellationToken ct = default)
    {
        var r = await ShellAsync($"getprop {prop}", serial, ct);
        return r.Output.Trim();
    }

    public Task<bool> PackageInstalledAsync(string serial, string package, CancellationToken ct = default)
        => PackageInstalledAsync(serial, package, installed: null, ct);

    public async Task<bool> PackageInstalledAsync(
        string serial, string package, HashSet<string>? installed, CancellationToken ct = default)
    {
        installed ??= await ListInstalledPackagesAsync(serial, ct);
        return installed.Contains(package);
    }

    public async Task<string> SettingsGetAsync(string serial, string ns, string key, CancellationToken ct = default)
    {
        var r = await ShellAsync($"settings get {ns} {key}", serial, ct);
        if (!r.Ok || string.IsNullOrWhiteSpace(r.Output))
            r = await ShellAsync($"cmd settings get {ns} {key}", serial, ct);
        var val = r.Output.Trim();
        return val is "null" or "" ? "" : val;
    }

    public async Task<ProcessResult> SettingsPutAsync(
        string serial, string ns, string key, string value, CancellationToken ct = default)
    {
        var escaped = value.Replace("'", "'\\''");

        // global/secure gần như luôn cần WRITE_SECURE_SETTINGS trên HyperOS — grant trước
        if (ns is "global" or "secure" or "system")
        {
            if (_settingsElevatedSerials.Add(serial))
                await TryElevateSettingsAccessAsync(serial, ct);
        }

        var r = await TrySettingsPutCoreAsync(serial, ns, key, escaped, ct);
        if (r.Ok) return r;

        // Cấp lại nếu bị chặn (SecurityException / Permission denial)
        var denied = r.Combined.Contains("WRITE_SETTINGS", StringComparison.OrdinalIgnoreCase)
                     || r.Combined.Contains("WRITE_SECURE_SETTINGS", StringComparison.OrdinalIgnoreCase)
                     || r.Combined.Contains("SecurityException", StringComparison.OrdinalIgnoreCase)
                     || r.Combined.Contains("Permission denial", StringComparison.OrdinalIgnoreCase);
        if (denied)
            await TryElevateSettingsAccessAsync(serial, ct);

        r = await TrySettingsPutCoreAsync(serial, ns, key, escaped, ct);
        if (r.Ok) return r;

        // Root
        r = await ShellAsync($"su -c \"settings put {ns} {key} '{escaped}'\"", serial, ct);
        if (r.Ok) return r;

        // content provider fallback (HyperOS đôi khi chặn settings CLI)
        return await ShellAsync(
            $"content insert --uri content://settings/{ns} --bind name:s:{key} --bind value:s:{escaped}",
            serial, ct);
    }

    private async Task<ProcessResult> TrySettingsPutCoreAsync(
        string serial, string ns, string key, string escaped, CancellationToken ct)
    {
        var r = await ShellAsync($"settings put {ns} {key} '{escaped}'", serial, ct);
        if (r.Ok) return r;
        r = await ShellAsync($"cmd settings put {ns} {key} '{escaped}'", serial, ct);
        if (r.Ok) return r;
        // package grant form used by some tools
        await ShellAsync("cmd package grant com.android.shell android.permission.WRITE_SECURE_SETTINGS", serial, ct);
        await ShellAsync("cmd package grant com.android.shell android.permission.WRITE_SETTINGS", serial, ct);
        return await ShellAsync($"settings put {ns} {key} '{escaped}'", serial, ct);
    }

    private async Task TryElevateSettingsAccessAsync(string serial, CancellationToken ct)
    {
        // system.* → WRITE_SETTINGS; secure/global → WRITE_SECURE_SETTINGS
        string[] grants =
        [
            "pm grant com.android.shell android.permission.WRITE_SECURE_SETTINGS",
            "pm grant com.android.shell android.permission.WRITE_SETTINGS",
            "cmd package grant com.android.shell android.permission.WRITE_SECURE_SETTINGS",
            "cmd package grant com.android.shell android.permission.WRITE_SETTINGS",
            "cmd appops set com.android.shell WRITE_SECURE_SETTINGS allow",
            "cmd appops set com.android.shell WRITE_SETTINGS allow",
            "appops set com.android.shell WRITE_SECURE_SETTINGS allow",
            "appops set com.android.shell WRITE_SETTINGS allow",
            // Root / Magisk
            "su -c 'pm grant com.android.shell android.permission.WRITE_SECURE_SETTINGS'",
            "su -c 'pm grant com.android.shell android.permission.WRITE_SETTINGS'",
            "su -c 'cmd appops set com.android.shell WRITE_SECURE_SETTINGS allow'",
            "su -c 'cmd appops set com.android.shell WRITE_SETTINGS allow'",
        ];

        foreach (var cmd in grants)
            await ShellAsync(cmd, serial, ct);
    }

    public async Task<(int Ok, int Fail)> ApplySettingsBatchAsync(
        string serial,
        IEnumerable<(string Label, string Ns, string Key, string Value)> commands,
        Action<string>? log = null,
        CancellationToken ct = default)
    {
        var ok = 0;
        var fail = 0;
        foreach (var (label, ns, key, value) in commands)
        {
            var r = await SettingsPutAsync(serial, ns, key, value, ct);
            if (r.Ok)
            {
                log?.Invoke($"[OK] {label}");
                ok++;
            }
            else
            {
                log?.Invoke($"[WARN] {label}");
                fail++;
            }
        }
        return (ok, fail);
    }

    public async Task<HashSet<string>> ListInstalledPackagesAsync(string serial, CancellationToken ct = default)
    {
        var r = await ShellAsync("pm list packages", serial, ct);
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in r.Combined.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("package:", StringComparison.Ordinal))
                set.Add(trimmed["package:".Length..]);
        }
        return set;
    }

    public Task<ProcessResult> RunGlobalAsync(string[] args, CancellationToken ct = default)
        => RunAsync(args, serial: null, ct);

    public async Task<byte[]?> ExecOutAsync(string[] args, string serial, CancellationToken ct = default)
    {
        var cmd = new List<string> { AdbPath, "-s", serial };
        cmd.AddRange(args);

        var psi = new ProcessStartInfo
        {
            FileName = cmd[0],
            Arguments = string.Join(" ", cmd.Skip(1).Select(EscapeArg)),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = new Process { StartInfo = psi };
        proc.Start();

        await using var stdout = proc.StandardOutput.BaseStream;
        using var ms = new MemoryStream();
        await stdout.CopyToAsync(ms, ct);
        await proc.WaitForExitAsync(ct);

        if (proc.ExitCode != 0) return null;
        var bytes = ms.ToArray();
        return bytes.Length > 0 ? bytes : null;
    }

    public async Task<ProcessResult> RunAsync(
        string[] args,
        string? serial = null,
        CancellationToken ct = default)
    {
        var cmd = new List<string> { AdbPath };
        if (!string.IsNullOrEmpty(serial))
        {
            cmd.Add("-s");
            cmd.Add(serial);
        }
        cmd.AddRange(args);

        var psi = new ProcessStartInfo
        {
            FileName = cmd[0],
            Arguments = string.Join(" ", cmd.Skip(1).Select(EscapeArg)),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        using var proc = new Process { StartInfo = psi };
        proc.Start();

        var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);

        return new ProcessResult(proc.ExitCode, await stdoutTask, await stderrTask);
    }

    private static string EscapeArg(string arg) =>
        arg.Contains(' ') || arg.Contains('"') ? $"\"{arg.Replace("\"", "\\\"")}\"" : arg;
}

public readonly record struct ProcessResult(int ExitCode, string Output, string Error)
{
    public bool Ok => ExitCode == 0;
    public string Combined => (Output + Error).Trim();
}