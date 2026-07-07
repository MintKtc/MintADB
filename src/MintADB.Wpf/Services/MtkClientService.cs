using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;

namespace MintADB.Wpf.Services;

public sealed class MtkClientService
{
    private string? _mtkPath;
    private string? _pythonPath;
    private string? _bundledDir;

    private string? _depsDir;

    public string? ResolvedPath => _mtkPath;
    public string? PythonPath => _pythonPath;
    public string? BundledDir => _bundledDir;
    public bool IsAvailable => !string.IsNullOrEmpty(_mtkPath);
    public bool IsOfflineReady => _depsDir is not null && Directory.Exists(_depsDir);

    public MtkClientService()
    {
        _bundledDir = Path.Combine(
            AppContext.BaseDirectory, "bundled", "mtkclient");
        _depsDir = Path.Combine(
            AppContext.BaseDirectory, "bundled", "deps");
        Locate();
    }

    public void Locate()
    {
        _pythonPath = FindPython();
        _mtkPath = FindMtk();
    }

    public string GetSearchSummary()
    {
        var parts = new List<string>
        {
            $"mtk: {_mtkPath ?? "không tìm thấy"}",
            $"python: {_pythonPath ?? "không tìm thấy"}",
            $"bundled: {_bundledDir}",
        };
        return string.Join(Environment.NewLine, parts);
    }

    public void SetCustomPath(string mtkPath, string? pythonPath = null)
    {
        _mtkPath = mtkPath;
        if (pythonPath is not null)
            _pythonPath = pythonPath;
    }

    public async Task<string> InstallAsync(IProgress<string>? progress = null)
    {
        var python = _pythonPath ?? "python";
        progress?.Report($"Đang cài mtkclient qua pip (có thể mất 1-2 phút)...");

        var psi = new ProcessStartInfo
        {
            FileName = python,
            Arguments = "-m pip install mtkclient --upgrade",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        using var proc = new Process { StartInfo = psi };
        proc.Start();

        var stdout = await proc.StandardOutput.ReadToEndAsync();
        var stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        if (proc.ExitCode == 0)
        {
            Locate();
            progress?.Report("Đang cài thư viện bổ sung...");
            var depResult = await EnsureDependenciesAsync(progress);
            if (depResult != "OK")
                return $"OK (mtkclient installed, dependencies: {depResult})";
            return "OK";
        }

        return $"Lỗi (exit {proc.ExitCode}):{Environment.NewLine}{stderr}";
    }

    public async Task<string> InstallBundledAsync(IProgress<string>? progress = null)
    {
        Directory.CreateDirectory(_bundledDir!);

        CleanDirectory(_bundledDir!);

        progress?.Report("Đang tải mtkclient từ GitHub...");
        var zipUrl = "https://github.com/bkerler/mtkclient/archive/refs/heads/main.zip";

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            var zipBytes = await http.GetByteArrayAsync(zipUrl);

            var zipPath = Path.Combine(_bundledDir!, "mtkclient.zip");
            await File.WriteAllBytesAsync(zipPath, zipBytes);

            progress?.Report("Đang giải nén...");

            ZipFile.ExtractToDirectory(zipPath, _bundledDir!, true);
            File.Delete(zipPath);

            var resolved = ResolveBundledEntry();
            if (resolved is not null)
            {
                _mtkPath = resolved;
                progress?.Report("Đã giải nén xong. Đang cài thư viện cần thiết...");
                var depResult = await EnsureDependenciesAsync(progress);
                if (depResult != "OK")
                    return $"OK (entry point found, dependencies: {depResult})";
                return "OK";
            }

            Locate();
            return "OK (không tìm thấy entry point, đã quét lại)";
        }
        catch (Exception ex)
        {
            return $"Lỗi khi tải mtkclient: {ex.Message}";
        }
    }

    public async Task<string> InstallOfflineBundleAsync(IProgress<string>? progress = null)
    {
        Directory.CreateDirectory(_bundledDir!);
        Directory.CreateDirectory(_depsDir!);

        CleanDirectory(_bundledDir!);
        CleanDirectory(_depsDir!);

        progress?.Report("Đang tải mtkclient từ GitHub...");
        var zipUrl = "https://github.com/bkerler/mtkclient/archive/refs/heads/main.zip";

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            var zipBytes = await http.GetByteArrayAsync(zipUrl);

            var zipPath = Path.Combine(_bundledDir!, "mtkclient.zip");
            await File.WriteAllBytesAsync(zipPath, zipBytes);

            progress?.Report("Đang giải nén...");

            ZipFile.ExtractToDirectory(zipPath, _bundledDir!, true);
            File.Delete(zipPath);

            var resolved = ResolveBundledEntry();
            if (resolved is null)
            {
                Locate();
                return "OK (không tìm thấy entry point, đã quét lại)";
            }

            _mtkPath = resolved;

            progress?.Report("Đang tải wheels cho offline install...");

            foreach (var dir in Directory.GetDirectories(_depsDir!))
                try { Directory.Delete(dir, true); } catch { }
            foreach (var file in Directory.GetFiles(_depsDir!))
                try { File.Delete(file); } catch { }

            var reqFile = FindRequirementsFile();
            if (reqFile is not null)
            {
                progress?.Report("Đang download wheels từ requirements.txt...");
                var dlResult = await RunPipAsync(
                    $"download -r \"{reqFile}\" -d \"{_depsDir}\"", progress);
                if (dlResult != "OK")
                    return $"Wheels download lỗi: {dlResult}";

                progress?.Report("Đang cài từ wheels (offline)...");
                var installResult = await RunPipAsync(
                    $"install --no-index --find-links \"{_depsDir}\" -r \"{reqFile}\"", progress);
                if (installResult != "OK")
                    return $"Offline install lỗi: {installResult}";
            }
            else
            {
                var fallbackDeps = new[] { "pyusb", "pyserial", "cryptography", "colorama" };
                var fallbackStr = string.Join(" ", fallbackDeps);

                progress?.Report("Đang download wheels (fallback deps)...");
                var dlResult = await RunPipAsync(
                    $"download {fallbackStr} -d \"{_depsDir}\"", progress);
                if (dlResult != "OK")
                    return $"Wheels download lỗi: {dlResult}";

                progress?.Report("Đang cài từ wheels (offline)...");
                var installResult = await RunPipAsync(
                    $"install --no-index --find-links \"{_depsDir}\" {fallbackStr}", progress);
                if (installResult != "OK")
                    return $"Offline install lỗi: {installResult}";
            }

            progress?.Report("Offline bundle hoàn tất!");
            return "OK";
        }
        catch (Exception ex)
        {
            return $"Lỗi offline bundle: {ex.Message}";
        }
    }

    private static void CleanDirectory(string dir)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var d in Directory.GetDirectories(dir))
            try { Directory.Delete(d, true); } catch { }
        foreach (var f in Directory.GetFiles(dir))
            try { File.Delete(f); } catch { }
    }

    private string? ResolveBundledEntry()
    {
        if (_bundledDir is null || !Directory.Exists(_bundledDir))
            return null;

        var candidates = new[]
        {
            Path.Combine(_bundledDir, "mtkclient-main", "mtk"),
            Path.Combine(_bundledDir, "mtkclient-main", "mtk.py"),
            Path.Combine(_bundledDir, "mtkclient-main", "mtkclient", "__main__.py"),
            Path.Combine(_bundledDir, "mtk"),
            Path.Combine(_bundledDir, "mtk.py"),
        };

        foreach (var c in candidates)
        {
            if (File.Exists(c))
                return c;
        }

        var allPy = Directory.GetFiles(_bundledDir, "*.py", SearchOption.AllDirectories);
        var mainPy = allPy.FirstOrDefault(f =>
            Path.GetFileName(f).Equals("__main__.py", StringComparison.OrdinalIgnoreCase));
        if (mainPy is not null)
            return mainPy;

        return allPy.FirstOrDefault();
    }

    public async Task<string> EnsureDependenciesAsync(IProgress<string>? progress = null)
    {
        if (_pythonPath is null)
            return "Không tìm thấy Python";

        if (IsOfflineReady)
        {
            progress?.Report("Đang cài từ offline wheels...");
            var reqFile = FindRequirementsFile();
            if (reqFile is not null)
            {
                return await RunPipAsync(
                    $"install --no-index --find-links \"{_depsDir}\" -r \"{reqFile}\"", progress);
            }
            var offlineDeps = new[] { "pyusb", "pyserial", "cryptography", "colorama" };
            var offlineArgs = string.Join(" ", offlineDeps.Select(d => $"\"{d}\""));
            return await RunPipAsync(
                $"install --no-index --find-links \"{_depsDir}\" {offlineArgs}", progress);
        }

        progress?.Report("Đang kiểm tra thư viện Python cho mtkclient...");

        var requirementsPath = FindRequirementsFile();
        if (requirementsPath is not null)
        {
            return await InstallRequirementsFileAsync(requirementsPath, progress);
        }

        var coreDeps = new[] { "pyusb", "pyserial", "cryptography", "colorama" };
        var missing = new List<string>();
        foreach (var dep in coreDeps)
        {
            if (!await CheckPackageInstalledAsync(dep))
                missing.Add(dep);
        }

        if (missing.Count == 0)
        {
            progress?.Report("Tất cả thư viện đã có sẵn.");
            return "OK";
        }

        progress?.Report($"Đang cài: {string.Join(", ", missing)}...");
        var joined = string.Join(" ", missing.Select(m => $"\"{m}\""));
        var result = await RunPipAsync($"install {joined}", progress);
        return result;
    }

    private string? FindRequirementsFile()
    {
        if (_bundledDir is null || !Directory.Exists(_bundledDir))
            return null;

        var candidates = new[]
        {
            Path.Combine(_bundledDir, "mtkclient-main", "requirements.txt"),
            Path.Combine(_bundledDir, "requirements.txt"),
            Path.Combine(_bundledDir, "mtkclient-main", "requirements", "requirements.txt"),
        };

        foreach (var c in candidates)
        {
            if (File.Exists(c)) return c;
        }

        return Directory.GetFiles(_bundledDir, "requirements.txt", SearchOption.AllDirectories)
                        .FirstOrDefault();
    }

    private async Task<string> InstallRequirementsFileAsync(string reqPath, IProgress<string>? progress)
    {
        progress?.Report($"Đang cài thư viện từ {Path.GetFileName(reqPath)}...");
        return await RunPipAsync($"install -r \"{reqPath}\"", progress);
    }

    private async Task<bool> CheckPackageInstalledAsync(string packageName)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _pythonPath!,
                Arguments = $"-c \"import {packageName}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = new Process { StartInfo = psi };
            proc.Start();
            await proc.WaitForExitAsync();
            return proc.ExitCode == 0;
        }
        catch { return false; }
    }

    private async Task<string> RunPipAsync(string args, IProgress<string>? progress)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _pythonPath!,
            Arguments = $"-m pip {args}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        using var proc = new Process { StartInfo = psi };
        proc.Start();

        var stdoutTask = Task.Run(async () =>
        {
            while (!proc.StandardOutput.EndOfStream)
            {
                var line = await proc.StandardOutput.ReadLineAsync();
                if (!string.IsNullOrWhiteSpace(line))
                    progress?.Report(line);
            }
        });

        var stderrTask = proc.StandardError.ReadToEndAsync();

        await Task.WhenAll(stdoutTask, stderrTask);
        await proc.WaitForExitAsync();

        if (proc.ExitCode == 0) return "OK";
        return $"Lỗi (exit {proc.ExitCode}):{await stderrTask}";
    }

    private static string? FindPython()
    {
        foreach (var candidate in new[] { "python", "python3", "py" })
        {
            try
            {
                using var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = candidate,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                if (proc is null) continue;
                proc.WaitForExit(3000);
                if (proc.ExitCode == 0) return candidate;
            }
            catch { }
        }

        var commonPythonPaths = new[]
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Python", "python.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Python", "python.exe"),
            @"C:\Python\python.exe",
            @"C:\Python3\python.exe",
        };

        foreach (var path in commonPythonPaths)
        {
            if (File.Exists(path)) return path;
        }

        return null;
    }

    private string? FindMtk()
    {
        var bundled = ResolveBundledEntry();
        if (bundled is not null) return bundled;

        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "mtk",
                Arguments = "--help",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (proc is null) return null;
            proc.WaitForExit(3000);
            if (proc.ExitCode == 0) return "mtk";
        }
        catch { }

        var pipCandidates = new[]
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Python", "Scripts", "mtk.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Python", "Scripts", "mtk"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Python", "Scripts", "mtk.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Python", "Scripts", "mtk"),
        };

        foreach (var pip in pipCandidates)
        {
            if (!File.Exists(pip)) continue;
            try
            {
                using var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = pip,
                    Arguments = "--help",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                if (proc is null) continue;
                proc.WaitForExit(5000);
                if (proc.ExitCode == 0) return pip;
            }
            catch { }
        }

        try
        {
            var paths = Environment.GetEnvironmentVariable("PATH")?.Split(';') ?? [];
            foreach (var dir in paths)
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                foreach (var name in new[] { "mtk.exe", "mtk" })
                {
                    var candidate = Path.Combine(dir.Trim('"'), name);
                    if (!File.Exists(candidate)) continue;
                    try
                    {
                        using var proc = Process.Start(new ProcessStartInfo
                        {
                            FileName = candidate,
                            Arguments = "--help",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                        });
                        if (proc is null) continue;
                        proc.WaitForExit(5000);
                        if (proc.ExitCode == 0) return candidate;
                    }
                    catch { }
                }
            }
        }
        catch { }

        foreach (var pip in pipCandidates)
        {
            if (File.Exists(pip)) return pip;
        }

        return null;
    }

    private string BuildCommand(string args)
    {
        var mtk = _mtkPath ?? "mtk";

        if (mtk.IndexOfAny(new[] { '\\', '/' }) < 0)
            return $"\"{mtk}\" {args}";

        if (mtk.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return $"\"{mtk}\" {args}";

        if (_pythonPath is not null)
            return $"\"{_pythonPath}\" \"{mtk}\" {args}";

        return $"\"{mtk}\" {args}";
    }

    public Task<MtkResult> RunAsync(string args, CancellationToken ct = default)
        => RunProcessAsync(BuildCommand(args), ct);

    private static async Task<MtkResult> RunProcessAsync(string command, CancellationToken ct)
    {
        var parts = SplitCommand(command);
        if (parts.Count == 0)
            return new MtkResult(1, "", "Lệnh rỗng");

        var psi = new ProcessStartInfo
        {
            FileName = parts[0],
            Arguments = string.Join(" ", parts.Skip(1)),
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

        var timeout = !proc.HasExited;
        if (timeout)
        {
            try { proc.Kill(); } catch { }
        }

        return new MtkResult(proc.ExitCode, await stdoutTask, await stderrTask, timeout);
    }

    private static List<string> SplitCommand(string command)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        var inQuote = false;

        for (var i = 0; i < command.Length; i++)
        {
            var c = command[i];
            if (c == '"')
            {
                inQuote = !inQuote;
                continue;
            }

            if (c == ' ' && !inQuote)
            {
                if (current.Length > 0)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                }
                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0)
            parts.Add(current.ToString());

        return parts;
    }

    public Task<MtkResult> PrintInfoAsync(CancellationToken ct = default)
        => RunAsync("printinfo", ct);

    public Task<MtkResult> PayloadAsync(CancellationToken ct = default)
        => RunAsync("payload", ct);

    public Task<MtkResult> BypassFrpAsync(CancellationToken ct = default)
        => RunAsync("da bypass", ct);

    public Task<MtkResult> UnlockBootloaderAsync(CancellationToken ct = default)
        => RunAsync("da seccfg unlock", ct);

    public Task<MtkResult> GetFlashInfoAsync(CancellationToken ct = default)
        => RunAsync("gf", ct);

    public Task<MtkResult> ResetDeviceAsync(CancellationToken ct = default)
        => RunAsync("reset", ct);

    public Task<MtkResult> ReadPartitionAsync(string partition, string outputFile, CancellationToken ct = default)
        => RunAsync($"r {partition} \"{outputFile}\"", ct);

    public Task<MtkResult> WritePartitionAsync(string partition, string inputFile, CancellationToken ct = default)
        => RunAsync($"w {partition} \"{inputFile}\"", ct);

    public Task<MtkResult> ErasePartitionAsync(string partition, CancellationToken ct = default)
        => RunAsync($"e {partition}", ct);

    public Task<MtkResult> FormatFlashAsync(CancellationToken ct = default)
        => RunAsync("format", ct);

    public Task<MtkResult> CustomCommandAsync(string args, CancellationToken ct = default)
        => RunAsync(args, ct);

    public async Task<string> GetVersionAsync(CancellationToken ct = default)
    {
        var r = await RunAsync("--help", ct);
        if (!r.Ok) return "Không đọc được";
        var firstLine = r.Output.Trim().Split('\n', '\r').FirstOrDefault(l => l.Contains("mtk"));
        return firstLine?.Trim() ?? "mtkclient";
    }

    public async Task<string> ScanUsbDevicesAsync()
    {
        var sb = new StringBuilder();

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = "-NoProfile -Command \"Get-PnpDevice -Class USB -ErrorAction SilentlyContinue | Where-Object { $_.HardwareID -match 'VID_0E8D' } | Format-Table FriendlyName, Status, HardwareID -AutoSize | Out-String -Width 4096\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
            };

            using var proc = new Process { StartInfo = psi };
            proc.Start();
            var output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (!string.IsNullOrWhiteSpace(output))
            {
                sb.AppendLine("--- MTK USB Devices ---");
                sb.AppendLine(output.TrimEnd());

                if (output.Contains("BROM", StringComparison.OrdinalIgnoreCase) ||
                    output.Contains("Preloader", StringComparison.OrdinalIgnoreCase) ||
                    output.Contains("Download", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine("=> Thiết bị đang ở chế độ BROM/Download");
                }
                else
                {
                    sb.AppendLine("=> Thiết bị MTK được phát hiện (có thể cần chuyển sang BROM)");
                }
            }
            else
            {
                sb.AppendLine("Không tìm thấy thiết bị MTK qua USB.");
                sb.AppendLine("Yêu cầu: Tắt nguồn máy, giữ Volume +/- và cắm USB.");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Lỗi quét USB: {ex.Message}");
        }

        try
        {
            var psi2 = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = "-NoProfile -Command \"Get-PnpDevice -ErrorAction SilentlyContinue | Where-Object { $_.HardwareID -match 'VID_0E8D' -or $_.FriendlyName -match 'MTK|MediaTek|BROM|Preloader|DA' } | Format-Table FriendlyName, Status, Class -AutoSize | Out-String -Width 4096\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
            };

            using var proc2 = new Process { StartInfo = psi2 };
            proc2.Start();
            var output2 = await proc2.StandardOutput.ReadToEndAsync();
            await proc2.WaitForExitAsync();

            if (!string.IsNullOrWhiteSpace(output2))
            {
                sb.AppendLine();
                sb.AppendLine("--- Tất cả thiết bị MTK ---");
                sb.AppendLine(output2.TrimEnd());
            }
        }
        catch { }

        return sb.ToString().TrimEnd();
    }

    public async Task<EnvironmentReport> CheckEnvironmentAsync()
    {
        var report = new EnvironmentReport();

        report.PythonFound = _pythonPath is not null;
        report.PythonPath = _pythonPath ?? "Không tìm thấy";

        report.MtkFound = _mtkPath is not null;
        report.MtkPath = _mtkPath ?? "Không tìm thấy";

        if (_mtkPath is not null)
        {
            var ver = await RunAsync("--help");
            report.MtkWorks = ver.Ok;
            report.MtkVersion = ver.Ok ? ver.Output.Trim() : ver.Combined;
        }

        if (_pythonPath is not null)
        {
            report.Deps = new Dictionary<string, bool>
            {
                ["pyusb (usb)"] = await CheckPackageInstalledAsync("usb"),
                ["pyserial (serial)"] = await CheckPackageInstalledAsync("serial"),
                ["cryptography"] = await CheckPackageInstalledAsync("cryptography"),
                ["colorama"] = await CheckPackageInstalledAsync("colorama"),
            };
        }

        report.BundledDir = _bundledDir ?? "";
        report.BundledExists = _bundledDir is not null && Directory.Exists(_bundledDir);
        report.OfflineReady = IsOfflineReady;

        return report;
    }
}

public readonly record struct MtkResult(int ExitCode, string Output, string Error, bool Timeout = false)
{
    public bool Ok => ExitCode == 0 && !Timeout;
    public string Combined => (Output + Error).Trim();
}

public sealed class EnvironmentReport
{
    public bool PythonFound { get; set; }
    public string PythonPath { get; set; } = "";

    public bool MtkFound { get; set; }
    public string MtkPath { get; set; } = "";
    public bool MtkWorks { get; set; }
    public string MtkVersion { get; set; } = "";

    public Dictionary<string, bool> Deps { get; set; } = [];
    public string BundledDir { get; set; } = "";
    public bool BundledExists { get; set; }
    public bool OfflineReady { get; set; }

    public bool AllGood =>
        PythonFound && MtkFound && MtkWorks &&
        Deps.Values.All(v => v);

    public override string ToString()
    {
        var lines = new List<string>
        {
            $"Python:  {(PythonFound ? PythonPath : "KHÔNG TÌM THẤY")}",
            $"mtk:     {(MtkFound ? MtkPath : "KHÔNG TÌM THẤY")}",
            $"version: {(MtkWorks ? MtkVersion : "không chạy được")}",
            $"bundled: {(BundledExists ? BundledDir : "không có")}",
            $"offline: {(OfflineReady ? "sẵn sàng" : "không")}",
            "",
            "--- Thư viện Python ---",
        };

        foreach (var (pkg, ok) in Deps)
            lines.Add($"  {pkg}: {(ok ? "OK" : "THIẾU")}");

        return string.Join("\n", lines);
    }
}
