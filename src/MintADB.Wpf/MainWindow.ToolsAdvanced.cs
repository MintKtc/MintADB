using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MintADB.Wpf.Models;
using MintADB.Wpf.Resources;
using MintADB.Wpf.Services;

namespace MintADB.Wpf;

public partial class MainWindow
{
    private NetworkService? _networkService;
    private NetworkService Network => _networkService ??= new NetworkService(_adb);

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
        BatteryInfoText.Text = Loc.Get("ReadingStatus", "Reading…");
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
        DisplayInfoText.Text = Loc.Get("ReadingStatus", "Reading…");
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
        SysDeviceInfoText.Text = Loc.Get("ReadingStatus", "Reading…");
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

    // ── Storage Info ──

    private async void LoadStorageInfo_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        SetActionButtonsEnabled(false);
        StorageInfoText.Text = Loc.Get("ReadingStatus", "Reading…");
        try
        {
            var info = await Hardware.GetStorageInfoAsync(serial);
            StorageInfoText.Text = $"Bộ nhớ trong: {info.InternalUsedText} / {info.InternalTotalText} ({info.InternalPercent:F0}%)\n"
                                + $"Trống: {info.InternalAvailText}";
            if (info.SdTotal > 0)
                StorageInfoText.Text += $"\nSD Card: {info.SdUsedText} / {info.SdTotalText}";
            AppendLog("--- Thông tin bộ nhớ ---");
            AppendLog(StorageInfoText.Text);
        }
        catch (Exception ex)
        {
            StorageInfoText.Text = $"Lỗi: {ex.Message}";
        }
        finally { SetActionButtonsEnabled(true); }
    }

    // ── RAM Info ──

    private async void LoadRamInfo_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        SetActionButtonsEnabled(false);
        RamInfoText.Text = Loc.Get("ReadingStatus", "Reading…");
        try
        {
            var info = await Hardware.GetRamInfoAsync(serial);
            RamInfoText.Text = $"RAM: {info.UsedText} / {info.TotalText} ({info.Percent:F0}%)\n"
                            + $"Available: {info.AvailableText} · Cached: {info.CachedText}";
            if (info.SwapTotalKb > 0)
                RamInfoText.Text += $"\nSwap: {info.SwapText}";
            AppendLog("--- Thông tin RAM ---");
            AppendLog(RamInfoText.Text);
        }
        catch (Exception ex)
        {
            RamInfoText.Text = $"Lỗi: {ex.Message}";
        }
        finally { SetActionButtonsEnabled(true); }
    }

    // ── CPU Info ──

    private async void LoadCpuInfo_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        SetActionButtonsEnabled(false);
        CpuInfoText.Text = Loc.Get("ReadingStatus", "Reading…");
        try
        {
            var info = await Hardware.GetCpuInfoAsync(serial);
            CpuInfoText.Text = info.Summary;
            AppendLog("--- Thông tin CPU ---");
            AppendLog(info.Summary);
        }
        catch (Exception ex)
        {
            CpuInfoText.Text = $"Lỗi: {ex.Message}";
        }
        finally { SetActionButtonsEnabled(true); }
    }

    // ── GPU Info ──

    private async void LoadGpuInfo_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        SetActionButtonsEnabled(false);
        GpuInfoText.Text = Loc.Get("ReadingStatus", "Reading…");
        try
        {
            var info = await Hardware.GetGpuInfoAsync(serial);
            GpuInfoText.Text = info.Summary;
            AppendLog("--- Thông tin GPU ---");
            AppendLog(info.Summary);
        }
        catch (Exception ex)
        {
            GpuInfoText.Text = $"Lỗi: {ex.Message}";
        }
        finally { SetActionButtonsEnabled(true); }
    }

    // ── Touch Info ──

    private async void LoadTouchInfo_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        SetActionButtonsEnabled(false);
        TouchInfoText.Text = Loc.Get("ReadingStatus", "Reading…");
        try
        {
            var info = await Hardware.GetTouchInfoAsync(serial);
            TouchInfoText.Text = $"Touch sampling: {info.SamplingRateText}";
            AppendLog("--- Thông tin Touch ---");
            AppendLog(TouchInfoText.Text);
        }
        catch (Exception ex)
        {
            TouchInfoText.Text = $"Lỗi: {ex.Message}";
        }
        finally { SetActionButtonsEnabled(true); }
    }

    private void ToolNavAdvanced_Click(object sender, RoutedEventArgs e) => ShowToolPage(6);

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

    // ── WiFi ──

    private async void ToggleWifi_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var enable = WifiToggle.IsChecked == true;
        await RunToolAsync($"WiFi {(enable ? "bật" : "tắt")}", async () =>
        {
            var r = await Network.SetWifiEnabledAsync(serial, enable);
            AppendLog(r.Ok ? $"[OK] WiFi → {(enable ? "ON" : "OFF")}" : $"[FAIL] {r.Combined}");
        });
    }

    private async void ScanWifi_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        await RunToolAsync("Scan WiFi", async () =>
        {
            var result = await Network.ScanWifiAsync(serial);
            AppendLog($"[WiFi Scan]\n{result}");
        });
    }

    private async void ConnectWifi_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var ssid = GetBoxText(WifiSsidBox);
        var password = GetBoxText(WifiPasswordBox);
        if (string.IsNullOrEmpty(ssid))
        {
            MessageBox.Show("Nhập tên WiFi (SSID).", "MintADB");
            return;
        }

        await RunToolAsync($"WiFi connect → {ssid}", async () =>
        {
            var r = await Network.ConnectWifiAsync(serial, ssid, string.IsNullOrEmpty(password) ? null : password);
            AppendLog(r.Ok ? $"[OK] Đang kết nối {ssid}" : $"[FAIL] {r.Combined}");
        });
    }

    private async void DisconnectWifi_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        await RunToolAsync("WiFi disconnect", async () =>
        {
            var r = await Network.DisconnectWifiAsync(serial);
            AppendLog(r.Ok ? "[OK] Đã ngắt WiFi" : $"[FAIL] {r.Combined}");
        });
    }

    // ── Bluetooth ──

    private async void ToggleBluetooth_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var enable = BluetoothToggle.IsChecked == true;
        await RunToolAsync($"Bluetooth {(enable ? "bật" : "tắt")}", async () =>
        {
            var r = await Network.SetBluetoothEnabledAsync(serial, enable);
            AppendLog(r.Ok ? $"[OK] Bluetooth → {(enable ? "ON" : "OFF")}" : $"[FAIL] {r.Combined}");
        });
    }

    private async void ScanBluetooth_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        await RunToolAsync("Scan Bluetooth", async () =>
        {
            var result = await Network.ScanBluetoothAsync(serial);
            AppendLog($"[BT Scan]\n{result}");
        });
    }

    // ── Airplane Mode ──

    private async void ToggleAirplane_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var enable = AirplaneToggle.IsChecked == true;
        await RunToolAsync($"Airplane {(enable ? "bật" : "tắt")}", async () =>
        {
            var r = await Network.SetAirplaneModeAsync(serial, enable);
            AppendLog(r.Ok ? $"[OK] Airplane → {(enable ? "ON" : "OFF")}" : $"[FAIL] {r.Combined}");
        });
    }

    // ── Hotspot ──

    private async void ToggleHotspot_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var enable = HotspotToggle.IsChecked == true;
        await RunToolAsync($"Hotspot {(enable ? "bật" : "tắt")}", async () =>
        {
            var r = await Network.SetHotspotEnabledAsync(serial, enable);
            AppendLog(r.Ok ? $"[OK] Hotspot → {(enable ? "ON" : "OFF")}" : $"[FAIL] {r.Combined}");
        });
    }

    private async void ReadNetworkStatus_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        NetworkStatusText.Text = Loc.Get("ReadingStatus", "Reading…");
        try
        {
            // Full status (WiFi + BT + airplane + hotspot + mobile + IP)
            var status = await Network.GetFullNetworkStatusAsync(serial);
            NetworkStatusText.Text = status;
            AppendLog("--- Trạng thái mạng ---");
            AppendLog(status);
        }
        catch (Exception ex)
        {
            NetworkStatusText.Text = $"Lỗi: {ex.Message}";
        }
    }
}