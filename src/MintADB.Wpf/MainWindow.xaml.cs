using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MintADB.Wpf.Helpers;
using MintADB.Wpf.Models;
using MintADB.Wpf.Services;

namespace MintADB.Wpf;

public partial class MainWindow : Window
{
    private readonly AdbService _adb = new();
    private readonly AppScanService _appScan;
    private readonly XiaomiCnOptimizer _optimizer;
    private readonly ObservableCollection<InstalledApp> _apps = [];
    private readonly ObservableCollection<AdbDevice> _devices = [];
    private readonly CollectionViewSource _appViewSource;
    private readonly HashSet<string> _presetPackages = AppPreset.Defaults.Select(p => p.Package).ToHashSet(StringComparer.Ordinal);
    private readonly DispatcherTimer _monitor = new() { Interval = TimeSpan.FromSeconds(5) };
    private string? _selectedSerial;
    private bool _scanning;
    private bool _refreshingDevices;
    private bool _suppressDeviceSelection;
    private string _filterText = "";
    private AppCategory? _filterCategory;

    public MainWindow()
    {
        InitializeComponent();
        WindowRoundHelper.Attach(this, WindowRoundHelper.DefaultRadius);
        _appScan = new AppScanService(_adb);
        _optimizer = new XiaomiCnOptimizer(_adb);

        _appViewSource = new CollectionViewSource { Source = _apps };
        _appViewSource.GroupDescriptions.Add(new PropertyGroupDescription(nameof(InstalledApp.CategoryLabel)));
        _appViewSource.SortDescriptions.Add(new SortDescription(nameof(InstalledApp.CategorySortOrder), ListSortDirection.Ascending));
        _appViewSource.SortDescriptions.Add(new SortDescription(nameof(InstalledApp.Name), ListSortDirection.Ascending));
        _appViewSource.Filter += OnAppFilter;
        AppList.ItemsSource = _appViewSource.View;

        DeviceList.ItemsSource = _devices;
        InitCategoryFilter();
        InitInactiveAppList();

        InitToolAppsPanel();
        InitHintBox(CustomPackageBox);
        InitHintBox(AppSearchBox);
        InitHintBox(InactiveAppSearchBox);
        InitHintBox(ToolAppSearchBox);
        InitHintBox(PushLocalBox);
        InitHintBox(PushRemoteBox);
        InitHintBox(PullRemoteBox);
        InitHintBox(ListRemoteBox);
        InitHintBox(ScreenshotSaveBox);
        InitHintBox(RecordSaveBox);
        InitHintBox(RecordSecondsBox);
        ScreenshotSaveBox.Text = Path.Combine(AdbToolsService.MintAdbDir, "Screenshots");
        RecordSaveBox.Text = Path.Combine(AdbToolsService.MintAdbDir, "Recordings");
        InitHintBox(ShizukuPermCustomBox);
        InitHintBox(LocaleCustomBox);
        InitHintBox(FastbootImageBox);

        ShowToolPage(0);
        ShowSystemSubPage(0);
        InitBundledApkList();
        InitDeviceSpoofPanel();

        InitLogDocument();

        _adb.Shizuku = Shizuku;

        _monitor.Tick += async (_, _) => await RefreshDevicesAsync(silent: true);
        _monitor.Start();
        Loaded += async (_, _) => await RunStartupDriverCheckAsync();
    }

    private void InitLogDocument()
    {
        LogBox.Document = new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontFamily = LogBox.FontFamily,
            FontSize = LogBox.FontSize,
            Background = Brushes.Transparent,
            Foreground = BrushForLevel(LogLevel.Normal),
        };
        LogBox.Document.Blocks.Add(new Paragraph
        {
            Margin = new Thickness(0),
            LineHeight = 1.35,
            Foreground = BrushForLevel(LogLevel.Normal),
        });
    }

    private static void InitHintBox(TextBox box)
    {
        box.GotFocus += (_, _) =>
        {
            if (box.Tag is string hint && box.Text == hint) box.Text = "";
        };
        box.LostFocus += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(box.Text) && box.Tag is string hint) box.Text = hint;
        };
        if (box.Tag is string t) box.Text = t;
    }

    private void InitCategoryFilter()
    {
        CategoryFilter.Items.Add(new ComboItem(null, "Tất cả loại"));
        foreach (AppCategory cat in Enum.GetValues<AppCategory>().OrderBy(c => c.SortOrder()))
            CategoryFilter.Items.Add(new ComboItem(cat, cat.Label()));
        CategoryFilter.DisplayMemberPath = nameof(ComboItem.Label);
        CategoryFilter.SelectedIndex = 0;
    }

    private void AppendLog(string msg) => AppendLog(msg, LogClassifier.Classify(msg));

    private void AppendLog(string msg, LogLevel level)
    {
        _ = Dispatcher.InvokeAsync(() =>
        {
            var para = LogBox.Document.Blocks.LastBlock as Paragraph;
            if (para is null)
            {
                para = new Paragraph { Margin = new Thickness(0), LineHeight = 1.35 };
                LogBox.Document.Blocks.Add(para);
            }

            para.Inlines.Add(new Run(msg + Environment.NewLine) { Foreground = BrushForLevel(level) });
            LogBox.ScrollToEnd();
        }, DispatcherPriority.Background);
    }

    private static SolidColorBrush BrushForLevel(LogLevel level) => level switch
    {
        LogLevel.Success => new(Color.FromRgb(93, 214, 138)),   // #5DD68A
        LogLevel.Error => new(Color.FromRgb(255, 105, 97)),     // #FF6961
        LogLevel.Warning => new(Color.FromRgb(255, 208, 96)),   // #FFD060
        LogLevel.Running => new(Color.FromRgb(255, 191, 71)),   // #FFBF47
        LogLevel.Header => new(Color.FromRgb(126, 200, 255)),   // #7EC8FF
        LogLevel.Info => new(Color.FromRgb(174, 174, 178)),     // #AEAEB2
        _ => new(Color.FromRgb(229, 229, 234)),                // #E5E5EA — text thường
    };

    private void ClearLog()
    {
        Dispatcher.Invoke(() =>
        {
            LogBox.Document.Blocks.Clear();
            LogBox.Document.Blocks.Add(new Paragraph { Margin = new Thickness(0), LineHeight = 1.35 });
        });
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e) => ClearLog();

    private static SolidColorBrush BrushFromHex(string hex) =>
        new((Color)ColorConverter.ConvertFromString(hex)!);

    private void SetConnectionStatus(bool connected)
    {
        ConnectionDot.Fill = BrushFromHex(connected ? "#5DD68A" : "#636366");
        ConnectionLabel.Text = connected ? "Đã kết nối" : "Chưa kết nối";
        ConnectionLabel.Foreground = BrushFromHex(connected ? "#5DD68A" : "#AEAEB2");
    }

    private void UpdateDevicePanel(AdbDevice? device)
    {
        if (device is null)
        {
            DeviceModelText.Text = "—";
            DeviceSerialText.Text = "—";
            DeviceStateText.Text = "Chưa kết nối";
            DeviceStateBadge.Background = BrushFromHex("#3A3A3C");
            DeviceStateText.Foreground = BrushFromHex("#AEAEB2");
            return;
        }

        DeviceModelText.Text = device.ModelDisplay;
        DeviceSerialText.Text = device.Serial;
        DeviceStateText.Text = device.StateLabel;
        DeviceStateBadge.Background = BrushFromHex(device.StateBadgeBackground);
        DeviceStateText.Foreground = BrushFromHex(device.StateAccentColor);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            BtnMaximize_Click(sender, e);
        else
            DragMove();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private void BtnMinimize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void BtnMaximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        WindowRoundHelper.Apply(this, WindowRoundHelper.DefaultRadius);
    }

    private string? RequireDevice()
    {
        if (DeviceList.SelectedItem is AdbDevice device)
        {
            if (device.State == "unauthorized")
            {
                MessageBox.Show(
                    "Thiết bị chưa ủy quyền ADB.\n\n"
                    + "1. Rút/cắm lại cáp USB\n"
                    + "2. Trên máy: Cài đặt → Tùy chọn nhà phát triển → bật USB Debugging\n"
                    + "3. Chấp nhận popup «Cho phép gỡ lỗi USB» (tick Luôn cho phép)\n"
                    + "4. Bấm ↻ làm mới danh sách",
                    "MintADB", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            if (!device.IsOnline)
            {
                MessageBox.Show(
                    $"Thiết bị đang «{device.StateLabel}» — chờ trạng thái «Sẵn sàng» rồi thử lại.",
                    "MintADB", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            return device.Serial;
        }

        if (!string.IsNullOrEmpty(_selectedSerial))
            return _selectedSerial;

        MessageBox.Show("Chưa chọn thiết bị ADB.", "MintADB", MessageBoxButton.OK, MessageBoxImage.Warning);
        return null;
    }

    private void SetDeviceHint(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            DeviceHintText.Visibility = Visibility.Collapsed;
            DeviceHintText.Text = "";
            return;
        }

        DeviceHintText.Text = text;
        DeviceHintText.Visibility = Visibility.Visible;
    }

    private IEnumerable<AppPreset> GetSelectedApps()
    {
        foreach (var app in _apps.Where(a => a.Selected))
        {
            var preset = AppPreset.Defaults.FirstOrDefault(p => p.Package == app.Package);
            yield return preset ?? new AppPreset
            {
                Name = app.Name,
                Package = app.Package,
                Selected = true,
            };
        }

        var custom = CustomPackageBox.Text.Trim();
        if (CustomPackageBox.Tag is string hint && custom == hint) custom = "";
        if (!string.IsNullOrEmpty(custom))
        {
            if (AdbService.IsValidPackage(custom))
                yield return new AppPreset { Name = custom.Split('.')[^1], Package = custom, Selected = true };
            else
                AppendLog($"[WARN] Bỏ qua package không hợp lệ: {custom}");
        }
    }

    private void OnAppFilter(object sender, FilterEventArgs e)
    {
        if (e.Item is not InstalledApp app)
        {
            e.Accepted = false;
            return;
        }

        if (_filterCategory.HasValue && app.Category != _filterCategory.Value)
        {
            e.Accepted = false;
            return;
        }

        if (!string.IsNullOrWhiteSpace(_filterText))
        {
            e.Accepted = app.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase)
                         || app.Package.Contains(_filterText, StringComparison.OrdinalIgnoreCase);
            return;
        }

        e.Accepted = true;
    }

    private void RefreshAppView() => _appViewSource.View.Refresh();

    private IEnumerable<InstalledApp> GetVisibleApps() =>
        _appViewSource.View.Cast<object>().OfType<InstalledApp>();

    private void AppSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = AppSearchBox.Text.Trim();
        if (AppSearchBox.Tag is string hint && text == hint) text = "";
        _filterText = text;
        RefreshAppView();
    }

    private void CategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CategoryFilter.SelectedItem is ComboItem item)
            _filterCategory = item.Category;
        RefreshAppView();
    }

    private void UpdateDeviceListUi(bool hasDevices, string? emptyMessage = null)
    {
        DeviceList.Visibility = hasDevices ? Visibility.Visible : Visibility.Collapsed;
        if (!hasDevices && !string.IsNullOrWhiteSpace(emptyMessage))
            NoDeviceText.Text = emptyMessage;
        else if (!hasDevices)
            NoDeviceText.Text = "Không có thiết bị — cắm USB và bật USB Debugging";
        NoDeviceText.Visibility = hasDevices ? Visibility.Collapsed : Visibility.Visible;
    }

    private static bool DeviceListsEqual(IReadOnlyList<AdbDevice> current, IReadOnlyList<AdbDevice> incoming)
    {
        if (current.Count != incoming.Count) return false;
        for (var i = 0; i < current.Count; i++)
        {
            var a = current[i];
            var b = incoming[i];
            if (a.Serial != b.Serial || a.State != b.State || a.Model != b.Model || a.Product != b.Product)
                return false;
        }
        return true;
    }

    private async Task RefreshDevicesAsync(bool silent = false)
    {
        if (_refreshingDevices) return;
        _refreshingDevices = true;
        try
        {
            var devices = await _adb.ListDevicesAsync();
            var visible = devices
                .Where(d => d.State is "device" or "unauthorized" or "offline")
                .ToList();
            var online = visible.Where(d => d.IsOnline).ToList();
            var prev = _selectedSerial;
            AdbDevice? match = null;
            var listChanged = false;
            var needsApply = false;

            await Dispatcher.InvokeAsync(() =>
            {
                listChanged = visible.Count != _devices.Count
                              || !DeviceListsEqual([.. _devices], visible);

                if (listChanged)
                {
                    _devices.Clear();
                    foreach (var d in visible)
                        _devices.Add(d);
                }

                if (visible.Count == 0)
                {
                    _selectedSerial = null;
                    _suppressDeviceSelection = true;
                    DeviceList.SelectedItem = null;
                    _suppressDeviceSelection = false;
                    UpdateDevicePanel(null);
                    RomInfoText.Text = "Chưa quét";
                    SetConnectionStatus(false);
                    SetDeviceHint(null);
                    UpdateDeviceListUi(false);
                    ResetAppScanState();
                    return;
                }

                UpdateDeviceListUi(true);

                if (online.Count == 0)
                {
                    SetConnectionStatus(false);
                    SetDeviceHint(
                        "Thiết bị chưa sẵn sàng — chấp nhận popup «Cho phép gỡ lỗi USB» trên máy, rồi bấm ↻");
                }
                else
                    SetDeviceHint(null);

                match = online.FirstOrDefault(d => d.Serial == prev)
                        ?? visible.FirstOrDefault(d => d.Serial == prev)
                        ?? online.FirstOrDefault()
                        ?? visible[0];

                if (listChanged || DeviceList.SelectedItem is not AdbDevice selected || selected.Serial != match.Serial)
                {
                    _suppressDeviceSelection = true;
                    DeviceList.SelectedItem = match;
                    _suppressDeviceSelection = false;
                }

                needsApply = match.IsOnline
                             && (match.Serial != prev || string.IsNullOrEmpty(prev));
            });

            if (match is null) return;

            if (needsApply)
                await ApplyDeviceAsync(match, autoScanApps: true);
            else if (!match.IsOnline)
            {
                await Dispatcher.InvokeAsync(() => HandleNonOnlineDevice(match));
            }
            else
                _selectedSerial = match.Serial;
        }
        catch (Exception) when (silent) { }
        catch (Exception ex)
        {
            AppendLog($"[Thiết bị] Lỗi: {ex.Message}");
        }
        finally
        {
            _refreshingDevices = false;
        }
    }

    private void ResetAppScanState()
    {
        _apps.Clear();
        _inactiveApps.Clear();
        AppScanStatus.Text = "Kết nối thiết bị để quét toàn bộ app";
        InactiveAppScanStatus.Text = "Quét để xem app đã tắt, gỡ hoặc ẩn";
        RefreshAppView();
        RefreshInactiveAppView();
    }

    private async void RefreshDevices_Click(object sender, RoutedEventArgs e)
        => await RefreshDevicesAsync();

    private async void DeviceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressDeviceSelection) return;
        if (DeviceList.SelectedItem is not AdbDevice device) return;

        if (!device.IsOnline)
        {
            HandleNonOnlineDevice(device);
            return;
        }

        var isNewDevice = _selectedSerial != device.Serial;
        await ApplyDeviceAsync(device, autoScanApps: isNewDevice && !_scanning);
    }

    private void HandleNonOnlineDevice(AdbDevice device)
    {
        _selectedSerial = null;
        UpdateDevicePanel(device);
        SetConnectionStatus(false);
        RomInfoText.Text = device.State switch
        {
            "unauthorized" => "Chờ ủy quyền USB Debugging",
            "offline" => "Thiết bị offline — thử cắm lại USB",
            _ => "Chưa sẵn sàng",
        };

        SetDeviceHint(device.State switch
        {
            "unauthorized" =>
                "Chưa ủy quyền: trên máy bật USB Debugging và chấp nhận popup «Cho phép gỡ lỗi USB»",
            "offline" => "Offline: thử cáp USB khác, cổng khác, hoặc adb kill-server rồi ↻",
            _ => $"Trạng thái «{device.StateLabel}» — chờ «Sẵn sàng»",
        });
    }

    private async Task ApplyDeviceAsync(AdbDevice device, bool autoScanApps)
    {
        _selectedSerial = device.Serial;
        SetConnectionStatus(true);
        SetDeviceHint(null);

        UpdateDevicePanel(device);

        try
        {
            var rom = await _optimizer.DetectRomAsync(device.Serial);
            RomInfoText.Text = rom.Summary;
        }
        catch (Exception ex)
        {
            RomInfoText.Text = "Không đọc được ROM";
            AppendLog($"[ROM] Lỗi: {ex.Message}");
        }

        if (autoScanApps)
            await ScanAppsInternalAsync(auto: true);
    }

    private void AppList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source)
        {
            var el = source;
            while (el is not null)
            {
                if (el is Button) return;
                if (el is ListBoxItem) break;
                el = VisualTreeHelper.GetParent(el);
            }
        }

        if (e.OriginalSource is not DependencyObject hit) return;
        while (hit is not null and not ListBoxItem)
            hit = VisualTreeHelper.GetParent(hit);
        if (hit is null) return;

        if (hit is ListBoxItem { DataContext: InstalledApp app })
        {
            app.Selected = !app.Selected;
            e.Handled = true;
        }
    }

    private void SelectAllApps_Click(object sender, RoutedEventArgs e)
    {
        var visible = GetVisibleApps().ToList();
        foreach (var app in visible)
            app.Selected = true;
        AppScanStatus.Text = visible.Count > 0
            ? $"Đã chọn {visible.Count} app (theo bộ lọc hiện tại)"
            : "Không có app nào trong bộ lọc";
    }

    private void DeselectAllApps_Click(object sender, RoutedEventArgs e)
    {
        foreach (var app in _apps)
            app.Selected = false;
        AppScanStatus.Text = "Đã tắt chọn tất cả app";
    }

    private async void ScanApps_Click(object sender, RoutedEventArgs e)
        => await ScanAppsInternalAsync(auto: false);

    private async Task ScanAppsInternalAsync(bool auto)
    {
        var serial = _selectedSerial;
        if (serial is null)
        {
            await Dispatcher.InvokeAsync(() =>
                AppScanStatus.Text = "Chưa chọn thiết bị — chọn máy ở sidebar trước");
            return;
        }

        if (_scanning) return;
        _scanning = true;
        await Dispatcher.InvokeAsync(() =>
            AppScanStatus.Text = auto ? "Đang quét toàn bộ app..." : "Đang quét app trên máy...");
        EnterBusy();
        try
        {
            var scanned = await _appScan.ScanAllAsync(serial);
            var counts = scanned.GroupBy(a => a.Category).ToDictionary(g => g.Key, g => g.Count());

            await Dispatcher.InvokeAsync(() =>
            {
                _apps.Clear();
                foreach (var app in scanned)
                {
                    if (app.Category is AppCategory.PlayStore or AppCategory.UserInstalled
                        && _presetPackages.Contains(app.Package))
                        app.Selected = true;
                    _apps.Add(app);
                }
                RefreshAppView();
            });

            var summary = string.Join(" · ", Enum.GetValues<AppCategory>()
                .OrderBy(c => c.SortOrder())
                .Select(c => $"{c.Label()} {counts.GetValueOrDefault(c)}"));

            await Dispatcher.InvokeAsync(() =>
                AppScanStatus.Text = $"Quét xong: {scanned.Count} app — {summary}");

            AppendLog($"[Quét app] Tổng {scanned.Count} | Play Store {counts.GetValueOrDefault(AppCategory.PlayStore)} | User {counts.GetValueOrDefault(AppCategory.UserInstalled)} | Rác ROM {counts.GetValueOrDefault(AppCategory.RomBloat)} | Hệ thống {counts.GetValueOrDefault(AppCategory.System)}");
        }
        catch (Exception ex)
        {
            await Dispatcher.InvokeAsync(() =>
                AppScanStatus.Text = $"Lỗi quét app: {ex.Message}");
            AppendLog($"[Quét app] Lỗi: {ex.Message}");
        }
        finally
        {
            _scanning = false;
            ExitBusy();
        }
    }

    private async void ScanRom_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var rom = await _optimizer.DetectRomAsync(serial);
        RomInfoText.Text = $"{rom.Summary} | {rom.Build}";
        AppendLog($"ROM: {rom.Summary}");
        AppendLog($"Build: {rom.Build}");
    }

    private async void FixNotifications_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var apps = GetSelectedApps().ToList();
        if (apps.Count == 0)
        {
            MessageBox.Show("Chọn ít nhất 1 app.", "MintADB");
            return;
        }

        ClearLog();
        await RunWithBusyAsync(async () =>
        {
            if (OptGlobal.IsChecked == true)
                await _optimizer.ApplyGlobalRelaxAsync(serial, AppendLog);
            if (OptChina.IsChecked == true)
                await _optimizer.ApplyChinaUnlockAsync(serial, AppendLog);
            foreach (var app in apps)
                await _optimizer.FixAppNotificationsAsync(serial, app, AppendLog);
        });
    }

    private async void GrantPermissions_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var apps = GetSelectedApps().ToList();
        if (apps.Count == 0)
        {
            MessageBox.Show("Chọn ít nhất 1 app.", "MintADB");
            return;
        }

        await RunWithBusyAsync(async () =>
        {
            foreach (var app in apps)
                await _optimizer.GrantPermissionsAsync(serial, app.Package, AppendLog);
        });
    }

    private async void FullOptimize_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var apps = GetSelectedApps().ToList();
        if (apps.Count == 0)
        {
            MessageBox.Show("Chọn ít nhất 1 app.", "MintADB");
            return;
        }

        if (OptMiuiOpt.IsChecked == true)
        {
            var confirm = MessageBox.Show(
                "Tắt MIUI Optimization sẽ thay đổi hành vi hệ thống.\nCần reboot sau khi chạy.\nTiếp tục?",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;
        }

        ClearLog();
        await RunWithBusyAsync(async () =>
        {
            await _optimizer.FullOptimizeAsync(
                serial, apps,
                OptGlobal.IsChecked == true,
                OptChina.IsChecked == true,
                OptMiuiOpt.IsChecked == true,
                OptGrant.IsChecked == true,
                AppendLog);
            MessageBox.Show("Tối ưu hoàn tất! Xem log để biết chi tiết.", "MintADB");
        });
    }

    private void SetActionButtonsEnabled(bool enabled)
    {
        // Chặn double-click bằng IsHitTestVisible — không IsEnabled=false (tránh nháy nền trắng mọi tab)
        PanelOptimize.IsHitTestVisible = enabled;
        PanelTools.IsHitTestVisible = enabled;
        NavOptimize.IsHitTestVisible = enabled;
        NavTools.IsHitTestVisible = enabled;
    }

    private sealed record ComboItem(AppCategory? Category, string Label);
}