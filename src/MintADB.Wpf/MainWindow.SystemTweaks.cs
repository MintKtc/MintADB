using System.Windows;
using System.Windows.Controls;
using MintADB.Wpf.Services;

namespace MintADB.Wpf;

public partial class MainWindow
{
    private SystemTweaksService? _systemTweaks;

    private SystemTweaksService SystemTweaks => _systemTweaks ??= new SystemTweaksService(_adb);

    private void ToolNavTweaks_Click(object sender, RoutedEventArgs e) => ShowToolPage(5);

    // ── DPI ──

    private async void ReadDensity_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        DensityStatusText.Text = "Đang đọc...";
        try
        {
            var status = await SystemTweaks.GetDensityStatusAsync(serial);
            DensityStatusText.Text = status;
            AppendLog("--- DPI ---");
            AppendLog(status);
        }
        catch (Exception ex)
        {
            DensityStatusText.Text = $"Lỗi: {ex.Message}";
        }
    }

    private async void ApplyDensity_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        if (!int.TryParse(DensityCustomBox.Text.Trim(), out var dpi) || dpi < 120 || dpi > 800)
        {
            MessageBox.Show("Nhập DPI hợp lệ (120–800).\nVí dụ: 320, 360, 400, 440, 480", "MintADB");
            return;
        }

        if (MessageBox.Show($"Đặt DPI → {dpi}?\n\nGiao diện sẽ thay đổi ngay. Có thể reset sau.",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        await RunToolAsync("DPI", async () =>
        {
            var r = await SystemTweaks.SetDensityAsync(serial, dpi);
            AppendLog(r.Ok ? $"[OK] DPI → {dpi}" : $"[FAIL] {r.Combined}");
            DensityStatusText.Text = await SystemTweaks.GetDensityStatusAsync(serial);
        });
    }

    private void DensityPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string dpi })
            DensityCustomBox.Text = dpi;
    }

    private async void ResetDensity_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        await RunToolAsync("DPI reset", async () =>
        {
            var r = await SystemTweaks.ResetDensityAsync(serial);
            AppendLog(r.Ok ? "[OK] Đã reset DPI" : $"[FAIL] {r.Combined}");
            DensityStatusText.Text = await SystemTweaks.GetDensityStatusAsync(serial);
        });
    }

    // ── Animation ──

    private async void ReadAnimationStatus_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        AnimationStatusText.Text = "Đang đọc...";
        try
        {
            var status = await SystemTweaks.GetAnimationStatusAsync(serial);
            AnimationStatusText.Text = status;
            AppendLog("--- Animation ---");
            AppendLog(status);
        }
        catch (Exception ex)
        {
            AnimationStatusText.Text = $"Lỗi: {ex.Message}";
        }
    }

    private async void ApplyAnimationSpeed_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var scale = AnimationSpeedCombo.SelectedItem switch
        {
            ComboBoxItem { Tag: string t } => t,
            _ => "1.0",
        };

        var label = AnimationSpeedCombo.SelectedItem switch
        {
            ComboBoxItem { Content: string c } => c,
            _ => "1x",
        };

        if (MessageBox.Show($"Đặt tốc độ animation → {label}?",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        await RunToolAsync($"Animation {label}", async () =>
        {
            var (ok, fail) = await SystemTweaks.SetAnimationSpeedAsync(serial, scale, AppendLog);
            AppendLog($"[Animation] {ok} OK · {fail} WARN");
            AnimationStatusText.Text = await SystemTweaks.GetAnimationStatusAsync(serial);
        });
    }

    // ── Navigation ──

    private async void ReadNavigationStatus_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        NavStatusText.Text = "Đang đọc...";
        try
        {
            var status = await SystemTweaks.GetNavigationStatusAsync(serial);
            NavStatusText.Text = status;
            AppendLog("--- Điều hướng ---");
            AppendLog(status);
        }
        catch (Exception ex)
        {
            NavStatusText.Text = $"Lỗi: {ex.Message}";
        }
    }

    private async void SetGestureNav_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        if (MessageBox.Show("Chuyển sang điều hướng cử chỉ?\n\nCó thể đổi lại 3 nút sau.",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        await RunToolAsync("Cử chỉ", async () =>
        {
            var r = await SystemTweaks.SetGestureNavigationAsync(serial);
            AppendLog(r.Ok ? "[OK] Đã chuyển sang cử chỉ" : $"[FAIL] {r.Combined}");
            NavStatusText.Text = await SystemTweaks.GetNavigationStatusAsync(serial);
        });
    }

    private async void SetThreeButtonNav_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        if (MessageBox.Show("Chuyển sang điều hướng 3 nút?",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        await RunToolAsync("3 nút", async () =>
        {
            var r = await SystemTweaks.SetThreeButtonNavigationAsync(serial);
            AppendLog(r.Ok ? "[OK] Đã chuyển sang 3 nút" : $"[FAIL] {r.Combined}");
            NavStatusText.Text = await SystemTweaks.GetNavigationStatusAsync(serial);
        });
    }

    private async void ToggleImmersiveMode_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var enable = ImmersiveToggle.IsChecked == true;
        if (MessageBox.Show(enable
                ? "Bật chế độ immersive (ẩn thanh điều hướng toàn màn hình)?"
                : "Tắt chế độ immersive?",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        await RunToolAsync("Immersive", async () =>
        {
            var r = await SystemTweaks.SetImmersiveModeAsync(serial, enable);
            AppendLog(r.Ok
                ? $"[OK] Immersive {(enable ? "bật" : "tắt")}"
                : $"[FAIL] {r.Combined}");
        });
    }

    // ── OTA Blocker ──

    private async void ReadOtaStatus_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        OtaStatusText.Text = "Đang kiểm tra...";
        try
        {
            var status = await SystemTweaks.GetOtaBlockStatusAsync(serial);
            OtaStatusText.Text = status;
            AppendLog("--- OTA ---");
            AppendLog(status);
        }
        catch (Exception ex)
        {
            OtaStatusText.Text = $"Lỗi: {ex.Message}";
        }
    }

    private async void BlockOta_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        if (MessageBox.Show(
                "Chặn cập nhật OTA?\n\n"
                + "· Tắt gói cập nhật hệ thống\n"
                + "· Private DNS → dns.adguard.com\n"
                + "· Có thể khôi phục sau.",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        await RunToolAsync("Chặn OTA", async () =>
        {
            var (ok, fail) = await SystemTweaks.BlockOtaUpdatesAsync(serial, AppendLog);
            AppendLog($"[OTA] {ok} OK · {fail} WARN");
            OtaStatusText.Text = await SystemTweaks.GetOtaBlockStatusAsync(serial);
        });
    }

    private async void UnblockOta_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        await RunToolAsync("Khôi phục OTA", async () =>
        {
            var (ok, fail) = await SystemTweaks.UnblockOtaUpdatesAsync(serial, AppendLog);
            AppendLog($"[OTA] {ok} OK · {fail} WARN");
            OtaStatusText.Text = await SystemTweaks.GetOtaBlockStatusAsync(serial);
        });
    }

    // ── Audio ──

    private async void ReadAudioStatus_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        AudioStatusText.Text = "Đang đọc...";
        try
        {
            var status = await SystemTweaks.GetAudioStatusAsync(serial);
            AudioStatusText.Text = status;
            AppendLog("--- Audio ---");
            AppendLog(status);
        }
        catch (Exception ex)
        {
            AudioStatusText.Text = $"Lỗi: {ex.Message}";
        }
    }

    private async void ApplyBluetoothCodec_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var codecValue = AudioCodecCombo.SelectedItem switch
        {
            ComboBoxItem { Tag: string t } => t,
            _ => "",
        };
        var codecLabel = AudioCodecCombo.SelectedItem switch
        {
            ComboBoxItem { Content: string c } => c,
            _ => "Mặc định",
        };

        if (MessageBox.Show($"Đặt codec Bluetooth → {codecLabel}?\nCần kết nối lại tai nghe.",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        await RunToolAsync($"Codec {codecLabel}", async () =>
        {
            var r = await SystemTweaks.SetBluetoothCodecAsync(serial, codecValue);
            AppendLog(r.Ok
                ? $"[OK] Codec → {codecLabel}"
                : $"[FAIL] {r.Combined}");
            AudioStatusText.Text = await SystemTweaks.GetAudioStatusAsync(serial);
        });
    }

    private async void ToggleAbsoluteVolume_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var enable = AbsVolumeToggle.IsChecked == true;
        await RunToolAsync($"Âm lượng tuyệt đối {(enable ? "bật" : "tắt")}", async () =>
        {
            var r = await SystemTweaks.ToggleAbsoluteVolumeAsync(serial, enable);
            AppendLog(r.Ok
                ? $"[OK] Absolute volume → {(enable ? "bật" : "tắt")}"
                : $"[FAIL] {r.Combined}");
            AudioStatusText.Text = await SystemTweaks.GetAudioStatusAsync(serial);
        });
    }

    private async void GoogleServicesStatus_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        GoogleServicesText.Text = "Đang kiểm tra...";
        try
        {
            var status = await SystemTweaks.GetGoogleServicesStatusAsync(serial);
            GoogleServicesText.Text = status;
            AppendLog("--- Google Services ---");
            AppendLog(status);
        }
        catch (Exception ex)
        {
            GoogleServicesText.Text = $"Lỗi: {ex.Message}";
        }
    }

    private async void EnableGoogleServices_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        if (MessageBox.Show(
                "Bật dịch vụ Google (GMS)?\n\n"
                + "· Bật các package Google có sẵn\n"
                + "· Cấp quyền cần thiết\n"
                + "· Chỉ áp dụng nếu đã có Google Services",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        await RunToolAsync("Google Services", async () =>
        {
            var (ok, fail) = await SystemTweaks.EnableGoogleServicesAsync(serial, AppendLog);
            AppendLog($"[Google] {ok} OK · {fail} WARN");
            GoogleServicesText.Text = await SystemTweaks.GetGoogleServicesStatusAsync(serial);
        });
    }

    // ── Refresh Rate ──

    private async void ReadRefreshRate_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        RefreshRateStatusText.Text = "Đang đọc...";
        try
        {
            var status = await SystemTweaks.GetRefreshRateStatusAsync(serial);
            RefreshRateStatusText.Text = status;
            AppendLog("--- Hz ---");
            AppendLog(status);
        }
        catch (Exception ex)
        {
            RefreshRateStatusText.Text = $"Lỗi: {ex.Message}";
        }
    }

    private async void SetRefreshRate_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        if (sender is Button { Tag: string hzStr } && int.TryParse(hzStr, out var hz))
        {
            if (MessageBox.Show($"Đặt tần số quét → {hz}Hz?",
                    "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            await RunToolAsync($"Hz → {hz}", async () =>
            {
                var r = await SystemTweaks.SetRefreshRateAsync(serial, hz);
                AppendLog(r.Ok ? $"[OK] Hz → {hz}" : $"[FAIL] {r.Combined}");
                RefreshRateStatusText.Text = await SystemTweaks.GetRefreshRateStatusAsync(serial);
            });
        }
    }

    // ── Font Scale ──

    private async void ReadFontScale_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        FontScaleStatusText.Text = "Đang đọc...";
        try
        {
            var status = await SystemTweaks.GetFontScaleStatusAsync(serial);
            FontScaleStatusText.Text = status;
            AppendLog("--- Font ---");
            AppendLog(status);
        }
        catch (Exception ex)
        {
            FontScaleStatusText.Text = $"Lỗi: {ex.Message}";
        }
    }

    private async void SetFontScale_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        if (sender is Button { Tag: string scaleStr } && float.TryParse(scaleStr, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var scale))
        {
            if (MessageBox.Show($"Đặt cỡ chữ → {scale}x?",
                    "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            await RunToolAsync($"Font → {scale}x", async () =>
            {
                var r = await SystemTweaks.SetFontScaleAsync(serial, scale);
                AppendLog(r.Ok ? $"[OK] Font → {scale}x" : $"[FAIL] {r.Combined}");
                FontScaleStatusText.Text = await SystemTweaks.GetFontScaleStatusAsync(serial);
            });
        }
    }

    // ── Screen Timeout ──

    private async void ReadScreenTimeout_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        ScreenTimeoutStatusText.Text = "Đang đọc...";
        try
        {
            var status = await SystemTweaks.GetScreenTimeoutStatusAsync(serial);
            ScreenTimeoutStatusText.Text = status;
            AppendLog("--- Timeout ---");
            AppendLog(status);
        }
        catch (Exception ex)
        {
            ScreenTimeoutStatusText.Text = $"Lỗi: {ex.Message}";
        }
    }

    private async void SetScreenTimeout_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        if (sender is Button { Tag: string msStr } && int.TryParse(msStr, out var ms))
        {
            var label = ms switch
            {
                < 60000 => $"{ms / 1000}s",
                < 3600000 => $"{ms / 60000} phút",
                _ => "vô hạn"
            };

            if (MessageBox.Show($"Đặt thời gian tắt màn hình → {label}?",
                    "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            await RunToolAsync($"Timeout → {label}", async () =>
            {
                var r = await SystemTweaks.SetScreenTimeoutAsync(serial, ms);
                AppendLog(r.Ok ? $"[OK] Timeout → {label}" : $"[FAIL] {r.Combined}");
                ScreenTimeoutStatusText.Text = await SystemTweaks.GetScreenTimeoutStatusAsync(serial);
            });
        }
    }

    // ── Developer Options ──

    private async void ReadDevOptions_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        DevOptionsStatusText.Text = "Đang đọc...";
        try
        {
            var stay = await SystemTweaks.GetStayAwakeStatusAsync(serial);
            var taps = await SystemTweaks.GetShowTapsStatusAsync(serial);
            var ptr = await SystemTweaks.GetPointerLocationStatusAsync(serial);
            DevOptionsStatusText.Text = $"{stay}\n{taps}\n{ptr}";
            StayAwakeToggle.IsChecked = stay.Contains("USB=True");
            ShowTapsToggle.IsChecked = taps.Contains("bật");
            PointerLocationToggle.IsChecked = ptr.Contains("bật");
            AppendLog("--- Dev Options ---");
            AppendLog(stay);
            AppendLog(taps);
            AppendLog(ptr);
        }
        catch (Exception ex)
        {
            DevOptionsStatusText.Text = $"Lỗi: {ex.Message}";
        }
    }

    private async void ToggleStayAwake_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var enable = StayAwakeToggle.IsChecked == true;
        await RunToolAsync($"Stay awake {(enable ? "bật" : "tắt")}", async () =>
        {
            var r = await SystemTweaks.SetStayAwakeAsync(serial, enable);
            AppendLog(r.Ok ? $"[OK] Stay awake → {(enable ? "bật" : "tắt")}" : $"[FAIL] {r.Combined}");
            DevOptionsStatusText.Text = await SystemTweaks.GetStayAwakeStatusAsync(serial);
        });
    }

    private async void ToggleShowTaps_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var enable = ShowTapsToggle.IsChecked == true;
        await RunToolAsync($"Show taps {(enable ? "bật" : "tắt")}", async () =>
        {
            var r = await SystemTweaks.SetShowTapsAsync(serial, enable);
            AppendLog(r.Ok ? $"[OK] Show taps → {(enable ? "bật" : "tắt")}" : $"[FAIL] {r.Combined}");
            DevOptionsStatusText.Text = await SystemTweaks.GetShowTapsStatusAsync(serial);
        });
    }

    private async void TogglePointerLocation_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var enable = PointerLocationToggle.IsChecked == true;
        await RunToolAsync($"Pointer location {(enable ? "bật" : "tắt")}", async () =>
        {
            var r = await SystemTweaks.SetPointerLocationAsync(serial, enable);
            AppendLog(r.Ok ? $"[OK] Pointer location → {(enable ? "bật" : "tắt")}" : $"[FAIL] {r.Combined}");
            DevOptionsStatusText.Text = await SystemTweaks.GetPointerLocationStatusAsync(serial);
        });
    }

    // ── Status Bar ──

    private async void ReadStatusBar_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        StatusBarStatusText.Text = "Đang đọc...";
        try
        {
            var status = await SystemTweaks.GetStatusBarStatusAsync(serial);
            StatusBarStatusText.Text = status;
            AppendLog("--- Status Bar ---");
            AppendLog(status);
        }
        catch (Exception ex)
        {
            StatusBarStatusText.Text = $"Lỗi: {ex.Message}";
        }
    }

    private async void ApplyStatusBar_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var showClock = StatusBarClockToggle.IsChecked == true;
        var showBattery = StatusBarBatteryToggle.IsChecked == true;
        var showSignal = StatusBarSignalToggle.IsChecked == true;

        await RunToolAsync("Status Bar", async () =>
        {
            var (ok, fail) = await SystemTweaks.SetStatusBarAsync(serial, showClock, showBattery, showSignal, AppendLog);
            AppendLog($"[StatusBar] {ok} OK · {fail} WARN");
            StatusBarStatusText.Text = await SystemTweaks.GetStatusBarStatusAsync(serial);
        });
    }

    // ── Battery Percentage ──

    private async void ReadBatteryPercent_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        BatteryPercentStatusText.Text = "Đang đọc...";
        try
        {
            var status = await SystemTweaks.GetBatteryPercentStatusAsync(serial);
            BatteryPercentStatusText.Text = status;
            AppendLog("--- Battery % ---");
            AppendLog(status);
        }
        catch (Exception ex)
        {
            BatteryPercentStatusText.Text = $"Lỗi: {ex.Message}";
        }
    }

    private async void SetBatteryPercent_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        if (sender is Button { Tag: string showStr } && bool.TryParse(showStr, out var show))
        {
            await RunToolAsync($"Battery % {(show ? "hiện" : "ẩn")}", async () =>
            {
                var r = await SystemTweaks.SetBatteryPercentAsync(serial, show);
                AppendLog(r.Ok ? $"[OK] Battery % → {(show ? "hiện" : "ẩn")}" : $"[FAIL] {r.Combined}");
                BatteryPercentStatusText.Text = await SystemTweaks.GetBatteryPercentStatusAsync(serial);
            });
        }
    }
}
