namespace MintADB.Wpf.Models;

public sealed class DeviceSpoofProfile
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required string Chip { get; init; }
    public required IReadOnlyDictionary<string, string> Props { get; init; }
}

public sealed class DeviceSpoofBackup
{
    public string Serial { get; init; } = "";
    public DateTime SavedAt { get; init; }
    public Dictionary<string, string> Props { get; init; } = new(StringComparer.Ordinal);
}

public readonly record struct DeviceSpoofCapability(
    bool HasRoot,
    bool HasResetProp,
    bool IsDebuggable,
    bool ShizukuInstalled,
    bool ShizukuRunning,
    string Summary)
{
    public bool CanFakeProps => HasResetProp || HasRoot;
}