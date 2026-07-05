using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MintADB.Wpf.Models;
using MintADB.Wpf.Services;

namespace MintADB.Wpf;

public partial class MainWindow
{
    private readonly ObservableCollection<BundledApk> _bundledApks = [];

    private void InitBundledApkList()
    {
        BundledApkList.ItemsSource = _bundledApks;
        RefreshBundledApkList();
    }

    private void RefreshBundledApkList()
    {
        var dir = BundledApkService.ResolveMiuiDir();
        var items = BundledApkService.Scan(dir);

        _bundledApks.Clear();
        foreach (var apk in items)
            _bundledApks.Add(apk);

        BundledApkPhonePathText.Text = $"Trên máy: {BundledApkService.DeviceFolder}";

        if (items.Count == 0)
        {
            BundledApkSummaryText.Text = Directory.Exists(dir)
                ? "Chưa có APK trong thư mục Miui"
                : "Thư mục Miui chưa tồn tại";
            BundledApkDirText.Text = $"PC: {dir}";
            return;
        }

        var totalMb = items.Sum(a => a.SizeBytes) / (1024.0 * 1024.0);
        BundledApkSummaryText.Text = $"{items.Count} gói · {totalMb:F0} MB";
        BundledApkDirText.Text = $"PC: {dir}";
    }

    private void RefreshBundledApks_Click(object sender, RoutedEventArgs e)
        => RefreshBundledApkList();

    private async void PushBundledApk_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: BundledApk apk })
            return;

        var serial = RequireDevice();
        if (serial is null) return;

        BundledApkList.SelectedItem = null;
        await PushBundledApkAsync(serial, apk);
    }

    private async void InstallBundledApk_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: BundledApk apk })
            return;

        var serial = RequireDevice();
        if (serial is null) return;

        BundledApkList.SelectedItem = null;
        await InstallBundledApkAsync(serial, apk);
    }

    private async void PushAllBundledApks_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        if (_bundledApks.Count == 0)
        {
            MessageBox.Show("Không có APK trong thư mục Miui.", "MintADB");
            return;
        }

        if (MessageBox.Show(
                $"Đẩy {_bundledApks.Count} APK vào {BundledApkService.DeviceFolder}?",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        SetActionButtonsEnabled(false);
        try
        {
            foreach (var apk in _bundledApks)
                await PushBundledApkAsync(serial, apk, quiet: true);
            AppendLog($"[Miui] Mở Tệp → Download → MintADB trên máy để cài thủ công.");
        }
        finally { SetActionButtonsEnabled(true); }
    }

    private async void InstallAllBundledApks_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        if (_bundledApks.Count == 0)
        {
            MessageBox.Show("Không có APK trong thư mục Miui.", "MintADB");
            return;
        }

        if (MessageBox.Show(
                $"Cài {_bundledApks.Count} APK qua ADB?\n\nNếu MIUI chặn, dùng «Đẩy tất cả» rồi cài trên máy.",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        SetActionButtonsEnabled(false);
        try
        {
            foreach (var apk in _bundledApks)
                await InstallBundledApkAsync(serial, apk, quiet: true);
        }
        finally { SetActionButtonsEnabled(true); }
    }

    private async Task PushBundledApkAsync(string serial, BundledApk apk, bool quiet = false)
    {
        if (!File.Exists(apk.FullPath))
        {
            AppendLog($"[FAIL] Không tìm thấy: {apk.FileName}");
            return;
        }

        if (!quiet)
            SetActionButtonsEnabled(false);

        try
        {
            AppendLog($"[Miui] Đẩy {apk.DisplayName} → {apk.RemotePath}...");
            var r = await Tools.PushBundledApkAsync(serial, apk.FullPath);
            if (r.Ok)
                AppendLog($"[OK] {apk.DisplayName} · mở Tệp/Download/MintADB trên máy");
            else
                AppendLog($"[FAIL] {apk.DisplayName}: {r.Combined}");
        }
        catch (Exception ex)
        {
            AppendLog($"[FAIL] {apk.DisplayName}: {ex.Message}");
        }
        finally
        {
            if (!quiet)
                SetActionButtonsEnabled(true);
        }
    }

    private async Task InstallBundledApkAsync(string serial, BundledApk apk, bool quiet = false)
    {
        if (!File.Exists(apk.FullPath))
        {
            AppendLog($"[FAIL] Không tìm thấy: {apk.FileName}");
            return;
        }

        if (!quiet)
            SetActionButtonsEnabled(false);

        try
        {
            AppendLog($"[Miui] Cài {apk.DisplayName} ({apk.SizeText})...");
            var r = await Tools.InstallApkAsync(serial, apk.FullPath);
            AppendLog(r.Ok ? $"[OK] {apk.DisplayName}" : $"[FAIL] {apk.DisplayName}: {r.Combined}");
        }
        catch (Exception ex)
        {
            AppendLog($"[FAIL] {apk.DisplayName}: {ex.Message}");
        }
        finally
        {
            if (!quiet)
                SetActionButtonsEnabled(true);
        }
    }
}