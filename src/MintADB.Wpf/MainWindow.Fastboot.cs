using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MintADB.Wpf.Models;
using MintADB.Wpf.Services;

namespace MintADB.Wpf;

public partial class MainWindow
{
    private FastbootService? _fastboot;
    private string? _selectedFastbootSerial;
    private CancellationTokenSource? _flashRomCts;

    private FastbootService Fastboot => _fastboot ??= new FastbootService(_adb.AdbPath);

    private string? RequireFastbootDevice()
    {
        if (FastbootDeviceList.SelectedItem is FastbootDevice dev)
        {
            _selectedFastbootSerial = dev.Serial;
            return dev.Serial;
        }

        if (!string.IsNullOrEmpty(_selectedFastbootSerial))
            return _selectedFastbootSerial;

        MessageBox.Show(
            "Chưa chọn thiết bị Fastboot.\nCắm USB, vào bootloader rồi bấm «Quét Fastboot».",
            "MintADB", MessageBoxButton.OK, MessageBoxImage.Warning);
        return null;
    }

    private async Task RunFastbootAsync(string label, Func<Task> action)
    {
        if (RequireFastbootDevice() is null) return;
        SetActionButtonsEnabled(false);
        AppendLog($"[Fastboot · {label}] Đang chạy...");
        try { await action(); }
        catch (Exception ex) { AppendLog($"[Fastboot · {label}] Lỗi: {ex.Message}"); }
        finally { SetActionButtonsEnabled(true); }
    }

    private void ToolNavFastboot_Click(object sender, RoutedEventArgs e) => ShowToolPage(7);

    private void FastbootDeviceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FastbootDeviceList.SelectedItem is FastbootDevice dev)
        {
            _selectedFastbootSerial = dev.Serial;
            FastbootStatusText.Text = $"Đã chọn: {dev.Serial}";
        }
    }

    private async void RefreshFastbootDevices_Click(object sender, RoutedEventArgs e)
        => await RefreshFastbootDevicesAsync();

    private async Task RefreshFastbootDevicesAsync()
    {
        SetActionButtonsEnabled(false);
        try
        {
            var devices = await Fastboot.ListDevicesAsync();
            FastbootDeviceList.ItemsSource = devices;
            FastbootPathText.Text = $"fastboot · {Path.GetFileName(Fastboot.FastbootPath)}";

            if (devices.Count == 0)
            {
                _selectedFastbootSerial = null;
                FastbootStatusText.Text = "Không có thiết bị — reboot bootloader hoặc cắm USB";
                FastbootVarText.Text = "";
                return;
            }

            FastbootStatusText.Text = $"{devices.Count} thiết bị Fastboot";

            var pick = devices.FirstOrDefault(d => d.Serial == _selectedFastbootSerial) ?? devices[0];
            _selectedFastbootSerial = pick.Serial;
            FastbootDeviceList.SelectedItem = pick;
        }
        catch (Exception ex)
        {
            FastbootStatusText.Text = $"Lỗi quét: {ex.Message}";
            AppendLog($"[Fastboot] Quét lỗi: {ex.Message}");
        }
        finally
        {
            SetActionButtonsEnabled(true);
        }
    }

    private async void FastbootGetVar_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireFastbootDevice();
        if (serial is null) return;

        await RunFastbootAsync("getvar", async () =>
        {
            var keys = new[] { "product", "variant", "secure", "unlocked", "current-slot", "slot-count" };
            var lines = new List<string>();
            foreach (var key in keys)
            {
                var r = await Fastboot.GetVarAsync(serial, key);
                var text = ExtractGetVarValue(r.Combined, key) ?? r.Combined.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                    lines.Add($"{key}: {text}");
            }

            var summary = lines.Count > 0 ? string.Join("\n", lines) : "(không đọc được getvar)";
            FastbootVarText.Text = summary;
            AppendLog($"[Fastboot] getvar\n{summary}");
        });
    }

    private static string? ExtractGetVarValue(string combined, string key)
    {
        foreach (var line in combined.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith($"{key}:", StringComparison.OrdinalIgnoreCase))
                return trimmed[(key.Length + 1)..].Trim();
        }
        return null;
    }

    private async void FastbootReboot_Click(object sender, RoutedEventArgs e)
        => await FastbootRebootWithMode(FastbootMode.Reboot);

    private async void FastbootRebootBootloader_Click(object sender, RoutedEventArgs e)
        => await FastbootRebootWithMode(FastbootMode.Bootloader);

    private async void FastbootRebootRecovery_Click(object sender, RoutedEventArgs e)
        => await FastbootRebootWithMode(FastbootMode.Recovery);

    private async void FastbootRebootFastbootd_Click(object sender, RoutedEventArgs e)
        => await FastbootRebootWithMode(FastbootMode.Fastboot);

    private async void FastbootContinue_Click(object sender, RoutedEventArgs e)
        => await FastbootRebootWithMode(FastbootMode.Continue);

    private async Task FastbootRebootWithMode(FastbootMode mode)
    {
        var serial = RequireFastbootDevice();
        if (serial is null) return;

        if (mode != FastbootMode.Reboot && mode != FastbootMode.Continue)
        {
            var ok = MessageBox.Show(
                $"Fastboot {mode.Label()}?\nThiết bị sẽ khởi động lại chế độ khác.",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (ok != MessageBoxResult.Yes) return;
        }

        await RunFastbootAsync(mode.Label(), async () =>
        {
            var r = await Fastboot.RebootAsync(serial, mode);
            AppendLog(r.Ok ? $"[OK] Fastboot {mode.Label()}" : $"[FAIL] {r.Combined}");
            if (r.Ok && mode != FastbootMode.Continue)
                _ = RefreshFastbootDevicesAsync();
        });
    }

    private async void FastbootOemEdl_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireFastbootDevice();
        if (serial is null) return;

        var ok = MessageBox.Show(
            "Gửi lệnh fastboot oem edl?\nChỉ dùng cho Xiaomi/Qualcomm — máy vào chế độ EDL.",
            "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (ok != MessageBoxResult.Yes) return;

        await RunFastbootAsync("oem edl", async () =>
        {
            var r = await Fastboot.OemEdlAsync(serial);
            AppendLog(r.Ok ? "[OK] oem edl" : $"[FAIL] {r.Combined}");
        });
    }

    private string GetFastbootPartition()
    {
        var partition = FastbootPartitionCombo.Text.Trim();
        if (FastbootPartitionCombo.SelectedItem is ComboBoxItem item)
            partition = item.Content?.ToString()?.Trim() ?? partition;
        return partition;
    }

    private string? GetFastbootImagePath()
    {
        var path = GetBoxText(FastbootImageBox);
        if (string.IsNullOrWhiteSpace(path))
        {
            MessageBox.Show("Chọn file .img để flash.", "MintADB", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }
        if (!File.Exists(path))
        {
            MessageBox.Show($"Không tìm thấy file:\n{path}", "MintADB", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }
        return path;
    }

    private void BrowseFastbootImage_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Chọn file image",
            Filter = "Image (*.img;*.bin)|*.img;*.bin|Tất cả (*.*)|*.*",
        };
        if (dlg.ShowDialog() == true)
            FastbootImageBox.Text = dlg.FileName;
    }

    private async void FastbootFlash_Click(object sender, RoutedEventArgs e)
        => await FlashPartitionAsync(GetFastbootPartition());

    private async void FastbootFlashBoot_Click(object sender, RoutedEventArgs e)
        => await FlashPartitionAsync("boot");

    private async void FastbootFlashRecovery_Click(object sender, RoutedEventArgs e)
        => await FlashPartitionAsync("recovery");

    private async void FastbootFlashVendorBoot_Click(object sender, RoutedEventArgs e)
        => await FlashPartitionAsync("vendor_boot");

    private async void FastbootFlashVbmeta_Click(object sender, RoutedEventArgs e)
        => await FlashPartitionAsync("vbmeta");

    private async Task FlashPartitionAsync(string partition)
    {
        var serial = RequireFastbootDevice();
        if (serial is null) return;

        partition = partition.Trim();
        if (string.IsNullOrEmpty(partition))
        {
            MessageBox.Show("Nhập tên partition (vd: boot, recovery).", "MintADB",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var imagePath = GetFastbootImagePath();
        if (imagePath is null) return;

        var ok = MessageBox.Show(
            $"Flash partition «{partition}»?\n\nFile: {Path.GetFileName(imagePath)}\nThiết bị: {serial}\n\n"
            + "⚠️  CẢNH BÁO ⚠️\n"
            + "• Sai partition có thể brick máy (không boot được)!\n"
            + "• Chỉ flash nếu bạn chắc chắn file đúng.\n"
            + "• Nên backup partition trước khi flash.",
            "Xác nhận flash", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (ok != MessageBoxResult.Yes) return;

        await RunFastbootAsync($"flash {partition}", async () =>
        {
            AppendLog($"fastboot flash {partition} \"{imagePath}\"");
            var r = await Fastboot.FlashAsync(serial, partition, imagePath);
            AppendLog(r.Ok ? $"[OK] Flash {partition}" : $"[FAIL] {r.Combined}");
        });
    }

    private async void FastbootErase_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireFastbootDevice();
        if (serial is null) return;

        var partition = GetFastbootPartition().Trim();
        if (string.IsNullOrEmpty(partition))
        {
            MessageBox.Show("Nhập partition cần erase.", "MintADB", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var ok = MessageBox.Show(
            $"Erase partition «{partition}»?\nHành động không thể hoàn tác!",
            "Cảnh báo", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (ok != MessageBoxResult.Yes) return;

        await RunFastbootAsync($"erase {partition}", async () =>
        {
            var r = await Fastboot.EraseAsync(serial, partition);
            AppendLog(r.Ok ? $"[OK] Erase {partition}" : $"[FAIL] {r.Combined}");
        });
    }

    // ── Flash ROM (MiFlash-style) ──

    private void BrowseFastbootRom_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Chọn thư mục ROM (chọn bất kỳ file nào trong đó)",
            Filter = "Script|flash_all.bat;flash_all_except_storage.bat|All|*.*",
        };
        if (dlg.ShowDialog() == true)
        {
            var dir = Path.GetDirectoryName(dlg.FileName);
            if (dir is not null)
            {
                FastbootRomBox.Text = dir;
                ValidateFastbootRomDir(dir);
            }
        }
    }

    private void ValidateFastbootRomDir(string? dir)
    {
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
        {
            FastbootRomStatusText.Text = "Thư mục không tồn tại";
            FastbootRomInfoText.Text = "";
            return;
        }

        var hasFlashAll = File.Exists(Path.Combine(dir, "flash_all.bat"));
        var hasFlashKeep = File.Exists(Path.Combine(dir, "flash_all_except_storage.bat"));
        var hasFlashCurrent = File.Exists(Path.Combine(dir, "flash_current_slot.bat"));
        var hasFlashLock = File.Exists(Path.Combine(dir, "flash_all_lock.bat"));
        var hasImages = Directory.Exists(Path.Combine(dir, "images"));

        var scripts = new List<string>();
        if (hasFlashAll) scripts.Add("flash_all");
        if (hasFlashKeep) scripts.Add("flash_all_except_storage");
        if (hasFlashCurrent) scripts.Add("flash_current_slot");
        if (hasFlashLock) scripts.Add("flash_all_lock");

        var parts = new List<string>();
        if (scripts.Count > 0) parts.Add($"Scripts: {string.Join(", ", scripts)}");
        if (hasImages) parts.Add("images/");
        parts.Add(Path.GetFileName(dir));

        FastbootRomStatusText.Text = parts.Count > 0
            ? $"✅ {string.Join(" · ", parts)}"
            : "⚠️ Không tìm thấy script flash nào";

        // Read ROM info
        var info = ReadRomInfo(dir);
        FastbootRomInfoText.Text = info;
    }

    private string ReadRomInfo(string dir)
    {
        var lines = new List<string>();

        // Try to read firmware-info or build.prop
        var imagesDir = Path.Combine(dir, "images");
        if (Directory.Exists(imagesDir))
        {
            // Check for firmware version file
            var versionFiles = new[] { "firmware-version.txt", "version.txt", "build.prop" };
            foreach (var vf in versionFiles)
            {
                var vfPath = Path.Combine(imagesDir, vf);
                if (File.Exists(vfPath))
                {
                    try
                    {
                        var content = File.ReadAllText(vfPath);
                        var firstLines = content.Split('\n', '\r', StringSplitOptions.RemoveEmptyEntries)
                            .Take(3);
                        lines.AddRange(firstLines.Select(l => l.Trim()));
                    }
                    catch { }
                }
            }

            // Count images
            var imgCount = Directory.GetFiles(imagesDir, "*.img").Length;
            var binCount = Directory.GetFiles(imagesDir, "*.bin").Length;
            if (imgCount > 0 || binCount > 0)
                lines.Add($"{imgCount} img + {binCount} bin files");
        }

        return lines.Count > 0 ? string.Join(" | ", lines) : "";
    }

    private string? RequireFastbootRomDir()
    {
        var dir = GetBoxText(FastbootRomBox);
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
        {
            MessageBox.Show("Chọn thư mục ROM hợp lệ trước.", "MintADB",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }
        return dir;
    }

    private async void FastbootFlashRom_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireFastbootDevice();
        if (serial is null) return;
        var romDir = RequireFastbootRomDir();
        if (romDir is null) return;

        var ok = MessageBox.Show(
            $"Flash ROM (xóa sạch)?\n\nThư mục: {romDir}\nThiết bị: {serial}\n\n"
            + "⚠️  CẢNH BÁO ⚠️\n"
            + "• Tất cả dữ liệu sẽ bị xóa!\n"
            + "• Sai ROM có thể brick máy.\n"
            + "• Chỉ flash ROM chính hãng cho model này.",
            "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (ok != MessageBoxResult.Yes) return;

        await RunFastbootAsync("Flash ROM (xóa sạch)", () =>
            RunFlashRomBatchAsync(serial, romDir, "flash_all.bat"));
    }

    private async void FastbootFlashRomKeep_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireFastbootDevice();
        if (serial is null) return;
        var romDir = RequireFastbootRomDir();
        if (romDir is null) return;

        var ok = MessageBox.Show(
            $"Flash ROM (giữ dữ liệu)?\n\nThư mục: {romDir}\nThiết bị: {serial}\n\n"
            + "⚠️  CẢNH BÁO ⚠️\n"
            + "• Sai ROM có thể brick máy.\n"
            + "• Chỉ flash ROM chính hãng cho model này.\n"
            + "• Nên sao lưu dữ liệu quan trọng trước khi flash.",
            "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (ok != MessageBoxResult.Yes) return;

        await RunFastbootAsync("Flash ROM (giữ dữ liệu)", () =>
            RunFlashRomBatchAsync(serial, romDir, "flash_all_except_storage.bat"));
    }

    private async void FastbootFlashRomCurrentSlot_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireFastbootDevice();
        if (serial is null) return;
        var romDir = RequireFastbootRomDir();
        if (romDir is null) return;

        var ok = MessageBox.Show(
            $"Flash ROM (slot hiện tại)?\n\nThư mục: {romDir}\nThiết bị: {serial}\n\n"
            + "Script: flash_current_slot.bat\n"
            + "Chỉ flash vào slot đang active (A hoặc B).",
            "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (ok != MessageBoxResult.Yes) return;

        await RunFastbootAsync("Flash ROM (current slot)", () =>
            RunFlashRomBatchAsync(serial, romDir, "flash_current_slot.bat"));
    }

    private async void FastbootFlashRomLock_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireFastbootDevice();
        if (serial is null) return;
        var romDir = RequireFastbootRomDir();
        if (romDir is null) return;

        var ok = MessageBox.Show(
            $"Flash ROM + Lock Bootloader?\n\nThư mục: {romDir}\nThiết bị: {serial}\n\n"
            + "⚠️  CẢNH BÁO ⚠️\n"
            + "• Tất cả dữ liệu sẽ bị xóa!\n"
            + "• Bootloader sẽ bị KHÓA sau khi flash!\n"
            + "• Cần unlock lại nếu muốn flash sau này.",
            "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (ok != MessageBoxResult.Yes) return;

        await RunFastbootAsync("Flash ROM + Lock", () =>
            RunFlashRomBatchAsync(serial, romDir, "flash_all_lock.bat"));
    }

    private async Task RunFlashRomBatchAsync(string serial, string romDir, string batchName)
    {
        var batchPath = Path.Combine(romDir, batchName);
        if (!File.Exists(batchPath))
        {
            AppendLog($"[FAIL] Không tìm thấy {batchPath}");
            return;
        }

        AppendLog($"[Flash ROM] Bắt đầu flash từ: {romDir}");
        AppendLog($"[Flash ROM] Script: {batchName}");
        AppendLog("[Flash ROM] Quá trình có thể mất 5-15 phút...");
        AppendLog("[Flash ROM] Nhấn «Hủy flash» để dừng giữa chừng.");

        _flashRomCts = new CancellationTokenSource();
        var ct = _flashRomCts.Token;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = batchPath,
                WorkingDirectory = romDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.Environment["ANDROID_SERIAL"] = serial;
            psi.Environment["FASTBOOT_SERIAL"] = serial;

            using var proc = Process.Start(psi);
            if (proc is null)
            {
                AppendLog("[FAIL] Không chạy được batch file");
                return;
            }

            var output = new List<string>();
            while (!proc.StandardOutput.EndOfStream)
            {
                if (ct.IsCancellationRequested)
                {
                    try { proc.Kill(); } catch { }
                    AppendLog("[Flash ROM] ⚠️ Đã hủy flash!");
                    return;
                }

                var line = await proc.StandardOutput.ReadLineAsync();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    output.Add(line);
                    if (line.StartsWith("error:", StringComparison.OrdinalIgnoreCase)
                        || line.Contains("FAILED", StringComparison.OrdinalIgnoreCase))
                        AppendLog($"[Flash ROM] ⚠️ {line}");
                }
            }

            await proc.WaitForExitAsync(ct);
            var error = await proc.StandardError.ReadToEndAsync();

            if (proc.ExitCode == 0)
            {
                AppendLog("[OK] Flash ROM hoàn tất!");
                AppendLog("[Flash ROM] Thiết bị sẽ tự reboot.");
            }
            else
            {
                AppendLog($"[FAIL] Flash ROM thất bại (mã {proc.ExitCode})");
                if (!string.IsNullOrEmpty(error))
                    AppendLog($"[Flash ROM] Lỗi:\n{error}");
            }

            AppendLog("[Flash ROM] === KẾT THÚC ===");
        }
        catch (OperationCanceledException)
        {
            AppendLog("[Flash ROM] ⚠️ Đã hủy flash!");
        }
        catch (Exception ex)
        {
            AppendLog($"[FAIL] Lỗi flash ROM: {ex.Message}");
        }
        finally
        {
            _flashRomCts?.Dispose();
            _flashRomCts = null;
        }
    }

    private void CancelFlashRom_Click(object sender, RoutedEventArgs e)
    {
        if (_flashRomCts is not null && !_flashRomCts.IsCancellationRequested)
        {
            var ok = MessageBox.Show(
                "Hủy flash ROM?\n\n"
                + "⚠️  Thiết bị có thể ở trạng thái không ổn định!\n"
                + "Nên reboot bootloader sau khi hủy.",
                "Xác nhận hủy", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (ok == MessageBoxResult.Yes)
                _flashRomCts.Cancel();
        }
    }

    private void FastbootAutoDetectSerial_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireFastbootDevice();
        if (serial is null) return;

        FastbootRomBox.Text = "";
        FastbootRomStatusText.Text = $"Thiết bị: {serial}";
        FastbootRomInfoText.Text = "";

        // Try to find common ROM paths
        var commonPaths = new[]
        {
            @"D:\Xiaomi\rom",
            @"D:\Miui\rom",
            @"D:\ROM",
            @"E:\Xiaomi\rom",
            @"E:\Miui\rom",
            @"E:\ROM",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
        };

        foreach (var path in commonPaths)
        {
            if (!Directory.Exists(path)) continue;

            // Look for flash_all.bat in subdirectories
            var found = Directory.GetFiles(path, "flash_all.bat", SearchOption.AllDirectories)
                .FirstOrDefault();

            if (found is not null)
            {
                var dir = Path.GetDirectoryName(found);
                if (dir is not null)
                {
                    FastbootRomBox.Text = dir;
                    ValidateFastbootRomDir(dir);
                    AppendLog($"[Fastboot] Tìm thấy ROM tại: {dir}");
                    return;
                }
            }
        }

        FastbootRomStatusText.Text = "Không tìm thấy ROM tự động — chọn thủ công";
    }

    private void FastbootFlashRomOpenDir_Click(object sender, RoutedEventArgs e)
    {
        var dir = GetBoxText(FastbootRomBox);
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
        {
            MessageBox.Show("Chưa chọn thư mục ROM.", "MintADB");
            return;
        }
        Process.Start(new ProcessStartInfo
        {
            FileName = dir,
            UseShellExecute = true,
        });
    }

    // ── Boot temporary ──

    private async void FastbootBootTemp_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireFastbootDevice();
        if (serial is null) return;

        var imagePath = GetFastbootImagePath();
        if (imagePath is null) return;

        var ok = MessageBox.Show(
            $"Boot tạm thời từ image?\n\nFile: {Path.GetFileName(imagePath)}\n\n"
            + "Thiết bị sẽ boot từ image này (không flash vào partition).\n"
            + "Khởi động lại sẽ về trạng thái cũ.",
            "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (ok != MessageBoxResult.Yes) return;

        await RunFastbootAsync("boot temp", async () =>
        {
            var r = await Fastboot.BootImageAsync(serial, imagePath);
            AppendLog(r.Ok ? $"[OK] Boot tạm thời" : $"[FAIL] {r.Combined}");
        });
    }

    // ── Flashing unlock/lock ──

    private async void FastbootUnlock_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireFastbootDevice();
        if (serial is null) return;

        var ok = MessageBox.Show(
            "Mở khóa bootloader?\n\n"
            + "⚠️  CẢNH BÁO NGHIÊM TRỌNG ⚠️\n"
            + "• Dữ liệu sẽ bị XÓA SẠCH khi unlock!\n"
            + "• Warranty sẽ bị mất.\n"
            + "• Thiết bị có thể bị brick nếu sai thao tác.\n"
            + "• Chỉ unlock nếu bạn biết mình đang làm gì.",
            "Xác nhận unlock", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (ok != MessageBoxResult.Yes) return;

        await RunFastbootAsync("unlock", async () =>
        {
            var r = await Fastboot.FlashingUnlockAsync(serial);
            AppendLog(r.Ok ? "[OK] Đã gửi lệnh unlock" : $"[FAIL] {r.Combined}");
        });
    }

    private async void FastbootLock_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireFastbootDevice();
        if (serial is null) return;

        var ok = MessageBox.Show(
            "Khóa bootloader?\n\n"
            + "Thiết bị sẽ bị khóa lại. Cần unlock lại nếu muốn flash sau này.",
            "Xác nhận lock", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (ok != MessageBoxResult.Yes) return;

        await RunFastbootAsync("lock", async () =>
        {
            var r = await Fastboot.FlashingLockAsync(serial);
            AppendLog(r.Ok ? "[OK] Đã gửi lệnh lock" : $"[FAIL] {r.Combined}");
        });
    }

    // ── OEM unlock/lock (Xiaomi) ──

    private async void FastbootOemUnlock_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireFastbootDevice();
        if (serial is null) return;

        var ok = MessageBox.Show(
            "OEM unlock (Xiaomi)?\n\n"
            + "⚠️  Dữ liệu sẽ bị xóa!\n"
            + "Chỉ dùng cho thiết bị Xiaomi.",
            "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (ok != MessageBoxResult.Yes) return;

        await RunFastbootAsync("oem unlock", async () =>
        {
            var r = await Fastboot.OemUnlockAsync(serial);
            AppendLog(r.Ok ? "[OK] Đã gửi lệnh oem unlock" : $"[FAIL] {r.Combined}");
        });
    }

    private async void FastbootOemLock_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireFastbootDevice();
        if (serial is null) return;

        var ok = MessageBox.Show(
            "OEM lock (Xiaomi)?\n\n"
            + "Khóa lại bootloader.",
            "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (ok != MessageBoxResult.Yes) return;

        await RunFastbootAsync("oem lock", async () =>
        {
            var r = await Fastboot.OemLockAsync(serial);
            AppendLog(r.Ok ? "[OK] Đã gửi lệnh oem lock" : $"[FAIL] {r.Combined}");
        });
    }

    // ── Factory reset ──

    private async void FastbootWipe_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireFastbootDevice();
        if (serial is null) return;

        var ok = MessageBox.Show(
            "Factory reset (xóa sạch dữ liệu)?\n\n"
            + "⚠️  TẤT CẢ DỮ LIỆU SẼ BỊ XÓA!\n"
            + "• Tài khoản Google sẽ bị xóa.\n"
            + "• Ảnh, video, ứng dụng sẽ mất.\n"
            + "• Chỉ thực hiện khi chắc chắn.",
            "Xác nhận wipe", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (ok != MessageBoxResult.Yes) return;

        await RunFastbootAsync("wipe", async () =>
        {
            var r = await Fastboot.WipeAsync(serial);
            AppendLog(r.Ok ? "[OK] Đã wipe" : $"[FAIL] {r.Combined}");
        });
    }

    // ── Set active slot ──

    private async void FastbootSetActiveA_Click(object sender, RoutedEventArgs e)
        => await SetActiveSlotAsync("a");

    private async void FastbootSetActiveB_Click(object sender, RoutedEventArgs e)
        => await SetActiveSlotAsync("b");

    private async Task SetActiveSlotAsync(string slot)
    {
        var serial = RequireFastbootDevice();
        if (serial is null) return;

        var ok = MessageBox.Show(
            $"Đặt slot active → {slot.ToUpper()}?\n\n"
            + "Thiết bị sẽ boot từ slot {slot.ToUpper()} ở lần khởi động tiếp theo.",
            "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (ok != MessageBoxResult.Yes) return;

        await RunFastbootAsync($"set_active {slot}", async () =>
        {
            var r = await Fastboot.SetActiveSlotAsync(serial, slot);
            AppendLog(r.Ok ? $"[OK] Slot → {slot.ToUpper()}" : $"[FAIL] {r.Combined}");
        });
    }

    // ── Flash vbmeta disable verity ──

    private async void FastbootFlashVbmetaDisable_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireFastbootDevice();
        if (serial is null) return;

        var imagePath = GetFastbootImagePath();
        if (imagePath is null) return;

        var ok = MessageBox.Show(
            $"Flash vbmeta với disable-verity?\n\n"
            + "File: {Path.GetFileName(imagePath)}\n\n"
            + "Tắt verified boot — cần thiết khi flash custom ROM.",
            "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (ok != MessageBoxResult.Yes) return;

        await RunFastbootAsync("flash vbmeta (disable verity)", async () =>
        {
            var r = await Fastboot.FlashVbmetaDisableVerityAsync(serial, imagePath);
            AppendLog(r.Ok ? "[OK] Flash vbmeta (disable verity)" : $"[FAIL] {r.Combined}");
        });
    }

    // ── Get all vars ──

    private async void FastbootGetVarAll_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireFastbootDevice();
        if (serial is null) return;

        await RunFastbootAsync("getvar all", async () =>
        {
            var r = await Fastboot.GetVarAllAsync(serial);
            FastbootVarText.Text = string.IsNullOrWhiteSpace(r.Combined) ? "(không đọc được)" : r.Combined;
            AppendLog($"[Fastboot] getvar all\n{r.Combined}");
        });
    }

    // ── OEM device info ──

    private async void FastbootOemDeviceInfo_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireFastbootDevice();
        if (serial is null) return;

        await RunFastbootAsync("oem device-info", async () =>
        {
            var r = await Fastboot.OemDeviceInfoAsync(serial);
            AppendLog($"[Fastboot] oem device-info\n{r.Combined}");
        });
    }

    // ── Format partition ──

    private async void FastbootFormat_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireFastbootDevice();
        if (serial is null) return;

        var partition = GetFastbootPartition().Trim();
        if (string.IsNullOrEmpty(partition))
        {
            MessageBox.Show("Nhập partition cần format.", "MintADB", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var ok = MessageBox.Show(
            $"Format partition «{partition}»?\n\n"
            + "⚠️  Dữ liệu trên partition sẽ bị xóa hoàn toàn!\n"
            + "Hành động không thể hoàn tác.",
            "Cảnh báo", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (ok != MessageBoxResult.Yes) return;

        await RunFastbootAsync($"format {partition}", async () =>
        {
            var r = await Fastboot.FormatAsync(serial, partition);
            AppendLog(r.Ok ? $"[OK] Format {partition}" : $"[FAIL] {r.Combined}");
        });
    }
}