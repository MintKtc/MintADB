using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using MintADB.Wpf.Models;

namespace MintADB.Wpf;

public partial class MainWindow
{
    private readonly ObservableCollection<InactiveApp> _inactiveApps = [];
    private CollectionViewSource _inactiveViewSource = null!;
    private int _appPage;
    private string _inactiveFilterText = "";
    private InactiveAppState? _inactiveFilterState;
    private bool _scanningInactive;

    private void InitInactiveAppList()
    {
        _inactiveViewSource = new CollectionViewSource { Source = _inactiveApps };
        _inactiveViewSource.GroupDescriptions.Add(new PropertyGroupDescription(nameof(InactiveApp.StateLabel)));
        _inactiveViewSource.SortDescriptions.Add(new SortDescription(nameof(InactiveApp.StateSortOrder), ListSortDirection.Ascending));
        _inactiveViewSource.SortDescriptions.Add(new SortDescription(nameof(InactiveApp.Name), ListSortDirection.Ascending));
        _inactiveViewSource.Filter += OnInactiveAppFilter;
        InactiveAppList.ItemsSource = _inactiveViewSource.View;
        InitInactiveStateFilter();
        ShowAppSubPage(0);
    }

    private void InitInactiveStateFilter() => RebuildInactiveStateFilter();

    private void RebuildInactiveStateFilter()
    {
        if (InactiveStateFilter is null) return;

        var selected = InactiveStateFilter.SelectedItem as InactiveStateComboItem;
        InactiveStateFilter.Items.Clear();
        InactiveStateFilter.Items.Add(new InactiveStateComboItem(
            null, MintADB.Wpf.Resources.Loc.Get("AllStates", "Tất cả trạng thái")));
        foreach (InactiveAppState state in Enum.GetValues<InactiveAppState>().OrderBy(s => s.SortOrder()))
            InactiveStateFilter.Items.Add(new InactiveStateComboItem(state, state.Label()));
        InactiveStateFilter.DisplayMemberPath = nameof(InactiveStateComboItem.Label);

        if (selected is not null)
        {
            var match = InactiveStateFilter.Items.Cast<InactiveStateComboItem>()
                .FirstOrDefault(i => Equals(i.State, selected.State));
            InactiveStateFilter.SelectedItem = match ?? InactiveStateFilter.Items[0];
        }
        else
            InactiveStateFilter.SelectedIndex = 0;
    }

    private void ShowAppSubPage(int page)
    {
        _appPage = page;
        AppPageActive.Visibility = page == 0 ? Visibility.Visible : Visibility.Collapsed;
        AppPageInactive.Visibility = page == 1 ? Visibility.Visible : Visibility.Collapsed;
        AppActionActive.Visibility = page == 0 ? Visibility.Visible : Visibility.Collapsed;
        AppActionInactive.Visibility = page == 1 ? Visibility.Visible : Visibility.Collapsed;

        SetActiveTab(page, AppNavActive, AppNavInactive);

        if (page == 1 && _inactiveApps.Count == 0 && !_scanningInactive && _selectedSerial is not null)
            _ = ScanInactiveAppsInternalAsync(auto: true);
    }

    private void AppNavActive_Click(object sender, RoutedEventArgs e) => ShowAppSubPage(0);

    private void AppNavInactive_Click(object sender, RoutedEventArgs e) => ShowAppSubPage(1);

    private async void ScanInactiveApps_Click(object sender, RoutedEventArgs e)
        => await ScanInactiveAppsInternalAsync(auto: false);

    private async Task ScanInactiveAppsInternalAsync(bool auto)
    {
        var serial = _selectedSerial;
        if (serial is null)
        {
            await Dispatcher.InvokeAsync(() =>
                InactiveAppScanStatus.Text = "Chưa chọn thiết bị — chọn máy ở sidebar trước");
            return;
        }

        if (_scanningInactive) return;
        _scanningInactive = true;
        SetActionButtonsEnabled(false);
        await Dispatcher.InvokeAsync(() =>
            InactiveAppScanStatus.Text = auto ? "Đang quét app đã tắt/gỡ..." : "Đang quét...");

        try
        {
            var scanned = await _appScan.ScanInactiveAsync(serial);
            var counts = scanned.GroupBy(a => a.State).ToDictionary(g => g.Key, g => g.Count());

            await Dispatcher.InvokeAsync(() =>
            {
                _inactiveApps.Clear();
                foreach (var app in scanned)
                    _inactiveApps.Add(app);
                RefreshInactiveAppView();
            });

            var summary = string.Join(" · ", Enum.GetValues<InactiveAppState>()
                .OrderBy(s => s.SortOrder())
                .Select(s => $"{s.Label()} {counts.GetValueOrDefault(s)}"));

            await Dispatcher.InvokeAsync(() =>
                InactiveAppScanStatus.Text = scanned.Count > 0
                    ? $"Tìm thấy {scanned.Count} app — {summary}"
                    : "Không có app đã tắt / gỡ / ẩn");

            AppendLog($"[App đã tắt/gỡ] Tổng {scanned.Count} | {summary}");
        }
        catch (Exception ex)
        {
            await Dispatcher.InvokeAsync(() =>
                InactiveAppScanStatus.Text = $"Lỗi quét: {ex.Message}");
            AppendLog($"[App đã tắt/gỡ] Lỗi: {ex.Message}");
        }
        finally
        {
            _scanningInactive = false;
            SetActionButtonsEnabled(true);
        }
    }

    private void OnInactiveAppFilter(object sender, FilterEventArgs e)
    {
        if (e.Item is not InactiveApp app)
        {
            e.Accepted = false;
            return;
        }

        if (_inactiveFilterState.HasValue && app.State != _inactiveFilterState.Value)
        {
            e.Accepted = false;
            return;
        }

        if (!string.IsNullOrWhiteSpace(_inactiveFilterText))
        {
            e.Accepted = app.Name.Contains(_inactiveFilterText, StringComparison.OrdinalIgnoreCase)
                         || app.Package.Contains(_inactiveFilterText, StringComparison.OrdinalIgnoreCase);
            return;
        }

        e.Accepted = true;
    }

    private void RefreshInactiveAppView()
    {
        if (_inactiveViewSource?.View is null) return;
        _inactiveViewSource.View.Refresh();
    }

    private IEnumerable<InactiveApp> GetSelectedInactiveApps() => _inactiveApps.Where(a => a.Selected);

    private void InactiveAppSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = InactiveAppSearchBox.Text.Trim();
        if (InactiveAppSearchBox.Tag is string hint && text == hint) text = "";
        _inactiveFilterText = text;
        RefreshInactiveAppView();
    }

    private void InactiveStateFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (InactiveStateFilter.SelectedItem is InactiveStateComboItem item)
            _inactiveFilterState = item.State;
        RefreshInactiveAppView();
    }

    private void SelectAllInactiveApps_Click(object sender, RoutedEventArgs e)
    {
        foreach (var app in _inactiveApps)
            app.Selected = true;
        InactiveAppScanStatus.Text = $"Đã chọn {_inactiveApps.Count} app";
    }

    private void DeselectAllInactiveApps_Click(object sender, RoutedEventArgs e)
    {
        foreach (var app in _inactiveApps)
            app.Selected = false;
        InactiveAppScanStatus.Text = "Đã tắt chọn tất cả";
    }

    private void InactiveAppList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;
        if (FindParent<Button>(source) is not null) return;
        if (FindParent<CheckBox>(source) is not null) return;

        if (FindParent<ListBoxItem>(source)?.DataContext is InactiveApp app)
            app.Selected = !app.Selected;
    }

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T match) return match;
            child = System.Windows.Media.VisualTreeHelper.GetParent(child);
        }
        return null;
    }

    private async void RestoreSingleInactiveApp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: InactiveApp app }) return;
        var serial = RequireDevice();
        if (serial is null) return;

        SetActionButtonsEnabled(false);
        try { await RestoreInactiveAppAsync(serial, app); }
        finally { SetActionButtonsEnabled(true); }
    }

    private async void RestoreInactiveApps_Click(object sender, RoutedEventArgs e)
        => await RestoreSelectedInactiveApps(onlyDisabled: false);

    private async void EnableInactiveApps_Click(object sender, RoutedEventArgs e)
        => await RestoreSelectedInactiveApps(onlyDisabled: true);

    private async Task RestoreSelectedInactiveApps(bool onlyDisabled)
    {
        var serial = RequireDevice();
        if (serial is null) return;

        var selected = GetSelectedInactiveApps()
            .Where(a => !onlyDisabled || a.State is InactiveAppState.Disabled or InactiveAppState.Hidden)
            .ToList();

        if (selected.Count == 0)
        {
            MessageBox.Show(onlyDisabled
                ? "Chọn app đã tắt hoặc đã ẩn."
                : "Chọn ít nhất 1 app trong danh sách.",
                "MintADB");
            return;
        }

        SetActionButtonsEnabled(false);
        try
        {
            foreach (var app in selected)
                await RestoreInactiveAppAsync(serial, app, quiet: true);
        }
        finally { SetActionButtonsEnabled(true); }
    }

    private async Task RestoreInactiveAppAsync(string serial, InactiveApp app, bool quiet = false)
    {
        if (!quiet)
            AppendLog($"[Khôi phục] {app.Package} ({app.StateLabel})...");

        var r = await Tools.RestoreInactivePackageAsync(serial, app.Package, app.State);
        if (r.Ok)
        {
            AppendLog($"[OK] Đã khôi phục {app.Name} ({app.StateLabel})");
            RemoveInactiveAppFromList(app.Package);
        }
        else
            AppendLog($"[FAIL] {app.Package}: {r.Combined}");
    }

    private void RemoveInactiveAppFromList(string package)
    {
        var existing = _inactiveApps.FirstOrDefault(a => a.Package == package);
        if (existing is null) return;
        _inactiveApps.Remove(existing);
        RefreshInactiveAppView();
    }

    private sealed record InactiveStateComboItem(InactiveAppState? State, string Label);
}