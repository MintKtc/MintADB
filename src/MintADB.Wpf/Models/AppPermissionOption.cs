using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MintADB.Wpf.Models;

public enum PermissionGrantKind
{
    Runtime,
    AppOp,
    Miui,
}

public sealed class AppPermissionOption : INotifyPropertyChanged
{
    private bool _selected;

    public required string Id { get; init; }
    public required string Label { get; init; }
    public required string Description { get; init; }
    public required string Group { get; init; }
    public required PermissionGrantKind Kind { get; init; }
    public required string Value { get; init; }

    public string KindLabel => Kind switch
    {
        PermissionGrantKind.Runtime => "pm grant",
        PermissionGrantKind.AppOp => "AppOps",
        PermissionGrantKind.Miui => "MIUI",
        _ => "—",
    };

    public string TechnicalName => Kind switch
    {
        PermissionGrantKind.Runtime when Value.StartsWith("android.permission.", StringComparison.Ordinal)
            => Value["android.permission.".Length..],
        _ => Value,
    };

    public bool Selected
    {
        get => _selected;
        set { if (_selected == value) return; _selected = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}