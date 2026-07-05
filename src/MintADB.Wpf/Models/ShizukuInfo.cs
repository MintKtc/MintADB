namespace MintADB.Wpf.Models;

public sealed class ShizukuStatusResult
{
    public bool Installed { get; init; }
    public bool Running { get; init; }
    public string? Version { get; init; }
    public string SummaryText { get; init; } = "";
}