namespace MintADB.Wpf.Models;

public sealed class RomInfo
{
    public bool IsXiaomi { get; init; }
    public bool IsChina { get; init; }
    public string Region { get; init; } = "";
    public string Build { get; init; } = "";
    public string OsVersion { get; init; } = "";
    public bool IsHyperOs { get; init; }

    public string Summary
    {
        get
        {
            if (!IsXiaomi) return "Không phải Xiaomi/MIUI";
            var region = IsChina ? "China" : (Region.Length > 0 ? Region : "Global/EU");
            var os = IsHyperOs ? "HyperOS" : "MIUI";
            return $"{os} {OsVersion} — ROM {region}";
        }
    }
}