namespace MintADB.Wpf.Models;

public enum BundledApkKind
{
    Other,
    Keyboard,
    Store,
    Settings,
    Tool,
}

public sealed class BundledApk
{
    public required string FileName { get; init; }
    public required string FullPath { get; init; }
    public required string DisplayName { get; init; }
    public required BundledApkKind Kind { get; init; }
    public required string RemotePath { get; init; }
    public long SizeBytes { get; init; }

    public string SizeText => SizeBytes >= 1024 * 1024
        ? $"{SizeBytes / (1024.0 * 1024.0):F1} MB"
        : $"{SizeBytes / 1024.0:F0} KB";

    public string IconLetter => string.IsNullOrWhiteSpace(DisplayName)
        ? "?"
        : char.ToUpperInvariant(DisplayName[0]).ToString();

    public string KindLabel => Kind switch
    {
        BundledApkKind.Keyboard => "Bàn phím",
        BundledApkKind.Store => "Cửa hàng",
        BundledApkKind.Settings => "Cài đặt",
        BundledApkKind.Tool => "Công cụ",
        _ => "APK",
    };

}