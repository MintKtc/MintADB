using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MintADB.Wpf.Models;
using MintADB.Wpf.Resources;
using MintADB.Wpf.Services;

namespace MintADB.Wpf;

public partial class MainWindow
{
    private ProTweaksService? _pro;
    private HyperOsServicesService? _hyperSvc;
    private readonly ObservableCollection<HyperServiceRow> _hyperRows = [];

    private ProTweaksService Pro => _pro ??= new ProTweaksService(_adb, Tools);
    private HyperOsServicesService HyperSvc => _hyperSvc ??= new HyperOsServicesService(_adb, Tools);

    private void ToolNavPro_Click(object sender, RoutedEventArgs e)
    {
        ShowToolPage(6);
        if (HyperServiceList.ItemsSource is null)
            HyperServiceList.ItemsSource = _hyperRows;
    }

    private async void ProScanHyperServices_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        await RunToolAsync("HyperOS CN services scan", async () =>
        {
            var rom = await HyperSvc.DetectRomAsync(serial);
            AppendLog($"[ROM] {rom.Summary}");
            if (rom.IsChina)
                AppendLog("[CN] Using China package map (MSA CN, Baidu, GetApps market…)");

            _hyperRows.Clear();
            var statuses = await HyperSvc.ScanAllAsync(serial);
            foreach (var st in statuses)
            {
                // Hide empty groups on this device (cleaner list)
                if (st.Present == 0) continue;

                var title = Loc.Get(st.Group.TitleKey, st.Group.TitleFallback);
                var desc = Loc.Get(st.Group.DescKey, st.Group.DescFallback);
                _hyperRows.Add(new HyperServiceRow
                {
                    Id = st.Group.Id,
                    Title = title,
                    Description = desc,
                    NeedsWarning = st.Group.NeedsWarning,
                    Status = st.StatusLine,
                    Details = string.Join(" · ", st.DetailLines.Where(l => !l.EndsWith(": —")).Take(8)),
                });
            }

            var tag = rom.IsChina ? "China" : rom.Region;
            AppendLog($"[HyperOS] Scanned {_hyperRows.Count} groups · {tag}");
            HyperServiceStatusText.Text = rom.IsChina
                ? Loc.Format("HyperSvcScannedCn", "China ROM · {0} groups", _hyperRows.Count)
                : Loc.Format("HyperSvcScanned", "{0} groups", _hyperRows.Count);
        });
    }

    private async void ProDisableHyperGroup_Click(object sender, RoutedEventArgs e)
        => await SetHyperGroupAsync(enable: false);

    private async void ProEnableHyperGroup_Click(object sender, RoutedEventArgs e)
        => await SetHyperGroupAsync(enable: true);

    private async Task SetHyperGroupAsync(bool enable)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var row = HyperServiceList.SelectedItem as HyperServiceRow
                  ?? _hyperRows.FirstOrDefault(r => r.IsSelected);
        if (row is null)
        {
            MessageBox.Show(Loc.Get("HyperSvcSelect", "Select a service group in the list."), "MintADB");
            return;
        }

        if (!enable && row.NeedsWarning)
        {
            if (MessageBox.Show(
                    Loc.Get("HyperSvcCloudWarn",
                        "This group includes Mi Cloud services.\nFind device / cloud sync may stop working.\n\nContinue?"),
                    Loc.Get("Confirm", "Confirm"),
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
        }

        await RunToolAsync(enable ? "Enable services" : "Disable services", async () =>
        {
            var (ok, fail, skip) = await HyperSvc.SetGroupEnabledAsync(serial, row.Id, enable, AppendLog);
            AppendLog($"[{row.Title}] OK={ok} FAIL={fail} SKIP={skip}");
            // refresh one group
            var g = HyperOsServicesService.Groups.First(x => x.Id == row.Id);
            var st = await HyperSvc.ScanGroupAsync(serial, g);
            row.Status = st.StatusLine;
            row.Details = string.Join(" · ", st.DetailLines.Take(6));
        });
    }

    private async void ProDeviceLevel_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
                Loc.Get("HyperDeviceLevelConfirm",
                    "Set deviceLevelList to flagship-style (v:1,c:3,g:3)?\n\nMay change HyperOS UI animations. Reboot recommended."),
                Loc.Get("Confirm", "Confirm"),
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        await RunToolAsync("deviceLevelList", async () =>
        {
            var serial = RequireDevice()!;
            var (ok, detail) = await HyperSvc.ApplyDeviceLevelFlagshipAsync(serial, AppendLog);
            HyperDeviceLevelText.Text = detail;
            AppendLog(ok ? $"[OK] deviceLevelList={detail}" : $"[WARN] {detail}");
        });
    }

    private async void ProReadDeviceLevel_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;
        try
        {
            var v = await HyperSvc.ReadDeviceLevelAsync(serial);
            HyperDeviceLevelText.Text = v;
            AppendLog($"deviceLevelList={v}");
        }
        catch (Exception ex)
        {
            HyperDeviceLevelText.Text = ex.Message;
        }
    }

    private async void ProOpenAutostart_Click(object sender, RoutedEventArgs e)
        => await RunToolAsync("Open Autostart", async () =>
        {
            var r = await HyperSvc.OpenAutostartSettingsAsync(RequireDevice()!);
            AppendLog(r.Ok ? "[OK] Autostart" : $"[WARN] {r.Combined}");
        });

    private async void ProOpenAppBattery_Click(object sender, RoutedEventArgs e)
        => await RunToolAsync("Open App battery", async () =>
        {
            var r = await HyperSvc.OpenAppBatterySettingsAsync(RequireDevice()!);
            AppendLog(r.Ok ? "[OK] App battery" : $"[WARN] {r.Combined} — open Security manually");
        });

    private async void ProOpenSecurity_Click(object sender, RoutedEventArgs e)
        => await RunToolAsync("Open Security", async () =>
        {
            var r = await HyperSvc.OpenSecurityCenterAsync(RequireDevice()!);
            AppendLog(r.Ok ? "[OK] Security Center" : $"[WARN] {r.Combined}");
        });

    private async void ProGameMode_Click(object sender, RoutedEventArgs e)
        => await RunToolAsync("Game mode", async () =>
        {
            var pkg = GetProPackage();
            if (pkg is null) return;
            var mode = (ProGameModeCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "performance";
            var r = await HyperSvc.SetGameModeAsync(RequireDevice()!, pkg, mode);
            AppendLog(r.Ok ? $"[OK] game mode {mode} · {pkg}" : $"[WARN] {r.Combined}");
        });

    // ── Audit ──

    private async void ProAudit_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        ProScoreText.Text = "…";
        ProAuditText.Text = Loc.Get("ReadingStatus", "Reading…");
        try
        {
            var audit = await Pro.AuditAsync(serial);
            ProScoreText.Text = $"{audit.Score}";
            ProScoreText.Foreground = audit.Score >= 80
                ? new SolidColorBrush(Color.FromRgb(0x5D, 0xD6, 0x8A))
                : audit.Score >= 55
                    ? new SolidColorBrush(Color.FromRgb(0xFF, 0xBF, 0x47))
                    : new SolidColorBrush(Color.FromRgb(0xFF, 0x69, 0x61));
            ProAuditText.Text = audit.SummaryText;
            AppendLog("--- Pro · Audit ---");
            AppendLog(audit.SummaryText);
        }
        catch (Exception ex)
        {
            ProAuditText.Text = ex.Message;
            ProScoreText.Text = "—";
        }
    }

    // ── Full Pro Optimize ──

    private async void ProFullOptimize_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var battery = ProBiasBattery.IsChecked == true;
        var msg = battery
            ? Loc.Get("ProFullBatteryConfirm",
                "FULL PRO OPTIMIZE (Battery)\n\n· Global relax + analytics off\n· MIUI optimization off\n· Ad packages + AdGuard DNS\n· 60 Hz / light animations\n\nContinue?")
            : Loc.Get("ProFullSmoothConfirm",
                "FULL PRO OPTIMIZE (Smooth)\n\n· Global relax + analytics off\n· MIUI optimization off\n· Ad packages + AdGuard DNS\n· High Hz / performance mode\n\nContinue?");

        if (MessageBox.Show(msg, Loc.Get("Confirm", "Confirm"),
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        await RunToolAsync("Full Pro Optimize", async () =>
        {
            var (ok, fail) = await Pro.RunFullProOptimizeAsync(
                serial,
                preferBattery: battery,
                blockAds: ProFullAdsCheck.IsChecked == true,
                setDns: ProFullDnsCheck.IsChecked == true,
                disableMiuiOpt: ProFullMiuiOptCheck.IsChecked == true,
                AppendLog);
            AppendLog($"[Full Pro] {ok} OK · {fail} WARN");
            // refresh audit
            try
            {
                var audit = await Pro.AuditAsync(serial);
                ProScoreText.Text = $"{audit.Score}";
                ProAuditText.Text = audit.SummaryText;
            }
            catch { /* ignore */ }
        });
    }

    // ── Balanced debloat ──

    private async void ProBalancedDebloat_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
                Loc.Get("ProBalancedConfirm",
                    "BALANCED DEBLOAT\n\nDisable/uninstall ~30 common HyperOS bloat packages (ads, games, hybrid, analytics…).\nSafer than aggressive XDA lists.\n\nContinue?"),
                Loc.Get("Confirm", "Confirm"),
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        await RunToolAsync("Balanced debloat", async () =>
        {
            var serial = RequireDevice()!;
            var (ok, fail, skip) = await Pro.RunBalancedDebloatAsync(serial, AppendLog);
            AppendLog($"[Balanced] OK={ok} FAIL={fail} SKIP={skip}");
            MessageBox.Show(
                Loc.Format("ProBalancedDone", "Balanced debloat finished.\n\nOK: {0}\nFail: {1}\nSkip: {2}", ok, fail, skip),
                "MintADB");
        });
    }

    private async void ProSafeDebloat_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
                Loc.Get("ProDebloatConfirm", "Run Safe Debloat (Hyper built-in)?"),
                Loc.Get("Confirm", "Confirm"),
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        await RunToolAsync("Safe Debloat", async () =>
        {
            var serial = RequireDevice()!;
            var hyper = new HyperDebloatService(_adb, Tools);
            var (ok, fail, skip) = await hyper.RunAsync(serial, AppendLog);
            AppendLog($"[Safe] OK={ok} FAIL={fail} SKIP={skip}");
        });
    }

    // ── Quick presets ──

    private async void ProPerfPreset_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(Loc.Get("ProPerfConfirm", "Apply Performance preset?"),
                Loc.Get("Confirm", "Confirm"), MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            return;

        await RunToolAsync("Performance", async () =>
        {
            var (ok, fail) = await Pro.ApplyPerformancePresetAsync(RequireDevice()!, AppendLog);
            AppendLog($"[Perf] {ok}/{fail}");
        });
    }

    private async void ProBatteryPreset_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(Loc.Get("ProBatteryConfirm", "Apply Battery preset?"),
                Loc.Get("Confirm", "Confirm"), MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            return;

        await RunToolAsync("Battery", async () =>
        {
            var (ok, fail) = await Pro.ApplyBatteryPresetAsync(RequireDevice()!, AppendLog);
            AppendLog($"[Battery] {ok}/{fail}");
        });
    }

    // ── Per-app ──

    private async void ProNotifPack_Click(object sender, RoutedEventArgs e)
        => await RunToolAsync("Notification pack", async () =>
        {
            var pkg = GetProPackage();
            if (pkg is null) return;
            await Pro.ApplyNotificationProPackAsync(RequireDevice()!, pkg, AppendLog);
        });

    private async void ProUnrestrictedData_Click(object sender, RoutedEventArgs e)
        => await RunToolAsync("Unrestricted data", async () =>
        {
            var pkg = GetProPackage();
            if (pkg is null) return;
            await Pro.ApplyUnrestrictedDataAsync(RequireDevice()!, pkg, AppendLog);
        });

    // ── Wireless ──

    private async void ProTcpip_Click(object sender, RoutedEventArgs e)
        => await RunToolAsync("TCP/IP ADB", async () =>
        {
            var serial = RequireDevice()!;
            if (!int.TryParse(GetBoxText(ProAdbPortBox), out var port) || port is < 1024 or > 65535)
                port = 5555;
            var r = await Pro.EnableTcpipAsync(serial, port);
            AppendLog(r.Ok ? $"[OK] tcpip {port}" : $"[FAIL] {r.Combined}");
            var ip = await Pro.GetWlanIpAsync(serial);
            if (!string.IsNullOrEmpty(ip))
                ProWirelessHostBox.Text = $"{ip}:{port}";
        });

    private async void ProPair_Click(object sender, RoutedEventArgs e)
        => await RunToolAsync("Wireless pair", async () =>
        {
            var host = GetBoxText(ProWirelessHostBox);
            var code = GetBoxText(ProWirelessCodeBox);
            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(code))
            {
                MessageBox.Show(Loc.Get("ProWirelessNeedPair", "Enter host:port and code."), "MintADB");
                return;
            }

            var r = await Pro.PairWirelessAsync(host, code);
            AppendLog(r.Ok ? $"[OK] pair {host}" : $"[FAIL] {r.Combined}");
        });

    private async void ProConnect_Click(object sender, RoutedEventArgs e)
        => await RunToolAsync("Wireless connect", async () =>
        {
            var host = GetBoxText(ProWirelessHostBox);
            if (string.IsNullOrEmpty(host))
            {
                MessageBox.Show(Loc.Get("ProWirelessNeedHost", "Enter host:port."), "MintADB");
                return;
            }

            var r = await Pro.ConnectWirelessAsync(host);
            AppendLog(r.Ok ? $"[OK] connect {host}" : $"[FAIL] {r.Combined}");
            await RefreshDevicesAsync();
        });

    private async void ProDisconnect_Click(object sender, RoutedEventArgs e)
        => await RunToolAsync("Disconnect", async () =>
        {
            var host = GetBoxText(ProWirelessHostBox);
            await Pro.DisconnectWirelessAsync(string.IsNullOrEmpty(host) ? null : host);
            await RefreshDevicesAsync();
        });

    private string? GetProPackage()
    {
        var pkg = GetBoxText(ProPackageBox);
        if (string.IsNullOrEmpty(pkg))
        {
            var selected = _apps.FirstOrDefault(a => a.Selected)?.Package;
            if (!string.IsNullOrEmpty(selected)) return selected;
            MessageBox.Show(Loc.Get("ProNeedPackage", "Enter package or select app on Optimize."), "MintADB");
            return null;
        }

        if (!AdbService.IsValidPackage(pkg))
        {
            MessageBox.Show(Loc.Get("InvalidInput", "Invalid"), "MintADB");
            return null;
        }

        return pkg;
    }
}
