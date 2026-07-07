using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MintADB.Wpf.Services;

namespace MintADB.Wpf;

public partial class MainWindow
{
    private MtkClientService? _mtkClient;
    private MtkClientService MtkClient => _mtkClient ??= new MtkClientService();

    private void ToolNavMtk_Click(object sender, RoutedEventArgs e)
    {
        ShowToolPage(8);
        UpdateMtkStatus();
    }

    // ── Setup ──

    private async void MtkUsbScan_Click(object sender, RoutedEventArgs e)
    {
        MtkUsbStatusText.Text = "Đang quét USB...";
        SetActionButtonsEnabled(false);
        try
        {
            var result = await MtkClient.ScanUsbDevicesAsync();
            MtkUsbStatusText.Text = result.Contains("BROM") || result.Contains("Download")
                ? "Phát hiện BROM!"
                : result.Contains("MTK") || result.Contains("MediaTek")
                    ? "Thiết bị MTK (không phải BROM)"
                    : "Không tìm thấy MTK";
            AppendLog(result);
            MtkLogText.Text = result;
        }
        finally
        {
            SetActionButtonsEnabled(true);
        }
    }

    private async void CheckMtkClient_Click(object sender, RoutedEventArgs e)
    {
        MtkClient.Locate();
        UpdateMtkStatus();
        if (MtkClient.IsAvailable)
        {
            var ver = await MtkClient.GetVersionAsync();
            MtkStatusText.Text = $"Sẵn sàng: {MtkClient.ResolvedPath} ({ver})";
            AppendLog($"[MTK] {MtkClient.GetSearchSummary()}");
        }
    }

    private async void CheckMtkEnv_Click(object sender, RoutedEventArgs e)
    {
        SetActionButtonsEnabled(false);
        try
        {
            AppendLog("===== KIỂM TRA MÔI TRƯỜNG MTK =====");
            MtkStatusText.Text = "Đang kiểm tra môi trường...";
            var report = await MtkClient.CheckEnvironmentAsync();
            MtkStatusText.Text = report.AllGood
                ? "Môi trường: OK"
                : "Môi trường: có vấn đề (xem log)";

            foreach (var line in report.ToString().Split('\n'))
                AppendLog(line.TrimEnd());

            if (!report.PythonFound)
            {
                AppendLog("[WARN] Python không tìm thấy — mtkclient cần Python");
                return;
            }
            if (!report.MtkFound)
            {
                AppendLog("[WARN] mtk client không tìm thấy — bấm «Tải bundled» hoặc «Cài đặt (pip)»");
                return;
            }
            if (!report.MtkWorks)
            {
                AppendLog($"[FAIL] mtk chạy thất bại: {report.MtkVersion}");
                return;
            }
            if (report.Deps.Count > 0 && report.Deps.Values.Any(v => !v))
            {
                var missing = report.Deps.Where(kv => !kv.Value).Select(kv => kv.Key);
                AppendLog($"[WARN] Thiếu thư viện: {string.Join(", ", missing)} — bấm «Dependencies»");
                return;
            }
            AppendLog("[OK] Môi trường sẵn sàng — có thể dùng mtkclient!");
        }
        finally
        {
            SetActionButtonsEnabled(true);
        }
    }

    private async void InstallMtkClient_Click(object sender, RoutedEventArgs e)
    {
        if (!MtkClient.IsAvailable && MtkClient.PythonPath is null)
        {
            var r = MessageBox.Show(
                "Chưa tìm thấy Python. Bạn muốn tải bundled mtkclient về thay vì dùng pip?",
                "MintADB", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r == MessageBoxResult.Yes)
            {
                await DownloadBundledAsync();
                return;
            }
            return;
        }

        MtkStatusText.Text = "Đang cài đặt mtkclient (pip)...";
        SetActionButtonsEnabled(false);
        try
        {
            var progress = new Progress<string>(msg =>
            {
                MtkStatusText.Text = msg;
                AppendLog($"[MTK] {msg}");
            });
            var result = await MtkClient.InstallAsync(progress);
            if (result == "OK")
            {
                MtkStatusText.Text = $"Cài đặt thành công!";
                UpdateMtkStatus();
            }
            else
            {
                MtkStatusText.Text = "Cài đặt thất bại";
                MessageBox.Show(result, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        finally
        {
            SetActionButtonsEnabled(true);
        }
    }

    private async void DownloadBundled_Click(object sender, RoutedEventArgs e)
        => await DownloadBundledAsync();

    private async void OfflineBundle_Click(object sender, RoutedEventArgs e)
    {
        MtkStatusText.Text = "Đang tạo offline bundle...";
        SetActionButtonsEnabled(false);
        try
        {
            var progress = new Progress<string>(msg =>
            {
                MtkStatusText.Text = msg;
                AppendLog($"[MTK] {msg}");
            });
            var result = await MtkClient.InstallOfflineBundleAsync(progress);
            if (result == "OK")
            {
                MtkStatusText.Text = "Offline bundle hoàn tất!";
                MtkDepsText.Text = "Thư viện: offline ready";
            }
            else
            {
                MtkStatusText.Text = "Offline bundle lỗi";
                MessageBox.Show(result, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            UpdateMtkStatus();
        }
        finally
        {
            SetActionButtonsEnabled(true);
        }
    }

    private async void InstallMtkDeps_Click(object sender, RoutedEventArgs e)
    {
        if (MtkClient.PythonPath is null)
        {
            MessageBox.Show("Chưa tìm thấy Python.", "MintADB");
            return;
        }
        MtkStatusText.Text = "Đang cài thư viện...";
        SetActionButtonsEnabled(false);
        try
        {
            var progress = new Progress<string>(msg =>
            {
                MtkStatusText.Text = msg;
                AppendLog($"[MTK] {msg}");
            });
            var result = await MtkClient.EnsureDependenciesAsync(progress);
            if (result == "OK")
            {
                MtkStatusText.Text = "Dependencies OK";
                MtkDepsText.Text = "Thư viện: đầy đủ";
            }
            else
            {
                MtkStatusText.Text = "Dependencies lỗi";
                MtkDepsText.Text = result;
            }
        }
        finally
        {
            SetActionButtonsEnabled(true);
        }
    }

    private async Task DownloadBundledAsync()
    {
        MtkStatusText.Text = "Đang tải mtkclient bundled...";
        SetActionButtonsEnabled(false);
        try
        {
            var progress = new Progress<string>(msg =>
            {
                MtkStatusText.Text = msg;
                AppendLog($"[MTK] {msg}");
            });
            var result = await MtkClient.InstallBundledAsync(progress);
            if (result == "OK")
            {
                MtkStatusText.Text = $"Đã tải bundled!";
                UpdateMtkStatus();
            }
            else
            {
                MtkStatusText.Text = "Tải bundled thất bại";
                MessageBox.Show(result, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        finally
        {
            SetActionButtonsEnabled(true);
        }
    }

    private void BrowseMtkClient_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Chọn mtk.exe hoặc mtk.py",
            Filter = "MTK Client|mtk.exe;mtk.py;mtk|Tất cả|*.*",
        };
        if (dlg.ShowDialog() == true)
        {
            MtkCustomPathBox.Text = dlg.FileName;
            MtkClient.SetCustomPath(dlg.FileName);
            UpdateMtkStatus();
        }
    }

    private void BrowsePythonPath_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Chọn python.exe",
            Filter = "Python|python.exe;python3.exe;python|Tất cả|*.*",
        };
        if (dlg.ShowDialog() == true)
        {
            MtkPythonPathBox.Text = dlg.FileName;
            var mtkPath = GetBoxText(MtkCustomPathBox);
            MtkClient.SetCustomPath(
                string.IsNullOrEmpty(mtkPath) ? "mtk" : mtkPath,
                dlg.FileName);
            UpdateMtkStatus();
        }
    }

    private void UpdateMtkStatus()
    {
        var available = MtkClient.IsAvailable;
        var python = MtkClient.PythonPath;

        MtkStatusText.Text = available
            ? $"Sẵn sàng: {MtkClient.ResolvedPath}"
            : "Chưa tìm thấy mtk client — bấm «Cài đặt (pip)» hoặc «Tải bundled»";

        MtkPythonDetectText.Text = python is not null
            ? $"Python: {python}"
            : "Python: không tìm thấy";

        MtkDepsText.Text = available && python is not null
            ? "Thư viện: chưa kiểm tra (bấm Dependencies)"
            : "";

        if (MtkCustomPathBox is not null)
            MtkCustomPathBox.Text ??= "";
        if (MtkPythonPathBox is not null)
            MtkPythonPathBox.Text = python ?? "";
    }

    // ── Device Info ──

    private async void MtkPrintInfo_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireMtkClient()) return;
        await RunMtkAsync("Thông tin MTK", async () =>
        {
            AppendLog("[MTK] Đang đọc thông tin thiết bị...");
            var r = await MtkClient.PrintInfoAsync();
            AppendLog(r.Ok ? r.Combined : $"[FAIL] {r.Combined}");
            MtkLogText.Text = r.Combined;
        });
    }

    private async void MtkPayload_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireMtkClient()) return;
        await RunMtkAsync("Payload", async () =>
        {
            AppendLog("[MTK] Đang gửi payload...");
            var r = await MtkClient.PayloadAsync();
            AppendLog(r.Ok ? "[OK] Payload thành công" : $"[FAIL] {r.Combined}");
            MtkLogText.Text = r.Combined;
        });
    }

    private async void MtkGetFlashInfo_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireMtkClient()) return;
        await RunMtkAsync("Flash info", async () =>
        {
            AppendLog("[MTK] Đang đọc thông tin flash...");
            var r = await MtkClient.GetFlashInfoAsync();
            AppendLog(r.Ok ? r.Combined : $"[FAIL] {r.Combined}");
            MtkLogText.Text = r.Combined;
        });
    }

    // ── Bootloader ──

    private async void MtkUnlockBootloader_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireMtkClient()) return;
        if (MessageBox.Show(
                "Mở khóa bootloader MTK?\n\n"
                + "· Dữ liệu sẽ bị xóa (wipe data)\n"
                + "· Không thể hoàn tác nếu không có backup\n"
                + "· Chỉ dùng cho máy MTK (MediaTek)",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        await RunMtkAsync("Unlock bootloader", async () =>
        {
            AppendLog("[MTK] Đang mở khóa bootloader (da seccfg unlock)...");
            var r = await MtkClient.UnlockBootloaderAsync();
            AppendLog(r.Ok ? "[OK] Bootloader đã mở khóa" : $"[FAIL] {r.Combined}");
            MtkLogText.Text = r.Combined;
        });
    }

    // ── Bypass FRP ──

    private async void MtkBypassFrp_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireMtkClient()) return;
        if (MessageBox.Show(
                "Bypass FRP MTK?\n\n"
                + "· Cần máy ở chế độ BROM (tắt nguồn, cắm USB)\n"
                + "· Có thể cần giữ Volume +/- khi cắm USB",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        await RunMtkAsync("Bypass FRP", async () =>
        {
            AppendLog("[MTK] Đang bypass FRP...");
            var r = await MtkClient.BypassFrpAsync();
            AppendLog(r.Ok ? "[OK] Bypass FRP thành công" : $"[FAIL] {r.Combined}");
            MtkLogText.Text = r.Combined;
        });
    }

    // ── Flash ──

    private async void MtkReadPartition_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireMtkClient()) return;
        var partition = GetBoxText(MtkPartitionBox);
        if (string.IsNullOrWhiteSpace(partition))
        {
            MessageBox.Show("Nhập tên partition (vd: boot, recovery, nvram).", "MintADB");
            return;
        }

        var dlg = new SaveFileDialog
        {
            Title = $"Lưu partition {partition}",
            FileName = $"{partition}.img",
            Filter = "IMG|*.img|Tất cả|*.*",
        };
        if (dlg.ShowDialog() != true) return;

        await RunMtkAsync($"Đọc {partition}", async () =>
        {
            AppendLog($"[MTK] Đang đọc partition {partition}...");
            var r = await MtkClient.ReadPartitionAsync(partition, dlg.FileName);
            AppendLog(r.Ok
                ? $"[OK] Đã lưu: {dlg.FileName}"
                : $"[FAIL] {r.Combined}");
            MtkLogText.Text = r.Combined;
        });
    }

    private async void MtkWritePartition_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireMtkClient()) return;
        var partition = GetBoxText(MtkPartitionBox);
        if (string.IsNullOrWhiteSpace(partition))
        {
            MessageBox.Show("Nhập tên partition (vd: boot, recovery).", "MintADB");
            return;
        }

        var dlg = new OpenFileDialog
        {
            Title = $"Chọn file cho partition {partition}",
            Filter = "IMG|*.img|Tất cả|*.*",
        };
        if (dlg.ShowDialog() != true) return;

        if (MessageBox.Show(
                $"Flash {partition} từ file?\n{Path.GetFileName(dlg.FileName)}\n\nHành động nguy hiểm!",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        await RunMtkAsync($"Flash {partition}", async () =>
        {
            AppendLog($"[MTK] Đang flash {partition}...");
            var r = await MtkClient.WritePartitionAsync(partition, dlg.FileName);
            AppendLog(r.Ok
                ? $"[OK] Flash {partition} thành công"
                : $"[FAIL] {r.Combined}");
            MtkLogText.Text = r.Combined;
        });
    }

    private async void MtkErasePartition_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireMtkClient()) return;
        var partition = GetBoxText(MtkPartitionBox);
        if (string.IsNullOrWhiteSpace(partition))
        {
            MessageBox.Show("Nhập tên partition.", "MintADB");
            return;
        }

        if (MessageBox.Show(
                $"Xóa partition {partition}?\n\nKhông thể hoàn tác!",
                "Cảnh báo", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        await RunMtkAsync($"Xóa {partition}", async () =>
        {
            AppendLog($"[MTK] Đang xóa {partition}...");
            var r = await MtkClient.ErasePartitionAsync(partition);
            AppendLog(r.Ok ? $"[OK] Đã xóa {partition}" : $"[FAIL] {r.Combined}");
            MtkLogText.Text = r.Combined;
        });
    }

    private async void MtkFormatFlash_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireMtkClient()) return;
        if (MessageBox.Show(
                "Format toàn bộ flash MTK?\n\n"
                + "· Xóa sạch tất cả dữ liệu\n"
                + "· Máy sẽ không boot được nếu chưa flash lại firmware\n"
                + "· Chỉ dùng khi thực sự cần thiết!",
                "Cảnh báo nguy hiểm", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        if (MessageBox.Show(
                "BẠN CHẮC CHẮN? Format flash sẽ làm sạch toàn bộ dữ liệu!",
                "Xác nhận lần cuối", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        await RunMtkAsync("Format flash", async () =>
        {
            AppendLog("[MTK] Đang format flash...");
            var r = await MtkClient.FormatFlashAsync();
            AppendLog(r.Ok ? "[OK] Format thành công" : $"[FAIL] {r.Combined}");
            MtkLogText.Text = r.Combined;
        });
    }

    private async void MtkReset_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireMtkClient()) return;
        await RunMtkAsync("Reset MTK", async () =>
        {
            AppendLog("[MTK] Đang reset thiết bị...");
            var r = await MtkClient.ResetDeviceAsync();
            AppendLog(r.Ok ? "[OK] Reset thành công" : $"[FAIL] {r.Combined}");
            MtkLogText.Text = r.Combined;
        });
    }

    private async void MtkCustomCommand_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireMtkClient()) return;
        var cmd = GetBoxText(MtkCustomCommandBox);
        if (string.IsNullOrWhiteSpace(cmd))
        {
            MessageBox.Show("Nhập lệnh mtk (vd: printinfo, gf, da seccfg unlock).", "MintADB");
            return;
        }

        await RunMtkAsync($"mtk {cmd}", async () =>
        {
            AppendLog($"[MTK] $ mtk {cmd}");
            var r = await MtkClient.CustomCommandAsync(cmd);
            AppendLog(r.Ok ? r.Combined : $"[FAIL] {r.Combined}");
            MtkLogText.Text = r.Combined;
        });
    }

    // ── Helpers ──

    private bool RequireMtkClient()
    {
        if (!MtkClient.IsAvailable)
        {
            MessageBox.Show(
                "Chưa tìm thấy mtk client.\n\n"
                + "1. Bấm «Cài đặt (pip)» để cài qua pip\n"
                + "2. Hoaëc bấm «Tải bundled» để tải từ GitHub\n"
                + "3. Hoaëc dùng mục «Đường dẫn tùy chỉnh» để trỏ thủ công",
                "MintADB", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        return true;
    }

    private async Task RunMtkAsync(string label, Func<Task> action)
    {
        SetActionButtonsEnabled(false);
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            AppendLog($"[MTK] Lỗi: {ex.Message}");
            MtkLogText.Text = $"Lỗi: {ex.Message}";
        }
        finally
        {
            SetActionButtonsEnabled(true);
        }
    }
}
