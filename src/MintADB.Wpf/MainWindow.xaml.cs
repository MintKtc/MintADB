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
using MintADB.Wpf.Resources;
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
    private bool _suppressLanguageEvent;
    private readonly List<TextBox> _hintBoxes = [];

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
        InitLanguageSelector();
        LanguageManager.LanguageChanged += OnLanguageChanged;
        Closed += (_, _) => LanguageManager.LanguageChanged -= OnLanguageChanged;
        InitCategoryFilter();
        InitInactiveAppList();

        // Hiển thị Welcome popup lần đầu
        Loaded += (_, _) =>
        {
            if (Windows.WelcomeWindow.ShouldShow())
            {
                var welcome = new Windows.WelcomeWindow();
                welcome.ShowDialog();
            }
        };

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

    private void InitHintBox(TextBox box)
    {
        if (!_hintBoxes.Contains(box))
            _hintBoxes.Add(box);

        box.GotFocus += (_, _) =>
        {
            var hint = ResolveHint(box);
            if (hint.Length > 0 && box.Text == hint) box.Text = "";
        };
        box.LostFocus += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(box.Text))
            {
                var hint = ResolveHint(box);
                if (hint.Length > 0) box.Text = hint;
            }
        };
        ApplyHintText(box, force: true);
    }

    private static string ResolveHint(TextBox box)
    {
        // Tag có thể là string resource key đã resolve (DynamicResource gán string)
        if (box.Tag is string s && s.Length > 0)
            return s;
        return "";
    }

    private static void ApplyHintText(TextBox box, bool force)
    {
        var hint = ResolveHint(box);
        if (hint.Length == 0) return;
        if (force || string.IsNullOrWhiteSpace(box.Text))
            box.Text = hint;
    }

    private void InitCategoryFilter()
    {
        var selected = CategoryFilter.SelectedItem as ComboItem;
        CategoryFilter.Items.Clear();
        CategoryFilter.Items.Add(new ComboItem(null, Loc.Get("AllCategories", "Tất cả loại")));
        foreach (AppCategory cat in Enum.GetValues<AppCategory>().OrderBy(c => c.SortOrder()))
            CategoryFilter.Items.Add(new ComboItem(cat, cat.Label()));
        CategoryFilter.DisplayMemberPath = nameof(ComboItem.Label);

        if (selected is not null)
        {
            var match = CategoryFilter.Items.Cast<ComboItem>()
                .FirstOrDefault(i => Equals(i.Category, selected.Category));
            CategoryFilter.SelectedItem = match ?? CategoryFilter.Items[0];
        }
        else
            CategoryFilter.SelectedIndex = 0;
    }

    private void InitLanguageSelector()
    {
        _suppressLanguageEvent = true;
        try
        {
            var lang = LanguageManager.CurrentLanguage;
            foreach (var obj in LanguageSelector.Items)
            {
                if (obj is ComboBoxItem item && item.Tag is string tag
                    && string.Equals(tag, lang, StringComparison.OrdinalIgnoreCase))
                {
                    LanguageSelector.SelectedItem = item;
                    break;
                }
            }
        }
        finally
        {
            _suppressLanguageEvent = false;
        }
    }

    private void OnLanguageChanged(string lang)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => OnLanguageChanged(lang));
            return;
        }

        InitLanguageSelector();
        InitCategoryFilter();
        try { RebuildInactiveStateFilter(); } catch { /* panel chưa init */ }

        // Nhãn sidebar / trạng thái gán từ code
        var connected = _selectedSerial is not null
                        && _devices.Any(d => d.Serial == _selectedSerial && d.IsOnline);
        ConnectionLabel.Text = connected ? Loc.Get("Connected", Strings.Connected) : Loc.Get("Disconnected", Strings.Disconnected);
        if (!connected)
            DeviceStateText.Text = Loc.Get("Disconnected", Strings.Disconnected);

        if (string.IsNullOrWhiteSpace(RomInfoText.Text)
            || RomInfoText.Text is "Chưa quét" or "Not scanned" or "Not Scanned")
            RomInfoText.Text = Loc.Get("NotScanned", Strings.NotScanned);

        if (_apps.Count == 0)
            AppScanStatus.Text = Loc.Get("NoDeviceHint", Strings.NoDeviceHint);

        // Hint TextBox: Tag (DynamicResource) đã đổi → cập nhật Text nếu đang hiện hint
        foreach (var box in _hintBoxes)
        {
            // Force re-read Tag from resource: re-set Tag via key if needed
            // Tag binding DynamicResource already updated value
            var hint = ResolveHint(box);
            if (hint.Length == 0) continue;
            // Nếu text rỗng hoặc text là hint ngôn ngữ cũ (không khớp hint mới) → gán hint mới
            if (string.IsNullOrWhiteSpace(box.Text) || IsLikelyOldHint(box.Text, hint))
                box.Text = hint;
        }

        RefreshAppView();
        try { RefreshInactiveAppView(); } catch { /* ok */ }
        try { RefreshToolAppView(); } catch { /* ok */ }
    }

    private static bool IsLikelyOldHint(string text, string newHint)
    {
        if (text == newHint) return false;
        // Các hint placeholder thường kết thúc ... hoặc chứa từ khóa tìm
        return text.Contains("...", StringComparison.Ordinal)
               || text.Contains("Tìm", StringComparison.OrdinalIgnoreCase)
               || text.Contains("Search", StringComparison.OrdinalIgnoreCase)
               || text.Contains("Package", StringComparison.OrdinalIgnoreCase)
               || text.Contains("vd:", StringComparison.OrdinalIgnoreCase)
               || text.Contains("e.g.", StringComparison.OrdinalIgnoreCase);
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
        ConnectionLabel.Text = connected ? Strings.Connected : Strings.Disconnected;
        ConnectionLabel.Foreground = BrushFromHex(connected ? "#5DD68A" : "#AEAEB2");
    }

    private void UpdateDevicePanel(AdbDevice? device)
    {
        if (device is null)
        {
            DeviceModelText.Text = "—";
            DeviceSerialText.Text = "—";
            DeviceStateText.Text = Strings.Disconnected;
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
                    Strings.UnauthorizedMessage,
                    "MintADB", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            if (!device.IsOnline)
            {
                MessageBox.Show(
                    $"Device is «{device.StateLabel}» — wait for «Ready» then try again.",
                    "MintADB", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            return device.Serial;
        }

        if (!string.IsNullOrEmpty(_selectedSerial))
            return _selectedSerial;

        MessageBox.Show(Strings.NoDeviceSelected, "MintADB", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            NoDeviceText.Text = Strings.NoDeviceHint;
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
                    RomInfoText.Text = Strings.NotScanned;
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
                        Strings.DeviceNotReadyHint);
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
            AppendLog($"[Device] Error: {ex.Message}");
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
        AppScanStatus.Text = Strings.NoDeviceHint;
        InactiveAppScanStatus.Text = Strings.Scanning;
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
            "unauthorized" => "Waiting for USB Debugging authorization",
            "offline" => "Device offline — try reconnecting USB",
            _ => "Not ready",
        };

        SetDeviceHint(device.State switch
        {
            "unauthorized" =>
                "Unauthorized: enable USB Debugging on device and accept the popup",
            "offline" => "Offline: try different USB cable, port, or adb kill-server then ↻",
            _ => $"Status «{device.StateLabel}» — wait for «Ready»",
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
            RomInfoText.Text = "Cannot read ROM";
            AppendLog($"[ROM] Error: {ex.Message}");
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
            ? $"Selected {visible.Count} apps (filtered)"
            : "No apps in filter";
    }

    private void DeselectAllApps_Click(object sender, RoutedEventArgs e)
    {
        foreach (var app in _apps)
            app.Selected = false;
        AppScanStatus.Text = "All apps deselected";
    }

    private async void ScanApps_Click(object sender, RoutedEventArgs e)
        => await ScanAppsInternalAsync(auto: false);

    private async Task ScanAppsInternalAsync(bool auto)
    {
        var serial = _selectedSerial;
        if (serial is null)
        {
            await Dispatcher.InvokeAsync(() =>
                AppScanStatus.Text = "No device selected — select device first");
            return;
        }

        if (_scanning) return;
        _scanning = true;
        await Dispatcher.InvokeAsync(() =>
            AppScanStatus.Text = auto ? "Scanning all apps..." : "Scanning apps on device...");
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
                AppScanStatus.Text = $"Scan complete: {scanned.Count} apps — {summary}");

            AppendLog($"[Scan] Total {scanned.Count} | Play Store {counts.GetValueOrDefault(AppCategory.PlayStore)} | User {counts.GetValueOrDefault(AppCategory.UserInstalled)} | ROM Bloat {counts.GetValueOrDefault(AppCategory.RomBloat)} | System {counts.GetValueOrDefault(AppCategory.System)}");
        }
        catch (Exception ex)
        {
            await Dispatcher.InvokeAsync(() =>
                AppScanStatus.Text = $"Scan error: {ex.Message}");
            AppendLog($"[Scan] Error: {ex.Message}");
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
            MessageBox.Show(Strings.SelectAtLeastOneApp, "MintADB");
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
            MessageBox.Show(Strings.SelectAtLeastOneApp, "MintADB");
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
            MessageBox.Show(Strings.SelectAtLeastOneApp, "MintADB");
            return;
        }

        if (OptMiuiOpt.IsChecked == true)
        {
            var confirm = MessageBox.Show(
                "Disabling MIUI Optimization will change system behavior.\nReboot required after.\nContinue?",
                Strings.Confirm, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;
        }

        if (OptRegion.IsChecked == true)
        {
            var confirm = MessageBox.Show(
                "Changing region CN → Global will change system region.\nReboot required after.\nContinue?",
                Strings.Confirm, MessageBoxButton.YesNo, MessageBoxImage.Question);
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
                OptRegion.IsChecked == true,
                OptAnalytics.IsChecked == true,
                AppendLog);
            MessageBox.Show("Optimization complete! Check log for details.", "MintADB");
        });
    }

    // ── Backup App List ──

    private async void BackupAppList_Click(object sender, RoutedEventArgs e)
    {
        var apps = GetSelectedApps().ToList();
        if (apps.Count == 0)
        {
            MessageBox.Show("Select at least 1 app to backup.", "MintADB");
            return;
        }

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save app list",
            Filter = "Text file (*.txt)|*.txt|CSV (*.csv)|*.csv",
            FileName = $"app_backup_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            var lines = apps.Select(a => $"{a.Package}\t{a.Name}");
            await File.WriteAllLinesAsync(dlg.FileName, lines);
            AppendLog($"[Backup] Saved {apps.Count} apps to {dlg.FileName}");
            MessageBox.Show($"Saved {apps.Count} apps:\n{dlg.FileName}", "Backup");
        }
        catch (Exception ex)
        {
            AppendLog($"[FAIL] Backup: {ex.Message}");
        }
    }

    // ── Restore App List ──

    private async void RestoreAppList_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select backup file",
            Filter = "Text file (*.txt)|*.txt|CSV (*.csv)|*.csv",
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            var lines = await File.ReadAllLinesAsync(dlg.FileName);
            var packages = lines
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Split('\t')[0].Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToList();

            if (packages.Count == 0)
            {
                MessageBox.Show("File has no valid packages.", "MintADB");
                return;
            }

            // Select matching apps
            var count = 0;
            foreach (var app in _apps)
            {
                app.Selected = packages.Contains(app.Package);
                if (app.Selected) count++;
            }

            AppendLog($"[Restore] Selected {count}/{packages.Count} apps from backup file");
            MessageBox.Show($"Selected {count} apps from backup file.", "Restore");
        }
        catch (Exception ex)
        {
            AppendLog($"[FAIL] Restore: {ex.Message}");
        }
    }

    // ── Debloat Safe Mode (script Hyper.txt) ──

    private HyperDebloatService? _hyperDebloat;
    private HyperDebloatService HyperDebloat => _hyperDebloat ??= new HyperDebloatService(_adb, Tools);

    private async void DebloatSafe_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var pkgCount = HyperDebloatService.CountUniquePackages();
        if (pkgCount == 0)
        {
            MessageBox.Show(Loc.Get("DebloatHyperEmpty"), "MintADB", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            Loc.Get("DebloatHyperTitle") + "\n\n" + Loc.Format("DebloatHyperConfirm", pkgCount),
            Loc.Get("Confirm", Strings.Confirm),
            MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        ClearLog();
        await RunWithBusyAsync(async () =>
        {
            try
            {
                ShowOptProgress(true, Loc.Get("DebloatHyperRunning"), 0, 1);
                var progress = new Progress<(int Current, int Total, string Status)>(p =>
                    ShowOptProgress(true, p.Status, p.Current, p.Total));

                var (ok, fail, skip) = await HyperDebloat.RunAsync(
                    serial, AppendLog, backupApk: true, progress: progress);

                ShowOptProgress(true, Loc.Get("DebloatHyperDoneStatus"), 1, 1);
                MessageBox.Show(Loc.Format("DebloatHyperDone", ok, skip, fail), "MintADB");
            }
            catch (Exception ex)
            {
                AppendLog($"[FAIL] Debloat Hyper: {ex.Message}");
                MessageBox.Show(ex.Message, "MintADB", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ShowOptProgress(false);
            }
        });
    }

    private void ShowOptProgress(bool visible, string? text = null, int current = 0, int total = 0)
    {
        void Apply()
        {
            OptProgressBorder.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            if (!visible) return;

            if (!string.IsNullOrWhiteSpace(text))
                OptProgressText.Text = text;

            var max = Math.Max(total, 1);
            OptProgressBar.Minimum = 0;
            OptProgressBar.Maximum = max;
            OptProgressBar.Value = Math.Clamp(current, 0, max);
            OptProgressCount.Text = $"{Math.Clamp(current, 0, max)}/{max}";
            OptProgressBar.IsIndeterminate = total <= 0;
        }

        if (Dispatcher.CheckAccess())
            Apply();
        else
            Dispatcher.Invoke(Apply);
    }

    // ── Undo ──

    private readonly List<(string Action, List<string> Packages)> _undoHistory = new();
    private List<string> _lastRemovedPackages = new();

    private void RecordRemoveAction(List<string> packages)
    {
        _undoHistory.Add(("remove", new List<string>(packages)));
        _lastRemovedPackages = packages;
        UpdateUndoUI();
    }

    private void RecordDisableAction(List<string> packages)
    {
        _undoHistory.Add(("disable", new List<string>(packages)));
        _lastRemovedPackages = packages;
        UpdateUndoUI();
    }

    private void UpdateUndoUI()
    {
        if (_undoHistory.Count > 0)
        {
            var last = _undoHistory[^1];
            UndoInfoText.Text = $"{last.Action}: {last.Packages.Count} app ({string.Join(", ", last.Packages.Take(3))}...)";
            UndoBorder.Visibility = Visibility.Visible;
        }
        else
        {
            UndoBorder.Visibility = Visibility.Collapsed;
        }
    }

    private async void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (_undoHistory.Count == 0) return;

        var serial = RequireDevice();
        if (serial is null) return;

        var last = _undoHistory[^1];
        _undoHistory.RemoveAt(_undoHistory.Count - 1);

        await RunWithBusyAsync(async () =>
        {
            foreach (var pkg in last.Packages)
            {
                if (last.Action == "remove")
                {
                    var r = await Tools.EnablePackageAsync(serial, pkg);
                    AppendLog(r.Ok ? $"[OK] Restored {pkg}" : $"[FAIL] {pkg}: {r.Combined}");
                }
                else if (last.Action == "disable")
                {
                    var r = await Tools.EnablePackageAsync(serial, pkg);
                    AppendLog(r.Ok ? $"[OK] Re-enabled {pkg}" : $"[FAIL] {pkg}: {r.Combined}");
                }
            }
        });

        UpdateUndoUI();
    }

    private void ClearUndo_Click(object sender, RoutedEventArgs e)
    {
        _undoHistory.Clear();
        _lastRemovedPackages.Clear();
        UpdateUndoUI();
    }

    private void SetActionButtonsEnabled(bool enabled)
    {
        // Block double-click via IsHitTestVisible — not IsEnabled=false (avoids white flash on all tabs)
        PanelOptimize.IsHitTestVisible = enabled;
        PanelTools.IsHitTestVisible = enabled;
        NavOptimize.IsHitTestVisible = enabled;
        NavTools.IsHitTestVisible = enabled;
    }

    private void LanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressLanguageEvent) return;
        if (LanguageSelector.SelectedItem is ComboBoxItem item && item.Tag is string lang)
            LanguageManager.SetLanguage(lang);
    }

    private sealed record ComboItem(AppCategory? Category, string Label);
}