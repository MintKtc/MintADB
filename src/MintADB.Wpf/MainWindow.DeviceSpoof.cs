using System.Windows;
using System.Windows.Controls;
using MintADB.Wpf.Helpers;
using MintADB.Wpf.Models;
using MintADB.Wpf.Resources;
using MintADB.Wpf.Services;

namespace MintADB.Wpf;

public partial class MainWindow
{
    private DeviceSpoofService? _deviceSpoof;

    private DeviceSpoofService DeviceSpoof => _deviceSpoof ??= new DeviceSpoofService(_adb, Shizuku);

    private void InitDeviceSpoofPanel()
    {
        DeviceSpoofPreset.Items.Clear();
        foreach (var profile in DeviceSpoofCatalog.Profiles)
        {
            DeviceSpoofPreset.Items.Add(new ComboBoxItem
            {
                Content = profile.DisplayName,
                Tag = profile.Id,
                ToolTip = $"{profile.Chip} — {profile.Description}",
            });
        }
        DeviceSpoofPreset.SelectedIndex = 0;
        UpdateDeviceSpoofProfileHint();
    }

    private DeviceSpoofProfile? GetSelectedSpoofProfile()
    {
        if (DeviceSpoofPreset.SelectedItem is not ComboBoxItem { Tag: string id })
            return null;
        return DeviceSpoofCatalog.Profiles.FirstOrDefault(p => p.Id == id);
    }

    private int GetSelectedTargetHz() => HzPresetHelper.FromComboIndex(DeviceSpoofHzPreset.SelectedIndex);

    private void DeviceSpoofPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => UpdateDeviceSpoofProfileHint();

    private void UpdateDeviceSpoofProfileHint()
    {
        var profile = GetSelectedSpoofProfile();
        DeviceSpoofProfileHint.Text = profile is null
            ? "Chọn profile flagship để game nhận máy cao cấp hơn."
            : $"{profile.Description}\nModel: {profile.Props.GetValueOrDefault("ro.product.model", "—")}";
    }

    private async void CheckDeviceSpoofCap_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        try
        {
            var cap = await DeviceSpoof.DetectCapabilitiesAsync(serial);
            DeviceSpoofCapText.Text = cap.Summary;
            AppendLog($"[Fake device] {cap.Summary}");

            if (cap.CanFakeProps)
                AppendLog("[OK] Có thể fake ro.product.* — nhớ cấp quyền SU cho shell/adb trong Magisk");
            else
            {
                AppendLog("[Gợi ý] Fake thiết bị cần Magisk + resetprop (root). Shizuku/ADB không đổi được ro.*");
                AppendLog("[Gợi ý] Chỉ cần unlock FPS? Bỏ tick «Fake thông tin» và giữ «Unlock FPS».");
            }

            if (!cap.ShizukuRunning && cap.ShizukuInstalled)
                AppendLog("[Gợi ý] Shizuku đã cài nhưng chưa chạy — khởi động Shizuku bên dưới để unlock FPS ổn định hơn.");
            else if (!cap.ShizukuInstalled)
                AppendLog("[Gợi ý] Unlock FPS dùng ADB; nếu lệnh settings thất bại, cài + khởi động Shizuku.");
        }
        catch (Exception ex)
        {
            DeviceSpoofCapText.Text = $"Lỗi: {ex.Message}";
        }
    }

    private async void ReadDeviceSpoofInfo_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        DeviceSpoofInfoText.Text = Loc.Get("ReadingStatus", "Reading…");
        try
        {
            var text = await DeviceSpoof.FormatCurrentDeviceAsync(serial);
            DeviceSpoofInfoText.Text = text;
            AppendLog("--- Thông tin thiết bị hiện tại ---");
            AppendLog(text);
        }
        catch (Exception ex)
        {
            DeviceSpoofInfoText.Text = $"Lỗi: {ex.Message}";
        }
    }

    private async void ApplyDeviceSpoof_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var profile = GetSelectedSpoofProfile();
        if (profile is null) return;

        var applyProps = DeviceSpoofApplyProps.IsChecked == true;
        var applyFps = DeviceSpoofApplyFps.IsChecked == true;
        if (!applyProps && !applyFps)
        {
            MessageBox.Show("Chọn ít nhất một tùy chọn: fake thiết bị hoặc unlock FPS.", "MintADB");
            return;
        }

        var cap = await DeviceSpoof.DetectCapabilitiesAsync(serial);
        DeviceSpoofCapText.Text = cap.Summary;

        if (applyProps && !cap.CanFakeProps)
        {
            MessageBox.Show(
                "Máy không có Magisk resetprop (root).\n\n"
                + "· Fake ro.product.* KHÔNG hoạt động chỉ với ADB/Shizuku\n"
                + "· Cần: Magisk + bật Zygisk + cấp SU cho shell\n"
                + "· Hoặc bỏ tick «Fake thông tin» và chỉ dùng Unlock FPS",
                "MintADB", MessageBoxButton.OK, MessageBoxImage.Warning);
            if (!applyFps) return;
            applyProps = false;
        }

        if (MessageBox.Show(
                $"Áp dụng {profile.DisplayName}?\n\n"
                + (applyProps ? "· Fake thông tin thiết bị (Magisk resetprop + reboot)\n" : "")
                + (applyFps ? $"· Unlock FPS {GetSelectedTargetHz()} Hz (ADB/Shizuku)\n" : "")
                + "\nBackup tự động trước khi đổi. Reboot để game nhận diện.",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        await RunWithBusyAsync(async () =>
        {
            if (applyProps)
            {
                var (ok, fail) = await DeviceSpoof.ApplyProfileAsync(serial, profile, cap, AppendLog);
                AppendLog($"[Fake device] {ok} OK · {fail} FAIL");
                if (fail > 0 && ok == 0)
                    AppendLog("[Gợi ý] Mở Magisk → Superuser → cấp quyền cho «Shell» hoặc «ADB» rồi thử lại");
            }

            if (applyFps)
            {
                var hz = GetSelectedTargetHz();
                var miui = DeviceSpoofDisableBooster.IsChecked == true;
                var (fpsOk, fpsFail) = await DisplayPerf.ApplyLockHzAsync(serial, hz, miui, AppendLog);
                AppendLog($"[FPS] {fpsOk} OK · {fpsFail} WARN");
            }
        });
    }

    private async void RestoreDeviceSpoof_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        if (MessageBox.Show(
                "Khôi phục thông tin thiết bị từ backup?\nCần reboot sau khi khôi phục.",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        await RunWithBusyAsync(async () =>
        {
            var cap = await DeviceSpoof.DetectCapabilitiesAsync(serial);
            var (ok, fail) = await DeviceSpoof.RestoreBackupAsync(serial, cap, AppendLog);
            AppendLog($"[Khôi phục] {ok} OK · {fail} FAIL");
            if (ok > 0)
                DeviceSpoofInfoText.Text = await DeviceSpoof.FormatCurrentDeviceAsync(serial);
        });
    }
}