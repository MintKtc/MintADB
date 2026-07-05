using System.Diagnostics;
using System.IO;
using System.Text;
using MintADB.Wpf.Models;

namespace MintADB.Wpf.Services;

public sealed class FastbootService
{
    public string FastbootPath { get; }

    public FastbootService(string? adbPath = null) => FastbootPath = PlatformToolsLocator.ResolveFastbootPath(adbPath);

    public async Task<IReadOnlyList<FastbootDevice>> ListDevicesAsync(CancellationToken ct = default)
    {
        var result = await RunAsync(["devices"], ct: ct);
        var devices = new List<FastbootDevice>();

        foreach (var line in result.Combined.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("List of", StringComparison.OrdinalIgnoreCase)) continue;

            var parts = trimmed.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;
            if (!parts[1].Contains("fastboot", StringComparison.OrdinalIgnoreCase)) continue;

            devices.Add(new FastbootDevice
            {
                Serial = parts[0],
                State = parts[1],
            });
        }

        return devices;
    }

    public Task<ProcessResult> RebootAsync(string serial, FastbootMode mode, CancellationToken ct = default)
        => RunAsync(mode.Args(), serial, ct);

    public Task<ProcessResult> FlashAsync(string serial, string partition, string imagePath, CancellationToken ct = default)
        => RunAsync(["flash", partition, imagePath], serial, ct);

    public Task<ProcessResult> EraseAsync(string serial, string partition, CancellationToken ct = default)
        => RunAsync(["erase", partition], serial, ct);

    public Task<ProcessResult> GetVarAsync(string serial, string variable, CancellationToken ct = default)
        => RunAsync(["getvar", variable], serial, ct);

    public Task<ProcessResult> OemEdlAsync(string serial, CancellationToken ct = default)
        => RunAsync(["oem", "edl"], serial, ct);

    public async Task<ProcessResult> RunAsync(
        string[] args,
        string? serial = null,
        CancellationToken ct = default)
    {
        var cmd = new List<string> { FastbootPath };
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