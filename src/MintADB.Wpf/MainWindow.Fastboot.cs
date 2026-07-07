using System.IO;
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
            $"Flash partition «{partition}»?\n\nFile: {Path.GetFileName(imagePath)}\nThiết bị: {serial}\n\nSai partition có thể brick máy!",
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
}