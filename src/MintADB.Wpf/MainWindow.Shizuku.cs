using System.Windows;
using System.Windows.Controls;
using MintADB.Wpf.Services;

namespace MintADB.Wpf;

public partial class MainWindow
{
    private ShizukuService? _shizuku;
    private ShizukuService Shizuku => _shizuku ??= new ShizukuService(_adb);

    private async void CheckShizuku_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        await RunToolAsync("Shizuku", async () =>
        {
            var status = await Shizuku.GetStatusAsync(serial);
            ShizukuStatusText.Text = status.SummaryText;
            AppendLog($"[Shizuku] {status.SummaryText}");
        });
    }

    private async void StartShizuku_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        if (MessageBox.Show(
                "Khởi động Shizuku qua ADB?\n\n"
                + "· Cần cài app Shizuku và mở một lần trên máy\n"
                + "· Sau khi khởi động, vào app Shizuku để xác nhận",
                "Shizuku", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        await RunToolAsync("Khởi động Shizuku", async () =>
        {
            var r = await Shizuku.StartAsync(serial);
            var status = await Shizuku.GetStatusAsync(serial);
            ShizukuStatusText.Text = status.SummaryText;

            if (r.Ok || status.Running)
            {
                AppendLog("[OK] Shizuku đã khởi động");
                AppendLog($"[Shizuku] {status.SummaryText}");
            }
            else
            {
                AppendLog($"[FAIL] {r.Combined}");
                AppendLog("Gợi ý: mở app Shizuku trên máy một lần rồi thử lại.");
            }
        });
    }

    private async void OpenShizuku_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        await RunToolAsync("Mở Shizuku", async () =>
        {
            var r = await Shizuku.LaunchManagerAsync(serial);
            AppendLog(r.Ok ? "[OK] Đã mở app Shizuku" : $"[FAIL] {r.Combined}");
        });
    }

    private async void GrantShizukuApi_Click(object sender, RoutedEventArgs e)
        => await RunShizukuPermissionAction(ShizukuService.ApiPermission, grant: true, "Cấp API Shizuku");

    private async void GrantAllShizukuPerms_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var pkgs = GetSelectedPackages();
        if (pkgs.Count == 0)
        {
            MessageBox.Show("Chọn app hoặc nhập package tùy chỉnh.", "MintADB");
            return;
        }

        if (MessageBox.Show(
                $"Cấp tất cả quyền Shizuku + AppOps cho {pkgs.Count} app?",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        SetActionButtonsEnabled(false);
        try
        {
            foreach (var pkg in pkgs)
            {
                AppendLog($"[Shizuku] Cấp quyền cho {pkg}...");
                var results = await Shizuku.GrantPrivilegedBundleAsync(serial, pkg);
                foreach (var (label, ok, detail) in results)
                    AppendLog(ok ? $"  [OK] {label}" : $"  [FAIL] {label}: {detail}");
            }
        }
        catch (Exception ex) { AppendLog($"[Shizuku] Lỗi: {ex.Message}"); }
        finally { SetActionButtonsEnabled(true); }
    }

    private async void GrantShizukuPerm_Click(object sender, RoutedEventArgs e)
        => await RunShizukuPermissionAction(GetSelectedShizukuPermission(), grant: true, "Grant");

    private async void RevokeShizukuPerm_Click(object sender, RoutedEventArgs e)
        => await RunShizukuPermissionAction(GetSelectedShizukuPermission(), grant: false, "Revoke");

    private async void GrantShizukuUsageStats_Click(object sender, RoutedEventArgs e)
        => await RunShizukuAppOpAction("android:get_usage_stats", "Thống kê sử dụng");

    private async void GrantShizukuOverlay_Click(object sender, RoutedEventArgs e)
        => await RunShizukuAppOpAction("android:system_alert_window", "Hiển thị trên app");

    private async void GrantShizukuBattery_Click(object sender, RoutedEventArgs e)
        => await RunShizukuAppOpAction("android:request_ignore_battery_optimizations", "Bỏ qua tối ưu pin");

    private async void GrantShizukuRunBackground_Click(object sender, RoutedEventArgs e)
        => await RunShizukuAppOpAction("RUN_IN_BACKGROUND", "Chạy nền");

    private string GetSelectedShizukuPermission()
    {
        var custom = GetBoxText(ShizukuPermCustomBox);
        if (!string.IsNullOrEmpty(custom))
            return custom;

        if (ShizukuPermPreset.SelectedItem is ComboBoxItem item && item.Tag is string tag && !string.IsNullOrWhiteSpace(tag))
            return tag;

        return ShizukuService.ApiPermission;
    }

    private async Task RunShizukuPermissionAction(string permission, bool grant, string label)
    {
        if (string.IsNullOrWhiteSpace(permission))
        {
            MessageBox.Show("Chọn hoặc nhập tên quyền.", "MintADB");
            return;
        }

        await RunOnSelectedPackages(label, async (serial, pkg) =>
        {
            var r = grant
                ? await Shizuku.GrantPermissionAsync(serial, pkg, permission)
                : await Shizuku.RevokePermissionAsync(serial, pkg, permission);
            AppendLog(r.Ok
                ? $"[OK] {(grant ? "grant" : "revoke")} {permission} @ {pkg}"
                : $"[FAIL] {pkg}: {r.Combined}");
        });
    }

    private async Task RunShizukuAppOpAction(string op, string label)
    {
        await RunOnSelectedPackages(label, async (serial, pkg) =>
        {
            var r = await Shizuku.SetAppOpAsync(serial, pkg, op, "allow");
            AppendLog(r.Ok ? $"[OK] {pkg} · {label}" : $"[FAIL] {pkg}: {r.Combined}");
        });
    }
}