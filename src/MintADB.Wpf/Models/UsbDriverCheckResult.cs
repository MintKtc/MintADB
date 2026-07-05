namespace MintADB.Wpf.Models;

public sealed class UsbDriverCheckResult
{
    public bool BundlePresent { get; init; }
    public bool AndroidDriverInstalled { get; init; }
    public IReadOnlyList<string> InstalledDriverHits { get; init; } = [];
    public IReadOnlyList<string> ProblemDevices { get; init; } = [];
    public bool AdbToolsOk { get; init; }
    public string? AdbVersion { get; init; }
    public string Summary { get; init; } = "";

    public bool NeedsAttention =>
        !BundlePresent || !AdbToolsOk || !AndroidDriverInstalled || ProblemDevices.Count > 0;

    public DriverCheckLevel Level =>
        !BundlePresent || !AdbToolsOk ? DriverCheckLevel.Error
        : !AndroidDriverInstalled || ProblemDevices.Count > 0 ? DriverCheckLevel.Warning
        : DriverCheckLevel.Ok;
}

public enum DriverCheckLevel
{
    Ok,
    Warning,
    Error,
}