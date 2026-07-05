using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MintADB.Wpf.Models;

public sealed class InstalledApp : INotifyPropertyChanged
{
    private bool _selected;

    public string Package { get; init; } = "";
    public string Name { get; init; } = "";
    public string? Installer { get; init; }
    public int Uid { get; init; }
    public AppCategory Category { get; init; }
    public bool IsSystem { get; init; }

    public string CategoryLabel => Category.Label();
    public int CategorySortOrder => Category.SortOrder();

    public string CategoryRowBackground => Category.RowBackground();
    public string CategoryRowBorder => Category.RowBorder();
    public string CategoryAccentColor => Category.AccentColor();
    public string CategoryBadgeBackground => Category.BadgeBackground();
    public string CategoryBadgeForeground => Category.BadgeForeground();

    public string InstallerText =>
        string.IsNullOrWhiteSpace(Installer) || Installer is "null"
            ? "—"
            : Installer;

    public bool Selected
    {
        get => _selected;
        set { if (_selected == value) return; _selected = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}