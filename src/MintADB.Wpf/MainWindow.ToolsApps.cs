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
        });

    private async void ToolDisableBattery_Click(object sender, RoutedEventArgs e)
        => await RunToolAppActionAsync("Tắt tối ưu pin", async (serial, app) =>
        {
            await UserAppOptimizer.DisableBatteryOptimizationAsync(serial, app.Package, AppendLog);
        });

    private async void ToolApplyAll_Click(object sender, RoutedEventArgs e)
        => await RunToolAppActionAsync("Áp dụng tất cả", async (serial, app) =>
        {
            AppendLog($"=== {app.Name} ({app.Package}) ===");
            await UserAppOptimizer.GrantSelectedPermissionsAsync(
                serial, app.Package, GetSelectedPermissions(), AppendLog);
            var preset = UserAppOptimizerService.ToPreset(app);
            await UserAppOptimizer.OptimizeNotificationsAsync(serial, preset, AppendLog);
            await UserAppOptimizer.DisableBatteryOptimizationAsync(serial, app.Package, AppendLog);
            AppendLog("");
        }, requirePermissions: true);

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