using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using MintADB.Wpf.Models;
using MintADB.Wpf.Services;

namespace MintADB.Wpf;

public partial class MainWindow
{
    private readonly ObservableCollection<InstalledApp> _toolApps = [];
    private readonly ObservableCollection<AppPermissionOption> _toolPermissions = [];
    private CollectionViewSource _toolAppViewSource = null!;
    private CollectionViewSource _toolPermissionViewSource = null!;
    private UserAppOptimizerService? _userAppOptimizer;
    private string _toolAppFilter = "";
    private bool _toolAppScanning;

    private UserAppOptimizerService UserAppOptimizer =>
        _userAppOptimizer ??= new UserAppOptimizerService(_adb, _optimizer);

    private void InitToolAppsPanel()
    {
        _toolAppViewSource = new CollectionViewSource { Source = _toolApps };
        _toolAppViewSource.GroupDescriptions.Add(new PropertyGroupDescription(nameof(InstalledApp.CategoryLabel)));
        _toolAppViewSource.SortDescriptions.Add(new SortDescription(nameof(InstalledApp.CategorySortOrder), ListSortDirection.Ascending));
        _toolAppViewSource.SortDescriptions.Add(new SortDescription(nameof(InstalledApp.Name), ListSortDirection.Ascending));
        _toolAppViewSource.Filter += OnToolAppFilter;
        ToolAppList.ItemsSource = _toolAppViewSource.View;

        foreach (var perm in AppPermissionCatalog.CreateDefaults())
        {
            _toolPermissions.Add(perm);
            perm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(AppPermissionOption.Selected))
                    RefreshToolPermSummary();
            };
        }

        _toolPermissionViewSource = new CollectionViewSource { Source = _toolPermissions };
        _toolPermissionViewSource.GroupDescriptions.Add(new PropertyGroupDescription(nameof(AppPermissionOption.Group)));
        _toolPermissionViewSource.SortDescriptions.Add(new SortDescription(nameof(AppPermissionOption.Group), ListSortDirection.Ascending));
        _toolPermissionViewSource.SortDescriptions.Add(new SortDescription(nameof(AppPermissionOption.Label), ListSortDirection.Ascending));
        ToolPermissionList.ItemsSource = _toolPermissionViewSource.View;
        RefreshToolPermSummary();
    }

    private void RefreshToolPermSummary()
    {
        var selected = _toolPermissions.Count(p => p.Selected);
        ToolPermSummaryText.Text = $"{selected}/{_toolPermissions.Count} quyền đã chọn";
    }

    private void ApplyToolPermPreset(IEnumerable<string> ids)
    {
        var set = ids.ToHashSet(StringComparer.Ordinal);
        foreach (var p in _toolPermissions)
            p.Selected = set.Contains(p.Id);
        RefreshToolPermSummary();
    }

    private void OnToolAppFilter(object sender, FilterEventArgs e)
    {
        if (e.Item is not InstalledApp app) { e.Accepted = false; return; }

        if (string.IsNullOrWhiteSpace(_toolAppFilter))
        {
            e.Accepted = true;
            return;
        }

        var q = _toolAppFilter;
        e.Accepted = app.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                     || app.Package.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshToolAppView() => _toolAppViewSource?.View?.Refresh();

    private List<InstalledApp> GetSelectedToolApps() =>
        _toolApps.Where(a => a.Selected).ToList();

    private IEnumerable<AppPermissionOption> GetSelectedPermissions() =>
        _toolPermissions.Where(p => p.Selected);

    private void ToolNavApps_Click(object sender, RoutedEventArgs e) => ShowToolPage(1);

    private async void ScanToolApps_Click(object sender, RoutedEventArgs e)
    {
        var serial = RequireDevice();
        if (serial is null) return;
        if (_toolAppScanning) return;

        _toolAppScanning = true;
        ToolAppScanStatus.Text = "Đang quét...";
        try
        {
            if (_apps.Count > 0)
            {
                _toolApps.Clear();
                foreach (var app in _apps.Where(a => a.Category is AppCategory.PlayStore or AppCategory.UserInstalled))
                    _toolApps.Add(app);

                RefreshToolAppView();
                var play = _toolApps.Count(a => a.Category == AppCategory.PlayStore);
                var user = _toolApps.Count(a => a.Category == AppCategory.UserInstalled);
                ToolAppScanStatus.Text = $"{_toolApps.Count} app · Play Store {play} · User {user} (từ cache)";
                AppendLog($"[Công cụ/Ứng dụng] Đồng bộ {_toolApps.Count} app từ danh sách đã quét");
                return;
            }

            await RunWithBusyAsync(async () =>
            {
                var scanned = await _appScan.ScanAllAsync(serial);
                _toolApps.Clear();
                foreach (var app in scanned)
                {
                    if (app.Category is AppCategory.PlayStore or AppCategory.UserInstalled)
                        _toolApps.Add(app);
                }

                RefreshToolAppView();
                var play = _toolApps.Count(a => a.Category == AppCategory.PlayStore);
                var user = _toolApps.Count(a => a.Category == AppCategory.UserInstalled);
                ToolAppScanStatus.Text = $"{_toolApps.Count} app · Play Store {play} · User {user}";
                AppendLog($"[Công cụ/Ứng dụng] Quét {_toolApps.Count} app (Play Store + user)");
            });
        }
        catch (Exception ex)
        {
            ToolAppScanStatus.Text = $"Lỗi: {ex.Message}";
            AppendLog($"[FAIL] Quét app: {ex.Message}");
        }
        finally
        {
            _toolAppScanning = false;
        }
    }

    private void SelectAllToolApps_Click(object sender, RoutedEventArgs e)
    {
        foreach (var app in _toolApps) app.Selected = true;
    }

    private void DeselectAllToolApps_Click(object sender, RoutedEventArgs e)
    {
        foreach (var app in _toolApps) app.Selected = false;
    }

    private void SelectAllToolPermissions_Click(object sender, RoutedEventArgs e)
    {
        foreach (var p in _toolPermissions) p.Selected = true;
        RefreshToolPermSummary();
    }

    private void DeselectAllToolPermissions_Click(object sender, RoutedEventArgs e)
    {
        foreach (var p in _toolPermissions) p.Selected = false;
        RefreshToolPermSummary();
    }

    private void ToolPresetChat_Click(object sender, RoutedEventArgs e)
        => ApplyToolPermPreset(AppPermissionCatalog.ChatPresetIds);

    private void ToolPresetBasic_Click(object sender, RoutedEventArgs e)
        => ApplyToolPermPreset(AppPermissionCatalog.BasicPresetIds);

    private void ToolPresetBattery_Click(object sender, RoutedEventArgs e)
        => ApplyToolPermPreset(AppPermissionCatalog.BatteryPresetIds);

    private void ToolPresetMiuiAutostart_Click(object sender, RoutedEventArgs e)
        => ApplyToolPermPreset(AppPermissionCatalog.MiuiAutostartPresetIds);

    private void ToolPresetShizuku_Click(object sender, RoutedEventArgs e)
        => ApplyToolPermPreset(AppPermissionCatalog.ShizukuPresetIds);

    private void ToolAppSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _toolAppFilter = GetBoxText(ToolAppSearchBox);
        RefreshToolAppView();
    }

    private void ToolAppList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source)
        {
            var el = source;
            while (el is not null)
            {
                if (el is CheckBox) return;
                if (el is ListBoxItem) break;
                el = VisualTreeHelper.GetParent(el);
            }
        }

        if (e.OriginalSource is not DependencyObject hit) return;
        while (hit is not null and not ListBoxItem)
            hit = VisualTreeHelper.GetParent(hit);
        if (hit is ListBoxItem { DataContext: InstalledApp app })
        {
            app.Selected = !app.Selected;
            e.Handled = true;
        }
    }

    private static readonly Dictionary<string, string> KnownPerms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["android.permission.POST_NOTIFICATIONS"] = "Thông báo",
        ["android.permission.RECEIVE_BOOT_COMPLETED"] = "Tự khởi động",
        ["android.permission.VIBRATE"] = "Rung",
        ["android.permission.FOREGROUND_SERVICE"] = "Foreground service",
        ["android.permission.FOREGROUND_SERVICE_DATA_SYNC"] = "FGS đồng bộ",
        ["android.permission.FOREGROUND_SERVICE_MEDIA_PLAYBACK"] = "FGS media",
        ["android.permission.FOREGROUND_SERVICE_LOCATION"] = "FGS vị trí",
        ["android.permission.FOREGROUND_SERVICE_PHONE_CALL"] = "FGS cuộc gọi",
        ["android.permission.FOREGROUND_SERVICE_CONNECTED_DEVICE"] = "FGS kết nối",
        ["android.permission.FOREGROUND_SERVICE_CAMERA"] = "FGS camera",
        ["android.permission.FOREGROUND_SERVICE_HEALTH"] = "FGS sức khỏe",
        ["android.permission.FOREGROUND_SERVICE_REMOTE_MESSAGING"] = "FGS từ xa",
        ["android.permission.FOREGROUND_SERVICE_SPECIAL_USE"] = "FGS đặc biệt",
        ["android.permission.REQUEST_IGNORE_BATTERY_OPTIMIZATIONS"] = "Bỏ qua tối ưu pin",
        ["android.permission.INTERNET"] = "Internet",
        ["android.permission.ACCESS_NETWORK_STATE"] = "Trạng thái mạng",
        ["android.permission.ACCESS_WIFI_STATE"] = "Trạng thái Wi-Fi",
        ["android.permission.CHANGE_WIFI_STATE"] = "Thay đổi Wi-Fi",
        ["android.permission.NFC"] = "NFC",
        ["android.permission.BLUETOOTH"] = "Bluetooth",
        ["android.permission.BLUETOOTH_ADMIN"] = "Quản lý Bluetooth",
        ["android.permission.BLUETOOTH_SCAN"] = "Quét Bluetooth",
        ["android.permission.BLUETOOTH_CONNECT"] = "Kết nối Bluetooth",
        ["android.permission.BLUETOOTH_ADVERTISE"] = "Quảng bá Bluetooth",
        ["android.permission.ACCESS_FINE_LOCATION"] = "Vị trí chính xác",
        ["android.permission.ACCESS_COARSE_LOCATION"] = "Vị trí gần đúng",
        ["android.permission.ACCESS_BACKGROUND_LOCATION"] = "Vị trí nền",
        ["android.permission.CAMERA"] = "Camera",
        ["android.permission.RECORD_AUDIO"] = "Micro",
        ["android.permission.READ_EXTERNAL_STORAGE"] = "Đọc bộ nhớ",
        ["android.permission.WRITE_EXTERNAL_STORAGE"] = "Ghi bộ nhớ",
        ["android.permission.READ_MEDIA_IMAGES"] = "Đọc ảnh",
        ["android.permission.READ_MEDIA_VIDEO"] = "Đọc video",
        ["android.permission.READ_MEDIA_AUDIO"] = "Đọc nhạc",
        ["android.permission.MANAGE_EXTERNAL_STORAGE"] = "Quản lý bộ nhớ",
        ["android.permission.SYSTEM_ALERT_WINDOW"] = "Hiển thị trên app",
        ["android.permission.SCHEDULE_EXACT_ALARM"] = "Báo thức chính xác",
        ["android.permission.USE_EXACT_ALARM"] = "Báo thức chính xác 2",
        ["android.permission.USE_FULL_SCREEN_INTENT"] = "Thông báo toàn màn hình",
        ["android.permission.REQUEST_INSTALL_PACKAGES"] = "Cài APK",
        ["android.permission.READ_CONTACTS"] = "Danh bạ",
        ["android.permission.WRITE_CONTACTS"] = "Sửa danh bạ",
        ["android.permission.READ_PHONE_STATE"] = "Điện thoại",
        ["android.permission.CALL_PHONE"] = "Gọi điện",
        ["android.permission.ANSWER_PHONE_CALLS"] = "Trả lời cuộc gọi",
        ["android.permission.MANAGE_OWN_CALLS"] = "Quản lý cuộc gọi",
        ["android.permission.RECEIVE_SMS"] = "SMS",
        ["android.permission.SEND_SMS"] = "Gửi SMS",
        ["android.permission.READ_SMS"] = "Đọc SMS",
        ["android.permission.GET_USAGE_STATS"] = "Thống kê sử dụng",
        ["android.permission.PACKAGE_USAGE_STATS"] = "Truy cập package",
        ["android.permission.WRITE_SECURE_SETTINGS"] = "Ghi thiết lập bảo mật",
        ["android.permission.WRITE_SETTINGS"] = "Ghi thiết lập",
        ["android.permission.DUMP"] = "DUMP",
        ["android.permission.READ_LOGS"] = "Đọc log",
        ["android.permission.DEVICE_POWER"] = "Nhiệt độ",
    };

    private static readonly Dictionary<string, string> KnownAppOps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["POST_NOTIFICATION"] = "Gửi thông báo",
        ["RUN_IN_BACKGROUND"] = "Chạy nền",
        ["RUN_ANY_IN_BACKGROUND"] = "Chạy nền không giới hạn",
        ["WAKE_LOCK"] = "Wake lock",
        ["START_FOREGROUND"] = "Khởi chạy foreground",
        ["SYSTEM_ALERT_WINDOW"] = "Hiển thị trên app",
        ["GET_USAGE_STATS"] = "Thống kê sử dụng",
        ["IGNORE_BATTERY_OPTIMIZATIONS"] = "Bỏ qua tối ưu pin",
        ["MANAGE_EXTERNAL_STORAGE"] = "Quản lý bộ nhớ",
        ["REQUEST_INSTALL_PACKAGES"] = "Cài APK",
        ["FINE_LOCATION"] = "Vị trí chính xác",
        ["COARSE_LOCATION"] = "Vị trí gần đúng",
        ["CAMERA"] = "Camera",
        ["RECORD_AUDIO"] = "Micro",
        ["READ_CONTACTS"] = "Danh bạ",
        ["WRITE_CONTACTS"] = "Sửa danh bạ",
        ["READ_CALL_LOG"] = "Nhật ký cuộc gọi",
        ["WRITE_CALL_LOG"] = "Ghi nhật ký cuộc gọi",
        ["READ_SMS"] = "Đọc SMS",
        ["RECEIVE_SMS"] = "Nhận SMS",
        ["SEND_SMS"] = "Gửi SMS",
        ["READ_PHONE_STATE"] = "Điện thoại",
        ["READ_EXTERNAL_STORAGE"] = "Đọc bộ nhớ",
        ["WRITE_EXTERNAL_STORAGE"] = "Ghi bộ nhớ",
        ["READ_MEDIA_IMAGES"] = "Đọc ảnh",
        ["READ_MEDIA_VIDEO"] = "Đọc video",
        ["READ_MEDIA_AUDIO"] = "Đọc nhạc",
        ["WRITE_MEDIA_IMAGES"] = "Ghi ảnh",
        ["WRITE_MEDIA_VIDEO"] = "Ghi video",
        ["WRITE_MEDIA_AUDIO"] = "Ghi nhạc",
        ["BLUETOOTH_SCAN"] = "Quét Bluetooth",
        ["BLUETOOTH_CONNECT"] = "Kết nối Bluetooth",
        ["BLUETOOTH_ADVERTISE"] = "Quảng bá Bluetooth",
        ["NEARBY_WIFI_DEVICES"] = "Thiết bị gần",
        ["USE_FULL_SCREEN_INTENT"] = "Thông báo toàn màn hình",
        ["ACTIVITY_RECOGNITION"] = "Nhận diện hoạt động",
        ["BODY_SENSORS"] = "Cảm biến cơ thể",
        ["ACCESS_MEDIA_LOCATION"] = "Vị trí media",
        ["VIBRATE"] = "Rung",
        ["TOAST_WINDOW"] = "Toast",
        ["GET_ACCOUNTS"] = "Tài khoản",
        ["READ_CLIPBOARD"] = "Đọc clipboard",
        ["WRITE_CLIPBOARD"] = "Ghi clipboard",
        ["WIFI_SCAN"] = "Quét Wi-Fi",
        ["LEGACY_STORAGE"] = "Bộ nhớ cũ",
        ["GPS"] = "GPS",
        ["MONITOR_LOCATION"] = "Theo dõi vị trí",
        ["MONITOR_HIGH_POWER_LOCATION"] = "Theo dõi vị trí (cao)",
        ["WRITE_SETTINGS"] = "Ghi thiết lập",
        ["TURN_SCREEN_ON"] = "Bật màn hình",
        ["CONTROL_AUDIO"] = "Điều khiển âm thanh",
        ["CONTROL_AUDIO_PARTIAL"] = "Điều khiển âm thanh 1 phần",
        ["TAKE_AUDIO_FOCUS"] = "Lấy nét âm thanh",
        ["AUDIO_MEDIA_VOLUME"] = "Âm lượng media",
        ["READ_PHONE_NUMBERS"] = "Đọc số điện thoại",
        ["PROCESS_OUTGOING_CALLS"] = "Xử lý cuộc gọi đi",
        ["ACCEPT_HANDOVER"] = "Chuyển tiếp cuộc gọi",
        ["UWB_RANGING"] = "UWB",
        ["RANGING"] = "Đo khoảng cách",
        ["ADD_VOICEMAIL"] = "Thêm voicemail",
        ["USE_SIP"] = "SIP",
        ["READ_CELL_BROADCASTS"] = "Broadcast tế bào",
        ["READ_HEART_RATE"] = "Nhịp tim",
        ["READ_OXYGEN_SATURATION"] = "Oxy trong máu",
        ["READ_SKIN_TEMPERATURE"] = "Nhiệt độ da",
        ["READ_DEVICE_IDENTIFIERS"] = "Định danh thiết bị",
        ["USE_ICC_AUTH_WITH_DEVICE_IDENTIFIER"] = "ICC auth",
        ["ACCESS_RESTRICTED_SETTINGS"] = "Cài đặt hạn chế",
        ["READ_MEDIA_VISUAL_USER_SELECTED"] = "Người dùng chọn media",
    };

    private async void ToolCheckPermissions_Click(object sender, RoutedEventArgs e)
        => await RunToolAppActionAsync("Kiểm tra quyền", async (serial, app) =>
        {
            AppendLog($"=== {app.Name} ({app.Package}) ===");
            var dump = await _adb.ShellAsync($"dumpsys package {app.Package}", serial);
            if (!dump.Ok) { AppendLog("[LỖI] Không thể đọc dumpsys package"); return; }

            var output = dump.Combined;
            var hasRuntime = false;
            foreach (var line in output.Split('\n', '\r'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("grantedPermissions:", StringComparison.OrdinalIgnoreCase))
                {
                    hasRuntime = true;
                    AppendLog("  <Runtime>");
                    var list = trimmed["grantedPermissions:".Length..].Trim();
                    if (list.StartsWith("["))
                    {
                        foreach (var p in list.Trim('[', ']').Split(','))
                        {
                            var pn = p.Trim().Trim('"', ' ');
                            if (!string.IsNullOrWhiteSpace(pn))
                                AppendLog($"  ✅ {KnownPerms.GetValueOrDefault(pn, pn)}");
                        }
                    }
                    continue;
                }
                if (hasRuntime && trimmed.StartsWith("    ") && trimmed.Contains('='))
                {
                    var parts = trimmed.Split('=', 2);
                    var permName = parts[0].Trim();
                    var granted = parts.Length > 1 && parts[1].Trim().TrimEnd(',') == "true";
                    var label = KnownPerms.GetValueOrDefault(permName, permName);
                    AppendLog($"  {(granted ? "✅" : "  ")} {label}");
                }
            }

            AppendLog("  <AppOps>");
            var appops = await _adb.ShellAsync($"cmd appops get {app.Package}", serial);
            if (appops.Ok && !string.IsNullOrWhiteSpace(appops.Output))
            {
                foreach (var opLine in appops.Output.Split('\r', '\n'))
                {
                    var opTrimmed = opLine.Trim();
                    if (string.IsNullOrWhiteSpace(opTrimmed) || opTrimmed.StartsWith("Uid mode"))
                        continue;

                    var parts = opTrimmed.Split(':', 2, StringSplitOptions.TrimEntries);
                    var opName = parts[0];
                    var opVal = parts.Length > 1 ? parts[1].Trim() : "";
                    var opLabel = KnownAppOps.GetValueOrDefault(opName, opName);

                    var mode = opVal.Split(';', ' ')[0].Trim().ToLowerInvariant();
                    var tag = mode switch
                    {
                        "allow" => "[ALLOW]",
                        "foreground" => "[FG  ]",
                        "deny" => "[DENY ]",
                        "ignore" => "[IGN  ]",
                        "default" => "[DEF  ]",
                        _ => "[?    ]",
                    };
                    AppendLog($"  {tag} {opLabel}");
                }
            }
        }, requirePermissions: false);

    private async void ToolGrantPermissions_Click(object sender, RoutedEventArgs e)
        => await RunToolAppActionAsync("Cấp quyền", async (serial, app) =>
        {
            AppendLog($"--- {app.Name} ({app.Package}) ---");
            await UserAppOptimizer.GrantSelectedPermissionsAsync(
                serial, app.Package, GetSelectedPermissions(), AppendLog);
        }, requirePermissions: true);

    private async void ToolFixNotifications_Click(object sender, RoutedEventArgs e)
        => await RunToolAppActionAsync("Tối ưu thông báo", async (serial, app) =>
        {
            var preset = UserAppOptimizerService.ToPreset(app);
            await UserAppOptimizer.OptimizeNotificationsAsync(serial, preset, AppendLog);
            await UserAppOptimizer.DisableBatteryOptimizationAsync(serial, app.Package, AppendLog);
        });

    private async void ToolOpenAppSettings_Click(object sender, RoutedEventArgs e)
        => await RunToolAppActionAsync("Mở Settings", async (serial, app) =>
        {
            await _adb.ShellAsync($"am start -a android.settings.APPLICATION_DETAILS_SETTINGS -d package:{app.Package}", serial);
            AppendLog($"  Đã mở Settings > {app.Name}");
        }, requirePermissions: false);

    private async void ToolDisableBattery_Click(object sender, RoutedEventArgs e)
        => await RunToolAppActionAsync("Tắt tối ưu pin", async (serial, app) =>
        {
            await UserAppOptimizer.DisableBatteryOptimizationAsync(serial, app.Package, AppendLog);
        });

    private async void ToolApplyAll_Click(object sender, RoutedEventArgs e)
        => await RunToolAppActionAsync("Áp dụng tất cả", async (serial, app) =>
        {
            AppendLog($"=== {app.Name} ({app.Package}) ===");
            var selectedPerms = GetSelectedPermissions();
            if (selectedPerms.Any())
                await UserAppOptimizer.GrantSelectedPermissionsAsync(
                    serial, app.Package, selectedPerms, AppendLog);
            var preset = UserAppOptimizerService.ToPreset(app);
            await UserAppOptimizer.OptimizeNotificationsAsync(serial, preset, AppendLog);
            await UserAppOptimizer.DisableBatteryOptimizationAsync(serial, app.Package, AppendLog);
            AppendLog("");
        }, requirePermissions: false);

    private async Task RunToolAppActionAsync(
        string label,
        Func<string, InstalledApp, Task> action,
        bool requirePermissions = false)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var apps = GetSelectedToolApps();
        if (apps.Count == 0)
        {
            MessageBox.Show("Chọn ít nhất 1 app (Play Store hoặc user cài).", "MintADB");
            return;
        }

        if (requirePermissions && !GetSelectedPermissions().Any())
        {
            MessageBox.Show("Chọn ít nhất 1 quyền.", "MintADB");
            return;
        }

        if (MessageBox.Show(
                $"{label} cho {apps.Count} app đã chọn?",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        await RunWithBusyAsync(async () =>
        {
            AppendLog($"[{label}] Bắt đầu ({apps.Count} app)...");
            foreach (var app in apps)
                await action(serial, app);
            AppendLog($"[{label}] Hoàn tất.");
        });
    }
}