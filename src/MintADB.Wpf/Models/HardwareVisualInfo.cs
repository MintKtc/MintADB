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
    public string? PanelName { get; init; }
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

public sealed class StorageInfoResult
{
    public long InternalTotal { get; set; }
    public long InternalUsed { get; set; }
    public long InternalAvail { get; set; }
    public long SdTotal { get; set; }
    public long SdUsed { get; set; }
    public long SdAvail { get; set; }
    public bool HasEmulatedStorage { get; set; }

    public string InternalTotalText => FormatBytes(InternalTotal);
    public string InternalUsedText => FormatBytes(InternalUsed);
    public string InternalAvailText => FormatBytes(InternalAvail);
    public string SdTotalText => FormatBytes(SdTotal);
    public string SdUsedText => FormatBytes(SdUsed);
    public string SdAvailText => FormatBytes(SdAvail);
    public double InternalPercent => InternalTotal > 0 ? InternalUsed * 100.0 / InternalTotal : 0;

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (1L << 30):F1} GB",
        >= 1L << 20 => $"{bytes / (1L << 20):F0} MB",
        _ => $"{bytes / 1L << 10:F0} KB"
    };
}

public sealed class RamInfoResult
{
    public long TotalKb { get; set; }
    public long FreeKb { get; set; }
    public long AvailableKb { get; set; }
    public long UsedKb { get; set; }
    public long BuffersKb { get; set; }
    public long CachedKb { get; set; }
    public long SwapTotalKb { get; set; }
    public long SwapFreeKb { get; set; }

    public string TotalText => FormatKb(TotalKb);
    public string UsedText => FormatKb(UsedKb);
    public string AvailableText => FormatKb(AvailableKb);
    public string FreeText => FormatKb(FreeKb);
    public string CachedText => FormatKb(CachedKb);
    public string SwapText => FormatKb(SwapTotalKb - SwapFreeKb);
    public double Percent => TotalKb > 0 ? UsedKb * 100.0 / TotalKb : 0;

    private static string FormatKb(long kb) => kb switch
    {
        >= 1_048_576 => $"{kb / 1_048_576.0:F1} GB",
        >= 1024 => $"{kb / 1024.0:F0} MB",
        _ => $"{kb} KB"
    };
}

public sealed class CpuInfoResult
{
    public int CoreCount { get; set; }
    public string? Hardware { get; set; }
    public string? Revision { get; set; }
    public long MaxFreqKhz { get; set; }
    public string? SocModel { get; set; }
    public string? SocManufacturer { get; set; }

    public string MaxFreqText => MaxFreqKhz > 0 ? $"{MaxFreqKhz / 1000.0:F0} MHz" : "N/A";
    public string Summary => $"{SocModel ?? Hardware ?? "Unknown"} · {CoreCount} cores · {MaxFreqText}";
}

public sealed class GpuInfoResult
{
    public string? Renderer { get; set; }
    public long MaxFreqHz { get; set; }
    public string? GpuModel { get; set; }

    public string MaxFreqText => MaxFreqHz > 0 ? $"{MaxFreqHz / 1_000_000.0:F0} MHz" : "N/A";
    public string Summary => $"{GpuModel ?? "Unknown"} · {Renderer ?? "N/A"} · {MaxFreqText}";
}

public sealed class TouchInfoResult
{
    public string? TouchScreenSize { get; set; }
    public int? SamplingRateHz { get; set; }

    public string SamplingRateText => SamplingRateHz > 0 ? $"{SamplingRateHz} Hz" : "N/A";
}