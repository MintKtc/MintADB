namespace MintADB.Wpf.Models;

public sealed class FastbootDevice
{
    public string Serial { get; init; } = "";
    public string State { get; init; } = "fastboot";

    public string Label => Serial;
}