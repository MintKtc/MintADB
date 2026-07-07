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
    /// </summary>
    public static (bool Started, string Message) InstallDriverElevated()
    {
        var inf = PlatformToolsLocator.BundledDriverInf;
        if (!File.Exists(inf))
            return (false, "Không tìm thấy android_winusb.inf trong thư mục Drivers.");

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "pnputil",
                Arguments = $"/add-driver \"{inf}\" /install",
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            Process.Start(psi);
            return (true,
                "Đã mở cài driver (UAC).\nChấp nhận quyền Administrator để pnputil cài android_winusb.\n\n"
                + "Sau khi cài: rút/cắm USB, hoặc Cập nhật driver trong Device Manager → chọn thư mục Drivers\\usb_driver.");
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

    public static async Task<string> CheckMtkDriverInstalledAsync()
    {
        var output = await RunPnputilAsync("/enum-drivers", CancellationToken.None);
        var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        var sb = new StringBuilder();
        var mtkFound = false;
        var medtekFound = false;
        var libusbFound = false;

        foreach (var line in lines)
        {
            var lower = line.Trim().ToLowerInvariant();
            if (lower.Contains("0e8d"))
            {
                mtkFound = true;
                sb.AppendLine($"  · {line.Trim()}");
            }
            if (lower.Contains("mediatek"))
            {
                medtekFound = true;
                sb.AppendLine($"  · {line.Trim()}");
            }
            if (lower.Contains("libusb-win32") || lower.Contains("libusb0"))
            {
                libusbFound = true;
                if (!lower.Contains("0e8d") && !lower.Contains("mediatek"))
                    sb.AppendLine($"  · {line.Trim()}");
            }
        }

        if (!mtkFound && !medtekFound && !libusbFound)
            return "CHƯA CÀI: Không tìm thấy driver MTK hoặc libusb nào trong hệ thống.";

        var summary = mtkFound || medtekFound ? "Driver MTK (VID_0E8D)" : "";
        if (libusbFound) summary += (summary.Length > 0 ? " + " : "") + "libusb-win32";

        var result = $"ĐÃ CÀI: {summary}\n{string.Join("", lines.Where(l =>
        {
            var lower = l.Trim().ToLowerInvariant();
            return lower.Contains("0e8d") || lower.Contains("mediatek")
                || lower.Contains("libusb-win32") || lower.Contains("libusb0");
        }).Select(l => $"  · {l.Trim()}\n"))}";

        return result.TrimEnd();
    }

    public static (bool Started, string Message) InstallMtkDriverElevated()
    {
        var inf = PlatformToolsLocator.BundledDriverInf;
        if (!File.Exists(inf))
            return (false, "Không tìm thấy android_winusb.inf trong thư mục Drivers.");

        var mtkVcomInf = Path.Combine(
            AppContext.BaseDirectory, "Drivers", "mtk_vcom", "mediatek_usb_port.inf");

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "pnputil",
                Arguments = $"/add-driver \"{inf}\" /install",
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            Process.Start(psi);

            if (File.Exists(mtkVcomInf))
            {
                var psi2 = new ProcessStartInfo
                {
                    FileName = "pnputil",
                    Arguments = $"/add-driver \"{mtkVcomInf}\" /install",
                    Verb = "runas",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                };
                Process.Start(psi2);
            }

            return (true,
                "Đã mở cài driver MTK (UAC).\nChấp nhận quyền Administrator.\n\n"
                + "Sau khi cài: rút/cắm USB MTK ở chế độ BROM (tắt nguồn, giữ Vol+/- và cắm USB).\n"
                + "Kiểm tra trong Device Manager:\n"
                + "  · 'Android ADB Interface' (WinUSB) hoặc\n"
                + "  · 'MediaTek USB Port' (libusb-win32)\nlà được.");
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