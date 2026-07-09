namespace MintADB.Wpf.Helpers;

/// <summary>
/// Trước đây chặn gỡ một số gói hệ thống (Security/Updater/Launcher…).
/// Hiện không chặn — người dùng tự chịu rủi ro bootloop nếu gỡ nhầm.
/// </summary>
public static class DebloatBlacklist
{
    public static bool IsProtected(string package) => false;

    public static string? GetReason(string package) => null;

    public static IReadOnlyList<string> FilterProtected(IEnumerable<string> packages) => [];
}
