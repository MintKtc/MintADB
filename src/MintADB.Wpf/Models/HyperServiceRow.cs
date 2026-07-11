using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MintADB.Wpf.Models;

/// <summary>UI row for HyperOS service group list.</summary>
public sealed class HyperServiceRow : INotifyPropertyChanged
{
    private string _status = "";
    private string _details = "";
    private bool _isSelected;

    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public bool NeedsWarning { get; init; }

    public string Status
    {
        get => _status;
        set { if (_status != value) { _status = value; OnPropertyChanged(); } }
    }

    public string Details
    {
        get => _details;
        set { if (_details != value) { _details = value; OnPropertyChanged(); } }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
