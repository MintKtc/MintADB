using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MintADB.Wpf.Models;
using MintADB.Wpf.Services;

namespace MintADB.Wpf;

public partial class MainWindow
{
    private void ToolNavBasic_Click(object sender, RoutedEventArgs e) => ShowToolPage(0);
    private void ToolNavScreen_Click(object sender, RoutedEventArgs e) => ShowToolPage(2);
    private void ToolNavNetwork_Click(object sender, RoutedEventArgs e) => ShowToolPage(3);
    private void ToolNavSystem_Click(object sender, RoutedEventArgs e)
    {
        ShowToolPage(4);
        ShowSystemSubPage(0);
    }

    private void SysNavBattery_Click(object sender, RoutedEventArgs e) => ShowSystemSubPage(0);

    private void SysNavDisplay_Click(object sender, RoutedEventArgs e) => ShowSystemSubPage(1);

    private void SysNavDevice_Click(object sender, RoutedEventArgs e) => ShowSystemSubPage(2);

    private async void LoadBatteryInfo_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        SetActionButtonsEnabled(false);
        ResetBatteryVisual();
        BatteryInfoText.Text = "Đang đọc...";
        try
        {
            var info = await Hardware.GetBatteryInfoAsync(serial);
            UpdateBatteryVisual(info);
            BatteryInfoText.Text = info.MetricsText;
            AppendLog("--- Thông tin pin ---");
            AppendLog(info.MetricsText);
        }
        catch (Exception ex)
        {
            ResetBatteryVisual();
            BatteryInfoText.Text = $"Lỗi: {ex.Message}";
            AppendLog($"[Pin] Lỗi: {ex.Message}");
        }
        finally { SetActionButtonsEnabled(true); }
    }

    private async void LoadDisplayInfo_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        SetActionButtonsEnabled(false);
        ResetDisplayVisual();
        DisplayInfoText.Text = "Đang đọc...";
        try
        {
            var info = await Hardware.GetDisplayInfoAsync(serial);
            UpdateDisplayVisual(info);
            DisplayInfoText.Text = info.MetricsText;
            AppendLog("--- Thông tin màn hình ---");
            AppendLog(info.MetricsText);
        }
        catch (Exception ex)
        {
            ResetDisplayVisual();
            DisplayInfoText.Text = $"Lỗi: {ex.Message}";
            AppendLog($"[Màn hình] Lỗi: {ex.Message}");
        }
        finally { SetActionButtonsEnabled(true); }
    }

    private async void LoadDeviceInfo_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        SetActionButtonsEnabled(false);
        ResetDeviceVisual();
        SysDeviceInfoText.Text = "Đang đọc...";
        try
        {
            var info = await Hardware.GetDeviceInfoAsync(serial);
            UpdateDeviceVisual(info);
            SysDeviceInfoText.Text = info.MetricsText;
            AppendLog("--- Thông tin máy ---");
            AppendLog(info.MetricsText);
        }
        catch (Exception ex)
        {
            ResetDeviceVisual();
            SysDeviceInfoText.Text = $"Lỗi: {ex.Message}";
            AppendLog($"[Máy] Lỗi: {ex.Message}");
        }
        finally { SetActionButtonsEnabled(true); }
    }

    private void ToolNavAdvanced_Click(object sender, RoutedEventArgs e) => ShowToolPage(5);

    private void BrowsePush_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "Chọn file để push", Filter = "Tất cả (*.*)|*.*" };
        if (dlg.ShowDialog() == true)
            PushLocalBox.Text = dlg.FileName;
    }

    private async void Push_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var local = GetBoxText(PushLocalBox);
        var remote = GetBoxText(PushRemoteBox);
        if (string.IsNullOrEmpty(local) || !File.Exists(local))
        {
            MessageBox.Show("Chọn file local hợp lệ.", "MintADB");
            return;
        }
        if (string.IsNullOrEmpty(remote))
        {
            MessageBox.Show("Nhập đường dẫn remote.", "MintADB");
            return;
        }

        await RunToolAsync("Push", async () =>
        {
            AppendLog($"Push {local} → {remote}");
            var r = await Tools.PushAsync(serial, local, remote);
            AppendLog(r.Ok ? "[OK] Push thành công" : $"[FAIL] {r.Combined}");
        });
    }

    private async void Pull_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var remote = GetBoxText(PullRemoteBox);
        if (string.IsNullOrEmpty(remote))
        {
            MessageBox.Show("Nhập đường dẫn file trên máy.", "MintADB");
            return;
        }

        await RunToolAsync("Pull", async () =>
        {
            var dir = Path.Combine(AdbToolsService.MintAdbDir, "Pulls");
            Directory.CreateDirectory(dir);
            var name = Path.GetFileName(remote.TrimEnd('/'));
            if (string.IsNullOrEmpty(name)) name = "pull_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var local = Path.Combine(dir, name);

            AppendLog($"Pull {remote} → {local}");
            var r = await Tools.PullAsync(serial, remote, local);
            AppendLog(r.Ok ? $"[OK] Đã lưu: {local}" : $"[FAIL] {r.Combined}");
        });
    }

    private async void ListRemote_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var path = GetBoxText(ListRemoteBox);
        if (string.IsNullOrEmpty(path)) path = "/sdcard/";

        await RunToolAsync("List remote", async () =>
        {
            var r = await Tools.ListRemoteAsync(serial, path);
            AppendLog($"--- ls {path} ---");
            AppendLog(string.IsNullOrWhiteSpace(r.Combined) ? "(trống)" : r.Combined);
        });
    }

    private async void BackupApk_Click(object sender, RoutedEventArgs e)
        => await RunOnSelectedPackages("Backup APK", async (serial, pkg) =>
        {
            AppendLog($"Backup {pkg}...");
            var path = await Tools.BackupApkAsync(serial, pkg);
            AppendLog(path is not null ? $"[OK] {path}" : $"[FAIL] Không backup được {pkg}");
        }, requireSelection: true, singleOnly: true);

    private async void RecordScreen_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        if (!int.TryParse(GetBoxText(RecordSecondsBox), out var seconds) || seconds is < 1 or > 180)
        {
            MessageBox.Show("Nhập thời lượng quay từ 1–180 giây.", "MintADB");
            return;
        }

        var saveDir = ResolveSaveDir(RecordSaveBox, "Recordings");

        await RunToolAsync("Screen record", async () =>
        {
            AppendLog($"Quay màn hình {seconds} giây → {saveDir}...");
            var path = await Tools.RecordScreenAsync(serial, seconds, saveDir);
            if (path is not null)
            {
                AppendLog($"[OK] Đã lưu: {path}");
                MessageBox.Show($"Đã lưu video:\n{path}", "Screen record");
            }
            else AppendLog("[FAIL] Không quay được màn hình");
        });
    }

    private int GetSelectedMobileSimSlot() => MobileSimSlot.SelectedIndex switch
    {
        2 => 2,
        1 => 1,
        _ => 0,
    };

    private int GetSelectedMobileNetworkMode()
    {
        if (MobileNetworkPreset.SelectedItem is ComboBoxItem item
            && item.Tag is string tag
            && int.TryParse(tag, out var mode))
            return mode;
        return 33;
    }

    private async void SetMobileNetwork_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var mode = GetSelectedMobileNetworkMode();
        var slot = GetSelectedMobileSimSlot();
        var label = MobileNetworkMode.Describe(mode);

        if (MessageBox.Show(
                $"Đặt mạng di động SIM {Math.Max(slot, 1)} → {label}?",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        await RunToolAsync("Mạng di động", async () =>
        {
            var r = await Tools.SetPreferredNetworkModeAsync(serial, mode, slot);
            if (r.Ok)
            {
                AppendLog($"[OK] {label} (mode {mode})");
                AppendLog("Đã refresh data — nếu chưa đổi, thử bật/tắt máy bay hoặc reboot.");
            }
            else AppendLog($"[FAIL] {r.Combined}");
        });
    }

    private async void CheckMobileNetwork_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var slot = GetSelectedMobileSimSlot();
        await RunToolAsync("Kiểm tra mạng", async () =>
        {
            var status = await Tools.GetPreferredNetworkModeStatusAsync(serial, slot);
            AppendLog($"[Mạng] {status}");
        });
    }

    private string GetSelectedLocale()
    {
        var custom = GetBoxText(LocaleCustomBox);
        if (!string.IsNullOrEmpty(custom))
            return custom;

        if (LocalePreset.SelectedItem is ComboBoxItem item && item.Tag is string tag && !string.IsNullOrWhiteSpace(tag))
            return tag;

        return "vi-VN";
    }

    private async void SetLocale_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var raw = GetSelectedLocale();
        if (!AdbToolsService.TryNormalizeLocale(raw, out var locale))
        {
            MessageBox.Show("Nhập locale hợp lệ, ví dụ: vi-VN, en-US, zh-CN.", "MintADB");
            return;
        }

        if (MessageBox.Show(
                $"Đặt locale hệ thống → {locale}?\nMột số máy cần reboot để áp dụng hoàn toàn.",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        await RunToolAsync("Locale", async () =>
        {
            var r = await Tools.SetSystemLocaleAsync(serial, locale);
            if (r.Ok)
            {
                AppendLog($"[OK] Locale → {locale}");
                AppendLog("Nếu giao diện chưa đổi, thử reboot máy.");
            }
            else AppendLog($"[FAIL] {r.Combined}");
        });
    }

    private async void CheckLocale_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        await RunToolAsync("Kiểm tra locale", async () =>
        {
            var status = await Tools.GetSystemLocaleStatusAsync(serial);
            AppendLog($"[Locale] {status}");
        });
    }

    private string GetSelectedAdDns()
    {
        if (AdDnsPreset.SelectedItem is ComboBoxItem item && item.Tag is string host && !string.IsNullOrWhiteSpace(host))
            return host;
        return "dns.adguard.com";
    }

    private async void SetAdDns_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var host = GetSelectedAdDns();
        await RunToolAsync("DNS chặn quảng cáo", async () =>
        {
            var r = await Tools.SetPrivateDnsAsync(serial, host);
            if (r.Ok)
            {
                AppendLog($"[OK] Private DNS → {host}");
                AppendLog("Nếu chưa có hiệu lực: tắt/bật Wi‑Fi hoặc reboot.");
            }
            else AppendLog($"[FAIL] {r.Combined}");
        });
    }

    private async void ClearAdDns_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        await RunToolAsync("DNS mặc định", async () =>
        {
            var r = await Tools.ClearPrivateDnsAsync(serial);
            AppendLog(r.Ok ? "[OK] Đã tắt Private DNS (DNS mặc định nhà mạng)" : $"[FAIL] {r.Combined}");
        });
    }

    private async void CheckAdDns_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        await RunToolAsync("Kiểm tra DNS", async () =>
        {
            var status = await Tools.GetPrivateDnsStatusAsync(serial);
            AppendLog($"[DNS] {status}");
        });
    }

}