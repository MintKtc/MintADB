using System.Windows;
using System.Windows.Controls;
using MintADB.Wpf.Helpers;
using MintADB.Wpf.Services;

namespace MintADB.Wpf;

public partial class MainWindow
{
    private DisplayPerformanceService? _displayPerf;
    private DisplayPerformanceService DisplayPerf => _displayPerf ??= new DisplayPerformanceService(_adb);

    private int GetHzLockTarget() => HzPresetHelper.FromComboIndex(HzLockPreset.SelectedIndex);

    private async void ReadHzLockStatus_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        HzLockStatusText.Text = "Đang đọc...";
        try
        {
            var text = await DisplayPerf.ReadHzStatusAsync(serial);
            HzLockStatusText.Text = text;
            AppendLog("--- Trạng thái Hz ---");
            AppendLog(text);
        }
        catch (Exception ex)
        {
            HzLockStatusText.Text = $"Lỗi: {ex.Message}";
        }
    }

    private async void ApplyHzLock_Click(object sender, RoutedEventArgs e)
        => await ApplyHzLockInternal(smoothOnly: false, uiOnly: false);

    private async void ApplySmoothUi_Click(object sender, RoutedEventArgs e)
        => await ApplyHzLockInternal(smoothOnly: false, uiOnly: true);

    private async void ApplyHzLockFull_Click(object sender, RoutedEventArgs e)
        => await ApplyHzLockInternal(smoothOnly: true, uiOnly: false);

    private async void RestoreAdaptiveHz_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        if (MessageBox.Show(
                "Khôi phục chế độ Hz thích ứng (Adaptive)?\nPin sẽ tiết kiệm hơn nhưng UI có thể giật hơn.",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        await RunWithBusyAsync(async () =>
        {
            var (ok, fail) = await DisplayPerf.RestoreAdaptiveAsync(serial, AppendLog);
            AppendLog($"[Khôi phục Hz] {ok} OK · {fail} WARN");
            HzLockStatusText.Text = await DisplayPerf.ReadHzStatusAsync(serial);
        });
    }

    private async Task ApplyHzLockInternal(bool smoothOnly, bool uiOnly)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var hz = GetHzLockTarget();
        var miui = HzLockMiuiBoost.IsChecked == true;
        var smooth = HzLockSmoothUi.IsChecked == true;
        var gpu = HzLockGpuBoost.IsChecked == true;

        if (!uiOnly && MessageBox.Show(
                smoothOnly
                    ? $"Khóa {hz} Hz + tối ưu UI mượt?\n\n· min = max = {hz} Hz\n· Tắt adaptive refresh\n· Tốn pin hơn"
                    : $"Khóa tần số quét {hz} Hz?\n\nUI và game giữ {hz} Hz cố định — tốn pin hơn.",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        await RunWithBusyAsync(async () =>
        {
            int ok, fail;
            if (uiOnly)
            {
                (ok, fail) = await DisplayPerf.ApplySmoothUiAsync(serial, gpu, AppendLog);
                AppendLog($"[UI mượt] {ok} OK · {fail} WARN");
            }
            else if (smoothOnly && smooth)
            {
                (ok, fail) = await DisplayPerf.ApplyFullAsync(serial, hz, miui, smooth, gpu, AppendLog);
                AppendLog($"[Lock Hz + UI] {ok} OK · {fail} WARN");
            }
            else
            {
                (ok, fail) = await DisplayPerf.ApplyLockHzAsync(serial, hz, miui, AppendLog);
                AppendLog($"[Lock Hz] {ok} OK · {fail} WARN");
            }

            HzLockStatusText.Text = await DisplayPerf.ReadHzStatusAsync(serial);

            if (DisplayHzBig is not null)
            {
                var info = await Hardware.GetDisplayInfoAsync(serial);
                UpdateDisplayVisual(info);
            }
        });
    }
}