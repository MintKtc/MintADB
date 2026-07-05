namespace MintADB.Wpf.Models;

public sealed class BatteryInfoResult
{
    public int? LevelPercent { get; init; }
    public string? Technology { get; init; }
    public string? Status { get; init; }
    public string? Health { get; init; }
    public int? CurrentMah { get; init; }
    public int? MaxMah { get; init; }
    public string? Voltage { get; init; }
    public string? Temperature { get; init; }
    public string? Current { get; init; }
    public string MetricsText { get; init; } = "";
}

public sealed class DisplayInfoResult
{
    public string? Resolution { get; init; }
    public string? Dpi { get; init; }
    public float? RefreshHz { get; init; }
    public float? PeakHz { get; init; }
    public float? MinHz { get; init; }
    public string? PanelTech { get; init; }
    public string MetricsText { get; init; } = "";
}

public sealed class DeviceInfoResult
{
    public string? Manufacturer { get; init; }
    public string? Brand { get; init; }
    public string? Model { get; init; }
    public string? DeviceCodename { get; init; }
    public string? MarketName { get; init; }
    public string? AndroidVersion { get; init; }
    public int? SdkInt { get; init; }
    public string? SecurityPatch { get; init; }
    public string? OsName { get; init; }
    public string? OsVersion { get; init; }
    public string? RomRegion { get; init; }
    public string? RomBuild { get; init; }
    public string? RomType { get; init; }
    public string? Serial { get; init; }
    public string MetricsText { get; init; } = "";
}