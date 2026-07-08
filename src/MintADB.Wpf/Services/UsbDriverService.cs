using System.Diagnostics;
using System.IO;
using System.Text;
using MintADB.Wpf.Models;

namespace MintADB.Wpf.Services;

public static class UsbDriverService
{
    private static readonly string[] AndroidDriverMarkers =
    [
        "android_winusb",
        "androidusb",
        "winusb",
        "adb interface",
        "bootloader interface",
        "composite adb",
        "google, inc.",
    ];

    private static readonly string[] AndroidDeviceMarkers =
    [
        "android",
        "adb",
        "fastboot",
        "bootloader",
        "xiaomi",
        "mi ",
        "redmi",
        "poco",
        "qualcomm",
        "mediatek",
    ];

    public static bool DriverBundlePresent => File.Exists(PlatformToolsLocator.BundledDriverInf);

    public static string DriverFolder => PlatformToolsLocator.BundledDriverDir;

    public static async Task<UsbDriverCheckResult> CheckAsync(
        AdbService adb,
        CancellationToken ct = default)
    {
        var bundlePresent = DriverBundlePresent;
        var toolsStatus = PlatformToolsLocator.GetStatus(adb.AdbPath);
        var adbVersion = await adb.GetVersionAsync(ct);
        var toolsOk = toolsStatus.AdbFound && toolsStatus.FastbootFound && adbVersion is not null;

        var driverEnum = await RunPnputilAsync("/enum-drivers", ct);
        var installedHits = FindAndroidDriverHits(driverEnum);
        var driverInstalled = installedHits.Count > 0;

        var problemEnum = await RunPnputilAsync("/enum-devices /problem", ct);
        var problemDevices = ParseProblemDevices(problemEnum);

        var summary = BuildSummary(bundlePresent, toolsOk, driverInstalled, installedHits, problemDevices, adbVersion);

        return new UsbDriverCheckResult
        {
            BundlePresent = bundlePresent,
            AndroidDriverInstalled = driverInstalled,
            InstalledDriverHits = installedHits,
            ProblemDevices = problemDevices,
            AdbToolsOk = toolsOk,
            AdbVersion = adbVersion,
            Summary = summary,
        };
    }

    private static string BuildSummary(
        bool bundlePresent,
        bool toolsOk,
        bool driverInstalled,
        IReadOnlyList<string> installedHits,
        IReadOnlyList<string> problemDevices,
        string? adbVersion)
    {
        if (!bundlePresent)
            return "Thiếu gói driver offline (Drivers\\usb_driver).";

        if (!toolsOk)
            return "ADB/Fastboot offline chưa sẵn sàng — kiểm tra thư mục PlatformTools.";

        if (problemDevices.Count > 0)
            return $"Có {problemDevices.Count} thiết bị USB lỗi driver — cần cài/cập nhật driver.";

        if (!driverInstalled)
            return "Chưa phát hiện driver Android USB trong Windows — bấm «Cài driver USB».";

        var hit = installedHits.FirstOrDefault() ?? "android_winusb";
        var ver = string.IsNullOrWhiteSpace(adbVersion) ? "" : $" · {adbVersion.Trim()}";
        return $"Driver OK ({hit}) · ADB/Fastboot sẵn sàng{ver}";
    }

    private static List<string> FindAndroidDriverHits(string pnputilOutput)
    {
        var hits = new List<string>();
        foreach (var line in pnputilOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            var lower = trimmed.ToLowerInvariant();
            if (!AndroidDriverMarkers.Any(m => lower.Contains(m, StringComparison.Ordinal)))
                continue;

            if (trimmed.Contains("Original Name:", StringComparison.OrdinalIgnoreCase))
                hits.Add(trimmed["Original Name:".Length..].Trim());
            else if (trimmed.Contains("Published Name:", StringComparison.OrdinalIgnoreCase))
                hits.Add(trimmed["Published Name:".Length..].Trim());
            else
                hits.Add(trimmed);
        }

        return hits.Distinct(StringComparer.OrdinalIgnoreCase).Take(6).ToList();
    }

    private static List<string> ParseProblemDevices(string pnputilOutput)
    {
        var results = new List<string>();
        string? current = null;

        foreach (var raw in pnputilOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.StartsWith("Instance ID:", StringComparison.OrdinalIgnoreCase))
            {
                if (current is not null)
                    TryAddProblemDevice(results, current);
                current = line["Instance ID:".Length..].Trim();
                continue;
            }

            if (current is not null
                && line.StartsWith("Device Description:", StringComparison.OrdinalIgnoreCase))
            {
                current = line["Device Description:".Length..].Trim();
            }
        }

        if (current is not null)
            TryAddProblemDevice(results, current);

        return results.Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList();
    }

    private static void TryAddProblemDevice(List<string> results, string description)
    {
        var lower = description.ToLowerInvariant();
        if (AndroidDeviceMarkers.Any(m => lower.Contains(m, StringComparison.Ordinal))
            || lower.Contains("unknown", StringComparison.Ordinal)
            || lower.Contains("mtp", StringComparison.Ordinal)
            || lower.Contains("usb", StringComparison.Ordinal))
        {
            results.Add(description);
        }
    }

    private static async Task<string> RunPnputilAsync(string args, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "pnputil",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            using var proc = Process.Start(psi);
            if (proc is null) return "";

            var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
            var stderr = await proc.StandardError.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);
            return (stdout + stderr).Trim();
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// Installs Google USB Driver (ADB + Fastboot interfaces) via pnputil — requires Administrator.
    /// Also installs Qualcomm drivers if available.
    /// </summary>
    public static async Task<(bool Started, string Message)> InstallDriverElevatedAsync(IProgress<string>? progress = null)
    {
        var drivers = new List<(string Name, string Path)>();

        foreach (var inf in PlatformToolsLocator.AllDriverInfs)
        {
            if (File.Exists(inf))
            {
                var name = Path.GetFileNameWithoutExtension(inf) switch
                {
                    "android_winusb" => "Google ADB (android_winusb)",
                    "qdloadUSB" => "Qualcomm QDLoader 9008",
                    "qcser" => "Qualcomm Serial (qcser)",
                    _ => Path.GetFileName(inf)
                };
                drivers.Add((name, inf));
            }
        }

        if (drivers.Count == 0)
            return (false, "Không tìm thấy file .inf driver nào trong thư mục Drivers.");

        var results = new List<string>();
        var allOk = true;

        try
        {
            foreach (var (name, path) in drivers)
            {
                progress?.Report($"Đang cài {name}...");
                var (ok, msg) = await RunPnputilWithAdminAsync(path);
                if (ok)
                {
                    progress?.Report($"✅ {name}: thành công");
                    results.Add($"✅ {name}");
                }
                else
                {
                    progress?.Report($"❌ {name}: {msg}");
                    results.Add($"❌ {name}: {msg}");
                    allOk = false;
                }
            }

            if (allOk)
                return (true, $"✅ Cài driver thành công!\n\n{string.Join("\n", results)}\n\n"
                    + "Rút/cắm USB để kích hoạt.");
            else
                return (true, $"⚠️ Có lỗi khi cài driver:\n\n{string.Join("\n", results)}\n\n"
                    + "Thử mở Device Manager → cập nhật driver thủ công.");
        }
        catch (Exception ex)
        {
            return (false, $"Lỗi: {ex.Message}");
        }
    }

    private static async Task<(bool Ok, string Message)> RunPnputilWithAdminAsync(string infPath)
    {
        try
        {
            // Thử add driver (nếu đã tồn tại, pnputil sẽ báo "Already exists" — đó là OK)
            var psi = new ProcessStartInfo
            {
                FileName = "pnputil",
                Arguments = $"/add-driver \"{infPath}\" /install",
                Verb = "runas",
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var proc = Process.Start(psi);
            if (proc is null)
                return (false, "Không mở được pnputil (UAC bị từ chối?).");
            await proc.WaitForExitAsync();
            var output = proc.StandardOutput.ReadToEnd().Trim();
            var error = proc.StandardError.ReadToEnd().Trim();
            var detail = !string.IsNullOrEmpty(output) ? output : error;

            // Mã 0 = success, mã 5 = Access Denied / Already exists → coi như OK nếu driver đã có
            if (proc.ExitCode == 0)
                return (true, "OK");

            if (detail.Contains("already exists", StringComparison.OrdinalIgnoreCase)
                || detail.Contains("Already exists", StringComparison.OrdinalIgnoreCase))
                return (true, "OK (đã tồn tại trong hệ thống)");

            if (detail.Contains("Access is denied", StringComparison.OrdinalIgnoreCase))
                return (true, "OK (driver đã được cài trước đó)");

            return (false, $"pnputil thoát mã {proc.ExitCode}\n{detail}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public static void OpenDriverFolder()
    {
        if (!Directory.Exists(DriverFolder))
        {
            Directory.CreateDirectory(DriverFolder);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = DriverFolder,
            UseShellExecute = true,
        });
    }

    public static void OpenDeviceManager()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "devmgmt.msc",
            UseShellExecute = true,
        });
    }

    public static async Task<string> CheckQualcommDriverInstalledAsync()
    {
        var output = await RunPnputilAsync("/enum-drivers", CancellationToken.None);
        var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        var sb = new StringBuilder();
        var qcFound = false;

        foreach (var line in lines)
        {
            var lower = line.Trim().ToLowerInvariant();
            if (lower.Contains("qdload") || lower.Contains("qualcomm") || lower.Contains("05c6"))
            {
                qcFound = true;
                sb.AppendLine($"  · {line.Trim()}");
            }
        }

        if (!qcFound)
            return "CHƯA CÀI: Không tìm thấy driver Qualcomm (QDLoader) nào trong hệ thống.";

        return $"ĐÃ CÀI: Qualcomm USB (QDLoader 9008)\n{sb}";
    }

    public static (bool Started, string Message) InstallQualcommDriverElevated()
    {
        var qcInf = PlatformToolsLocator.BundledQualcommDriverInf;

        if (!File.Exists(qcInf))
            return (false, "Không tìm thấy qdloadUSB.inf trong thư mục Drivers\\qualcomm.");

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "pnputil",
                Arguments = $"/add-driver \"{qcInf}\" /install",
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            Process.Start(psi);

            return (true,
                "Đã mở cài driver Qualcomm (UAC).\nChấp nhận quyền Administrator.\n\n"
                + "Sau khi cài: rút/cắm USB ở chế độ EDL (Volume cả 2 + cắm USB).\n"
                + "Kiểm tra trong Device Manager:\n"
                + "  · 'Qualcomm HS-USB QDLoader 9008' (Ports)\nlà được.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public static string ManualInstallHint() =>
        "Cài tay (Xiaomi / máy không nhận):\n"
        + "1. Mở Device Manager (devmgmt.msc)\n"
        + "2. Tìm thiết bị có dấu ! (Android / ADB / Fastboot)\n"
        + "3. Cập nhật driver → Browse → chọn thư mục Drivers\\usb_driver\n"
        + "4. Chọn «Android ADB Interface» hoặc «Android Bootloader Interface»";
}