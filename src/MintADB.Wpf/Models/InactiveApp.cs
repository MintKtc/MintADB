using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MintADB.Wpf.Models;

public enum InactiveAppState
{
    Disabled,
    Uninstalled,
    Hidden,
}

public static class InactiveAppStateExtensions
{
    public static string Label(this InactiveAppState state) => state switch
    {
        InactiveAppState.Disabled => "Đã tắt",
        InactiveAppState.Uninstalled => "Đã gỡ",
        InactiveAppState.Hidden => "Đã ẩn",
        _ => state.ToString(),
    };

    public static int SortOrder(this InactiveAppState state) => state switch
    {
        InactiveAppState.Uninstalled => 0,
        InactiveAppState.Disabled => 1,
        InactiveAppState.Hidden => 2,
        _ => 9,
    };

    public static string RowBackground(this InactiveAppState state) => state switch
    {
        InactiveAppState.Uninstalled => "#2A1C1C",
        InactiveAppState.Disabled => "#2A2418",
        InactiveAppState.Hidden => "#1A2533",
        _ => "#323232",
    };

    public static string RowBorder(this InactiveAppState state) => state switch
    {
        InactiveAppState.Uninstalled => "#8A3030",
        InactiveAppState.Disabled => "#8A6B1E",
        InactiveAppState.Hidden => "#2B5A7A",
        _ => "#484848",
    };

    public static string AccentColor(this InactiveAppState state) => state switch
    {
        InactiveAppState.Uninstalled => "#FF8A82",
        InactiveAppState.Disabled => "#FFD060",
        InactiveAppState.Hidden => "#7EC8FF",
        _ => "#AEAEB2",
    };

    public static string BadgeBackground(this InactiveAppState state) => state switch
    {
        InactiveAppState.Uninstalled => "#3D1A1A",
        InactiveAppState.Disabled => "#3D3018",
        InactiveAppState.Hidden => "#1A3050",
        _ => "#3A3A3C",
    };

    public static string BadgeForeground(this InactiveAppState state) => AccentColor(state);
}

public sealed class InactiveApp : INotifyPropertyChanged
{
    private bool _selected;

    public string Package { get; init; } = "";
    public string Name { get; init; } = "";
    public InactiveAppState State { get; init; }

    public string StateLabel => State.Label();
    public int StateSortOrder => State.SortOrder();

    public string StateRowBackground => State.RowBackground();
    public string StateRowBorder => State.RowBorder();
    public string StateAccentColor => State.AccentColor();
    public string StateBadgeBackground => State.BadgeBackground();
    public string StateBadgeForeground => State.BadgeForeground();

    public bool Selected
    {
        get => _selected;
        set { if (_selected == value) return; _selected = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}