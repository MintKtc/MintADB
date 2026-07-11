using System.IO;
using System.Windows;
using MintADB.Wpf.Helpers;
using MintADB.Wpf.Models;
using MintADB.Wpf.Services;

namespace MintADB.Wpf;

public partial class MainWindow
{
    private UsbDriverCheckResult? _lastDriverCheck;
    private bool _startupDriverPromptShown;

    private async Task RunStartupDriverCheckAsync()
    {
        await RunInstallBootstrapIfNeededAsync();

        AppendLog("[Setup] Kiểm tra ADB/Fastboot và driver USB...");
        await RefreshPlatformToolsStatusAsync(runDriverCheck: true);
        await RefreshDevicesAsync();

        if (_lastDriverCheck is { NeedsAttention: true } check && !_startupDriverPromptShown)
        {
            _startupDriverPromptShown = true;
            PromptDriverFixIfNeeded(check, startup: true);
        }
    }

    private async Task RunInstallBootstrapIfNeededAsync()
    {
        var args = Environment.GetCommandLineArgs();
        if (!InstallBootstrapService.ShouldRun(args))
            return;

        AppendLog("[Cài đặt] Thiết lập lần đầu / sau cài đặt...");

        var bootstrap = new InstallBootstrapService(_adb);
        var result = await bootstrap.RunAsync(offerDriverInstall: true, AppendLog);

        ReloadPlatformTools();
        _lastDriverCheck = result.DriverCheck;

        var toolsLine = result.ToolsReady
            ? $"ADB/Fastboot OK ({result.AdbVersion})"
            : "ADB/Fastboot chưa sẵn sàng — xem log";

        var driverLine = result.DriverCheck?.Summary ?? "Chưa kiểm tra driver";

        var welcome = "MintADB đã chuẩn bị môi trường:\n\n"
                      + $"• {toolsLine}\n"
                      + $"• {driverLine}\n\n"
                      + "Trên điện thoại Xiaomi:\n"
                      + "1. Cài đặt → Giới thiệu → Nhấn 7 lần «Phiên bản MIUI»\n"
                      + "2. Cài đặt → Cài đặt bổ sung → Tùy chọn nhà phát triển → USB Debugging\n"
                      + "3. Cắm USB, chấp nhận popup «Cho phép gỡ lỗi USB»\n\n";

        if (result.DriverCheck is { NeedsAttention: true, BundlePresent: true })
        {
            welcome += "Cài driver USB Google ngay? (khuyến nghị lần đầu)";
            var answer = MessageBox.Show(
                welcome,
                "MintADB — Cài đặt hoàn tất",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (answer == MessageBoxResult.Yes)
                InstallUsbDriver_Click(this, new RoutedEventArgs());
        }
        else
        {
            MessageBox.Show(
                welcome + "Bấm ↻ ở sidebar nếu chưa thấy máy.",
                "MintADB — Cài đặt hoàn tất",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private async Task RefreshPlatformToolsStatusAsync(bool runDriverCheck = false)
    {
        var status = PlatformToolsLocator.GetStatus(_adb.AdbPath);
        var version = await _adb.GetVersionAsync();
        status = status with { AdbVersion = version };

        if (runDriverCheck)
            _lastDriverCheck = await UsbDriverService.CheckAsync(_adb);

        var toolLine = status.AdbFound
            ? $"ADB: {status.AdbPath}"
            : "ADB: không tìm thấy — bấm «Triển khai tools»";
        var fbLine = status.FastbootFound
            ? $"Fastboot: {status.FastbootPath}"
            : "Fastboot: không tìm thấy";
        var bundleLine = status.BundledToolsPresent
            ? "Bộ offline: có sẵn cạnh app"
            : "Bộ offline: thiếu thư mục PlatformTools";
        var verLine = string.IsNullOrWhiteSpace(version) ? "" : $"Phiên bản: {version}";

        PlatformToolsStatusText.Text = string.Join("\n", new[] { toolLine, fbLine, bundleLine, verLine }
            .Where(s => !string.IsNullOrEmpty(s)));

        UsbDriverStatusText.Text = FormatDriverStatusText(_lastDriverCheck, status.BundledDriverPresent);

        AdbPathText.Text = status.AdbFound
            ? $"adb · {Path.GetFileName(status.AdbPath)}"
            : "adb · chưa cấu hình";

        if (FastbootPathText is not null)
        {
            FastbootPathText.Text = status.FastbootFound
                ? $"fastboot · {Path.GetFileName(status.FastbootPath)}"
                : "fastboot · chưa cấu hình";
        }

        if (runDriverCheck && _lastDriverCheck is not null)
            LogDriverCheck(_lastDriverCheck);
    }

    private static string FormatDriverStatusText(UsbDriverCheckResult? check, bool bundledDriverPresent)
    {
        if (check is null)
        {
            return bundledDriverPresent
                ? $"Driver USB: có gói offline ({Path.GetFileName(PlatformToolsLocator.BundledDriverInf)}) — chưa kiểm tra"
                : "Driver USB: thiếu — cần thư mục Drivers\\usb_driver";
        }

        var prefix = check.Level switch
        {
            DriverCheckLevel.Ok => "✓",
            DriverCheckLevel.Warning => "⚠",
            _ => "✗",
        };

        var extra = check.ProblemDevices.Count > 0
            ? $"\nThiết bị lỗi: {string.Join(", ", check.ProblemDevices.Take(3))}"
            : "";

        return $"{prefix} {check.Summary}{extra}";
    }

    private void LogDriverCheck(UsbDriverCheckResult check)
    {
        var level = check.Level switch
        {
            DriverCheckLevel.Ok => LogLevel.Success,
            DriverCheckLevel.Warning => LogLevel.Warning,
            _ => LogLevel.Error,
        };

        AppendLog($"[Setup] {check.Summary}", level);

        foreach (var hit in check.InstalledDriverHits)
            AppendLog($"  · driver: {hit}", LogLevel.Info);

        foreach (var problem in check.ProblemDevices)
            AppendLog($"  · lỗi USB: {problem}", LogLevel.Warning);

        if (!check.AndroidDriverInstalled && check.BundlePresent)
            AppendLog("[Setup] Gợi ý: bấm «Cài driver USB» hoặc cập nhật driver thủ công trong Device Manager.", LogLevel.Info);
    }

    private void PromptDriverFixIfNeeded(UsbDriverCheckResult check, bool startup)
    {
        if (!check.NeedsAttention) return;

        var title = startup ? "Kiểm tra driver khi mở app" : "Kiểm tra driver USB";
        var body = check.Summary;
        if (check.ProblemDevices.Count > 0)
            body += "\n\nThiết bị cần xử lý:\n• " + string.Join("\n• ", check.ProblemDevices.Take(5));

        body += "\n\nCài driver Google USB (android_winusb) ngay?";

        var answer = MessageBox.Show(
            body,
            title,
            MessageBoxButton.YesNoCancel,
            check.Level == DriverCheckLevel.Error ? MessageBoxImage.Error : MessageBoxImage.Warning);

        if (answer == MessageBoxResult.Yes)
            InstallUsbDriver_Click(this, new RoutedEventArgs());
        else if (answer == MessageBoxResult.No)
            UsbDriverService.OpenDriverFolder();
    }

    private void ReloadPlatformTools()
    {
        _adb.ReloadPaths();
        _fastboot = null;
        _tools = null;
    }

    private async void InstallPlatformTools_Click(object sender, RoutedEventArgs e)
    {
        SetActionButtonsEnabled(false);
        try
        {
            var (ok, message) = await PlatformToolsLocator.DeployToolsToLocalAsync();
            AppendLog(ok ? $"[Setup] {message}" : $"[Setup] Lỗi: {message}");

            ReloadPlatformTools();
            await _adb.KillServerAsync();
            await _adb.StartServerAsync();
            await RefreshPlatformToolsStatusAsync();

            MessageBox.Show(
                ok
                    ? message + "\n\nĐã khởi động lại ADB server."
                    : message,
                "MintADB",
                MessageBoxButton.OK,
                ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            AppendLog($"[Setup] Lỗi triển khai: {ex.Message}");
            MessageBox.Show(ex.Message, "MintADB", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetActionButtonsEnabled(true);
        }
    }

    private async void InstallUsbDriver_Click(object sender, RoutedEventArgs e)
    {
        AppendLog("[Setup] Đang cài driver USB...");
        SetActionButtonsEnabled(false);
        try
        {
            var progress = new Progress<string>(msg =>
            {
                AppendLog($"[Setup] {msg}");
            });
            var (started, message) = await UsbDriverService.InstallDriverElevatedAsync(progress);
            if (started)
            {
                AppendLog($"[Setup] ✅ Cài driver thành công!");
                AppendLog(message);
            }
            else
            {
                AppendLog($"[Setup] ❌ Lỗi: {message}");
            }
        }
        finally
        {
            SetActionButtonsEnabled(true);
        }
    }

    private void OpenDriverFolder_Click(object sender, RoutedEventArgs e)
    {
        UsbDriverService.OpenDriverFolder();
        AppendLog($"[Setup] Mở thư mục driver: {UsbDriverService.DriverFolder}");
    }

    private void OpenDeviceManager_Click(object sender, RoutedEventArgs e)
    {
        UsbDriverService.OpenDeviceManager();
        AppendLog("[Setup] Mở Device Manager");
    }

    private async void RestartAdbServer_Click(object sender, RoutedEventArgs e)
    {
        SetActionButtonsEnabled(false);
        try
        {
            AppendLog("[Setup] Khởi động lại ADB server...");
            await _adb.KillServerAsync();
            var r = await _adb.StartServerAsync();
            AppendLog(r.Ok ? "[OK] ADB server đã khởi động" : $"[FAIL] {r.Combined}");
            await RefreshPlatformToolsStatusAsync(runDriverCheck: true);
            await RefreshDevicesAsync();
        }
        finally
        {
            SetActionButtonsEnabled(true);
        }
    }
}