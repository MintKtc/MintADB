using System.Windows;
using System.Windows.Controls;
using MintADB.Wpf.Services;

namespace MintADB.Wpf;

public partial class MainWindow
{
    private AdBlockService? _adBlock;

    private AdBlockService AdBlock => _adBlock ??= new AdBlockService(_adb, Tools);

    private async void BlockMiuiAds_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        if (MessageBox.Show(
                "Tắt gói quảng cáo MIUI/HyperOS và các cài đặt quảng cáo?\n\n"
                + "Một số gói có thể ảnh hưởng feed MIUI — có thể khôi phục sau.",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        await RunAdBlockAsync("MIUI", async () =>
        {
            var ok = await AdBlock.BlockMiuiAdsAsync(serial, AppendLog);
            AdBlockStatusText.Text = $"MIUI: {ok} thao tác OK";
        });
    }

    private async void BlockGoogleAds_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        if (MessageBox.Show(
                "Tắt quảng cáo Google (Ad Services, cá nhân hóa quảng cáo)?\n\n"
                + "Không tắt Google Play Services — app vẫn dùng GMS bình thường.",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        await RunAdBlockAsync("Google", async () =>
        {
            var ok = await AdBlock.BlockGoogleAdsAsync(serial, AppendLog);
            AdBlockStatusText.Text = $"Google: {ok} thao tác OK";
        });
    }

    private async void BlockAllAds_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var useDns = AdBlockUseDns.IsChecked == true;
        var dns = useDns ? GetSelectedAdDns() : null;

        if (MessageBox.Show(
                "Chặn quảng cáo MIUI + Google"
                + (useDns ? $" + DNS ({dns})" : "")
                + "?\n\nKhuyến nghị reboot sau khi áp dụng.",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        await RunAdBlockAsync("Tất cả", async () =>
        {
            var ok = await AdBlock.BlockAllAdsAsync(serial, dns, AppendLog);
            AdBlockStatusText.Text = $"Đã chặn · {ok} thao tác OK · reboot nếu cần";
        });
    }

    private async void RestoreMiuiAds_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        await RunAdBlockAsync("Khôi phục MIUI", async () =>
        {
            var ok = await AdBlock.RestoreMiuiAdsAsync(serial, AppendLog);
            AdBlockStatusText.Text = $"Khôi phục MIUI: {ok} gói";
        });
    }

    private async void RestoreGoogleAds_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        await RunAdBlockAsync("Khôi phục Google", async () =>
        {
            var ok = await AdBlock.RestoreGoogleAdsAsync(serial, AppendLog);
            AdBlockStatusText.Text = $"Khôi phục Google: {ok} gói";
        });
    }

    private async void CheckAdBlockStatus_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        try
        {
            var status = await AdBlock.GetStatusAsync(serial);
            AdBlockStatusText.Text = status.Replace(Environment.NewLine, " · ", StringComparison.Ordinal);
            AppendLog("--- Trạng thái chặn quảng cáo ---");
            AppendLog(status);
        }
        catch (Exception ex)
        {
            AdBlockStatusText.Text = $"Lỗi: {ex.Message}";
        }
    }

    private async Task RunAdBlockAsync(string label, Func<Task> action)
    {
        await RunWithBusyAsync(async () =>
        {
            try
            {
                AppendLog($"[{label}] Bắt đầu...");
                await action();
            }
            catch (Exception ex)
            {
                AppendLog($"[FAIL] {label}: {ex.Message}");
            }
        });
    }
}