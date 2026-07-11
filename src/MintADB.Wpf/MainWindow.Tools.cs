using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MintADB.Wpf.Models;
using MintADB.Wpf.Services;

namespace MintADB.Wpf;

public partial class MainWindow
{
    private AdbToolsService? _tools;
    private HardwareInfoService? _hardware;
    private int _toolPage;
    private int _systemPage;

    private AdbToolsService Tools => _tools ??= new AdbToolsService(_adb);
    private HardwareInfoService Hardware => _hardware ??= new HardwareInfoService(_adb);

    private void NavOptimize_Click(object sender, RoutedEventArgs e) => ShowPage(optimize: true);

    private void NavTools_Click(object sender, RoutedEventArgs e) => ShowPage(optimize: false);

    private void ShowPage(bool optimize)
    {
        PanelOptimize.Visibility = optimize ? Visibility.Visible : Visibility.Collapsed;
        PanelTools.Visibility = optimize ? Visibility.Collapsed : Visibility.Visible;

        SetActiveTab(optimize ? 0 : 1, NavOptimize, NavTools);
    }

    private void ShowToolPage(int page)
    {
        _toolPage = page;
        ToolPageBasic.Visibility = page == 0 ? Visibility.Visible : Visibility.Collapsed;
        ToolPageApps.Visibility = page == 1 ? Visibility.Visible : Visibility.Collapsed;
        ToolPageScreen.Visibility = page == 2 ? Visibility.Visible : Visibility.Collapsed;
        ToolPageNetwork.Visibility = page == 3 ? Visibility.Visible : Visibility.Collapsed;
        ToolPageSystem.Visibility = page == 4 ? Visibility.Visible : Visibility.Collapsed;
        ToolPageTweaks.Visibility = page == 5 ? Visibility.Visible : Visibility.Collapsed;
        ToolPageAdvanced.Visibility = page == 6 ? Visibility.Visible : Visibility.Collapsed;
        ToolPageFastboot.Visibility = page == 7 ? Visibility.Visible : Visibility.Collapsed;

        SetActiveTab(page,
            ToolNavBasic, ToolNavApps, ToolNavScreen, ToolNavNetwork,
            ToolNavSystem, ToolNavTweaks, ToolNavAdvanced, ToolNavFastboot);

        if (page == 4)
            ShowSystemSubPage(_systemPage);

        if (page == 7)
            _ = RefreshFastbootDevicesAsync();
    }

    private void ShowSystemSubPage(int page)
    {
        _systemPage = page;
        SysPageBattery.Visibility = page == 0 ? Visibility.Visible : Visibility.Collapsed;
        SysPageDisplay.Visibility = page == 1 ? Visibility.Visible : Visibility.Collapsed;
        SysPageDevice.Visibility = page == 2 ? Visibility.Visible : Visibility.Collapsed;

        SetActiveTab(page, SysNavBattery, SysNavDisplay, SysNavDevice);
    }

    private static void SetActiveTab(int index, params Button[] tabs)
    {
        for (var i = 0; i < tabs.Length; i++)
            tabs[i].Tag = i == index ? "active" : "";
    }

    private static string GetBoxText(TextBox box)
    {
        var text = box.Text.Trim();
        if (box.Tag is string hint && text == hint) return "";
        return text;
    }

    private List<string> GetSelectedPackages()
    {
        var pkgs = _apps.Where(a => a.Selected).Select(a => a.Package).ToList();
        if (pkgs.Count > 0) return pkgs;

        var custom = CustomPackageBox.Text.Trim();
        if (CustomPackageBox.Tag is string hint && custom == hint) custom = "";
        if (!string.IsNullOrEmpty(custom)) pkgs.Add(custom);
        return pkgs;
    }

    private async Task RunToolAsync(string label, Func<Task> action)
    {
        if (RequireDevice() is null) return;
        await RunWithBusyAsync(async () =>
        {
            AppendLog($"[{label}] Đang chạy...");
            try { await action(); }
            catch (Exception ex) { AppendLog($"[{label}] Lỗi: {ex.Message}"); }
        });
    }

    private async void Reboot_Click(object sender, RoutedEventArgs e)
        => await RebootWithMode(RebootMode.Normal);

    private async void RebootRecovery_Click(object sender, RoutedEventArgs e)
        => await RebootWithMode(RebootMode.Recovery);

    private async void RebootBootloader_Click(object sender, RoutedEventArgs e)
        => await RebootWithMode(RebootMode.Bootloader);

    private async void RebootSideload_Click(object sender, RoutedEventArgs e)
        => await RebootWithMode(RebootMode.Sideload);

    private async void RebootEdl_Click(object sender, RoutedEventArgs e)
        => await RebootWithMode(RebootMode.Edl);

    private async Task RebootWithMode(RebootMode mode)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        if (mode != RebootMode.Normal)
        {
            var ok = MessageBox.Show(
                $"Reboot {mode.Label()}?\nThiết bị sẽ khởi động lại chế độ đặc biệt.",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (ok != MessageBoxResult.Yes) return;
        }

        await RunToolAsync($"Reboot {mode.Label()}", async () =>
        {
            var r = await Tools.RebootAsync(serial, mode);
            AppendLog(r.Ok ? $"[OK] {mode.Label()}" : $"[FAIL] {r.Combined}");
        });
    }

    private async void InstallApk_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var dlg = new OpenFileDialog
        {
            Title = "Chọn file APK",
            Filter = "APK (*.apk)|*.apk|Tất cả (*.*)|*.*",
        };
        if (dlg.ShowDialog() != true) return;

        await RunToolAsync("Cài APK", async () =>
        {
            AppendLog($"Cài {Path.GetFileName(dlg.FileName)}...");
            var r = await Tools.InstallApkAsync(serial, dlg.FileName);
            AppendLog(r.Ok ? "[OK] Cài APK thành công" : $"[FAIL] {r.Combined}");
        });
    }

    private async void RemoveOrDisable_Click(object sender, RoutedEventArgs e)
        => await RemovePackagesWithConfirm(GetSelectedPackages(), "Gỡ / tắt app đã chọn");

    private async void UninstallSystemShell_Click(object sender, RoutedEventArgs e)
    {
        // Ưu tiên app đã chọn; nếu không có thì lấy app Hệ thống / Rác ROM đã chọn trong list + package tùy chỉnh
        var selected = GetSelectedPackages();
        if (selected.Count == 0)
        {
            selected = _apps
                .Where(a => a.Selected && (a.IsSystem || a.Category is AppCategory.System or AppCategory.RomBloat))
                .Select(a => a.Package)
                .ToList();
        }

        if (selected.Count == 0)
        {
            MessageBox.Show(
                "Chọn app hệ thống / rác ROM trong danh sách, hoặc nhập package vào ô tùy chỉnh.\n\n"
                + "Lệnh shell: pm uninstall --user 0 <package>",
                "MintADB");
            return;
        }

        await RemovePackagesWithConfirm(
            selected,
            $"Gỡ hệ thống qua shell ({selected.Count} app)",
            shellOnly: true);
    }

    private async void UninstallAllBloat_Click(object sender, RoutedEventArgs e)
    {
        var bloat = _apps.Where(a => a.Category == AppCategory.RomBloat).Select(a => a.Package).ToList();
        if (bloat.Count == 0)
        {
            MessageBox.Show("Không có app rác ROM — quét app trước.", "MintADB");
            return;
        }

        await RemovePackagesWithConfirm(bloat, $"Gỡ tất cả {bloat.Count} app rác ROM", shellOnly: true);
    }

    private async void RemoveSingleApp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: InstalledApp app }) return;
        var serial = RequireDevice();
        if (serial is null) return;

        var isSystem = app.IsSystem || app.Category is AppCategory.System or AppCategory.RomBloat;
        var modeHint = isSystem
            ? "\n\nApp hệ thống → shell: pm uninstall --user 0"
            : "";

        if (MessageBox.Show(
                $"Gỡ / tắt {app.Name}?\n{app.Package}{modeHint}",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        await RunWithBusyAsync(async () =>
        {
            await RemovePackageAsync(serial, app.Package, shellOnly: isSystem);
            RecordRemoveAction(new List<string> { app.Package });
        });
    }

    private async Task RemovePackagesWithConfirm(
        IReadOnlyList<string> packages, string title, bool shellOnly = false)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        if (packages.Count == 0)
        {
            MessageBox.Show("Chọn ít nhất 1 app trong danh sách.", "MintADB");
            return;
        }

        var method = shellOnly
            ? "Chỉ dùng shell:\n"
              + "· pm uninstall --user 0 <package>\n"
              + "· cmd package uninstall --user 0\n"
              + "· (nếu có root) su -c pm uninstall\n\n"
              + "Không gỡ được → disable / ẩn.\n"
            : "App hệ thống: shell pm uninstall --user 0.\n"
              + "App user: adb uninstall.\n"
              + "App lõi không gỡ được sẽ tự disable/ẩn.\n";

        if (MessageBox.Show(
                $"{title} ({packages.Count} app)?\n\n"
                + method
                + "Không chặn gói nào — gỡ nhầm app lõi có thể bootloop.",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        await RunWithBusyAsync(async () =>
        {
            foreach (var pkg in packages)
                await RemovePackageAsync(serial, pkg, shellOnly);
            RecordRemoveAction(packages.ToList());
        });
    }

    private async Task RemovePackageAsync(string serial, string package, bool shellOnly = false)
    {
        if (!AdbService.IsValidPackage(package))
        {
            AppendLog($"[FAIL] Package không hợp lệ: {package}");
            return;
        }

        PackageRemoveResult r;
        if (shellOnly)
        {
            AppendLog($"[shell] pm uninstall --user 0 {package}");
            r = await Tools.UninstallViaShellAsync(serial, package, fallbackDisable: true);
        }
        else
        {
            r = await Tools.UninstallAsync(serial, package);
        }

        AppendLog(r.Outcome switch
        {
            PackageRemoveOutcome.Uninstalled => $"[OK] Đã gỡ {package}" + (shellOnly ? " (shell)" : ""),
            PackageRemoveOutcome.Disabled => $"[WARN] {package}: không gỡ được → đã vô hiệu hóa",
            PackageRemoveOutcome.Hidden => $"[WARN] {package}: không gỡ được → đã ẩn",
            _ => $"[FAIL] {package}: {r.Detail}",
        });

        if (r.Ok)
            RemoveAppFromList(package);
    }

    private void RemoveAppFromList(string package)
    {
        var existing = _apps.FirstOrDefault(a => a.Package == package);
        if (existing is null) return;
        _apps.Remove(existing);
        RefreshAppView();
    }

    private async void DisableApps_Click(object sender, RoutedEventArgs e)
    {
        var pkgs = GetSelectedPackages();
        await RunOnSelectedPackages("Tắt app", async (serial, pkg) =>
        {
            var r = await Tools.DisablePackageAsync(serial, pkg);
            if (r.Ok)
            {
                AppendLog($"[OK] Đã tắt {pkg}");
                RemoveAppFromList(pkg);
            }
            else AppendLog($"[FAIL] {pkg}: {r.Combined}");
        }, requireSelection: true);
        if (pkgs.Count > 0)
            RecordDisableAction(pkgs);
    }

    private async void ClearAppData_Click(object sender, RoutedEventArgs e)
        => await RunOnSelectedPackages("Xóa data", async (serial, pkg) =>
        {
            if (MessageBox.Show($"Xóa toàn bộ data của {pkg}?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            var r = await Tools.ClearAppDataAsync(serial, pkg);
            AppendLog(r.Ok ? $"[OK] Clear data {pkg}" : $"[FAIL] {pkg}: {r.Combined}");
        });

    private async void ForceStopApps_Click(object sender, RoutedEventArgs e)
        => await RunOnSelectedPackages("Force stop", async (serial, pkg) =>
        {
            var r = await Tools.ForceStopAsync(serial, pkg);
            AppendLog(r.Ok ? $"[OK] Stop {pkg}" : $"[FAIL] {pkg}: {r.Combined}");
        });

    private async void LaunchApp_Click(object sender, RoutedEventArgs e)
        => await RunOnSelectedPackages("Mở app", async (serial, pkg) =>
        {
            var r = await Tools.LaunchAppAsync(serial, pkg);
            AppendLog(r.Ok ? $"[OK] Launch {pkg}" : $"[FAIL] {pkg}: {r.Combined}");
        }, singleOnly: true);

    private async Task RunOnSelectedPackages(
        string label,
        Func<string, string, Task> action,
        bool requireSelection = false,
        bool singleOnly = false)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var pkgs = GetSelectedPackages();
        if (pkgs.Count == 0)
        {
            MessageBox.Show(requireSelection
                ? "Chọn ít nhất 1 app trong danh sách."
                : "Chọn app hoặc nhập package tùy chỉnh.",
                "MintADB");
            return;
        }

        if (singleOnly && pkgs.Count > 1)
        {
            MessageBox.Show("Chỉ chọn 1 app.", "MintADB");
            return;
        }

        SetActionButtonsEnabled(false);
        try
        {
            foreach (var pkg in pkgs)
                await action(serial, pkg);
        }
        finally { SetActionButtonsEnabled(true); }
    }

    private string ResolveSaveDir(TextBox box, string defaultSubfolder)
    {
        var text = GetBoxText(box);
        if (string.IsNullOrWhiteSpace(text))
            return Path.Combine(AdbToolsService.MintAdbDir, defaultSubfolder);
        return text.Trim().TrimEnd('\\', '/');
    }

    private static bool TryPickFolder(string? initialDir, out string? folder)
    {
        folder = null;
        var dlg = new OpenFolderDialog
        {
            Title = "Chọn thư mục lưu",
            InitialDirectory = !string.IsNullOrWhiteSpace(initialDir) && Directory.Exists(initialDir)
                ? initialDir
                : Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        };
        if (dlg.ShowDialog() != true) return false;
        folder = dlg.FolderName;
        return true;
    }

    private void BrowseScreenshotDir_Click(object sender, RoutedEventArgs e)
    {
        if (TryPickFolder(ResolveSaveDir(ScreenshotSaveBox, "Screenshots"), out var dir) && dir is not null)
            ScreenshotSaveBox.Text = dir;
    }

    private void BrowseRecordDir_Click(object sender, RoutedEventArgs e)
    {
        if (TryPickFolder(ResolveSaveDir(RecordSaveBox, "Recordings"), out var dir) && dir is not null)
            RecordSaveBox.Text = dir;
    }

    private async void Screenshot_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var saveDir = ResolveSaveDir(ScreenshotSaveBox, "Screenshots");

        await RunToolAsync("Screenshot", async () =>
        {
            var path = await Tools.CaptureScreenshotAsync(serial, saveDir);
            if (path is not null)
            {
                AppendLog($"[OK] Đã lưu: {path}");
                MessageBox.Show($"Đã lưu ảnh màn hình:\n{path}", "Screenshot");
            }
            else AppendLog("[FAIL] Không chụp được màn hình");
        });
    }

    private void Scrcpy_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var maxSize = ScrcpyMaxSize.SelectedIndex switch { 0 => 720, 2 => 1440, _ => 1080 };
        var stayAwake = ScrcpyStayAwake.IsChecked == true;

        if (Tools.TryLaunchScrcpy(serial, maxSize, stayAwake, out var msg))
            AppendLog($"[OK] {msg}");
        else
        {
            AppendLog($"[FAIL] {msg}");
            MessageBox.Show(msg, "Scrcpy", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ── File Explorer ──

    private async void ListRemoteDir_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var path = GetBoxText(ListRemoteBox);
        if (string.IsNullOrEmpty(path)) path = "/sdcard";

        await RunToolAsync("File Explorer", async () =>
        {
            var r = await Tools.ListRemoteAsync(serial, path);
            AppendLog($"--- {path} ---");
            AppendLog(string.IsNullOrWhiteSpace(r.Combined) ? "(trống)" : r.Combined);
        });
    }

    // ── ADB Version ──

    private async void CheckAdbVersion_Click(object sender, RoutedEventArgs e)
    {
        await RunToolAsync("ADB Version", async () =>
        {
            var adbVer = await Tools.GetAdbVersionAsync();
            var fbVer = await Tools.GetFastbootVersionAsync();
            AdbVersionText.Text = $"ADB: {adbVer}\nFastboot: {fbVer}";
            AppendLog($"[ADB] {adbVer}");
            AppendLog($"[Fastboot] {fbVer}");
        });
    }

    // ── Batch Operations ──

    private async void BatchClearData_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var pkgs = GetSelectedPackages();
        if (pkgs.Count == 0)
        {
            MessageBox.Show("Chọn ít nhất 1 app.", "MintADB");
            return;
        }

        if (MessageBox.Show(
                $"Xóa data {pkgs.Count} app đã chọn?",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        await RunToolAsync("Batch Clear Data", async () =>
        {
            var r = await Tools.ClearAppDataBatchAsync(serial, pkgs);
            AppendLog(r.Output);
        });
    }
}