namespace MintADB.Wpf.Helpers;

/// <summary>
/// Gói hệ thống không được gỡ/tắt — tham chiếu cộng đồng MIUI (mcxiaoke gist, XDA).
/// Gỡ có thể gây bootloop hoặc mất cập nhật/bảo mật.
/// </summary>
public static class DebloatBlacklist
{
    private static readonly Dictionary<string, string> Reasons = new(StringComparer.Ordinal)
    {
        ["com.lbe.security.miui"] = "Quản lý quyền — bootloop nếu gỡ",
        ["com.android.updater"] = "Cập nhật hệ thống — bootloop",
        ["com.miui.securitycenter"] = "Mi Security — bootloop",
        ["com.miui.securityadd"] = "Mi Security bổ sung — không gỡ",
        ["com.xiaomi.finddevice"] = "Tìm máy — bootloop",
        ["com.miui.home"] = "Launcher hệ thống — bootloop",
        ["com.miui.guardprovider"] = "Thành phần bảo mật MIUI",
        ["com.xiaomi.market"] = "GetApps / App Store Xiaomi",
        ["com.xiaomi.account"] = "Tài khoản Xiaomi",
        ["com.miui.packageinstaller"] = "Trình cài gói MIUI — bootloop nếu thay",
    };

    public static bool IsProtected(string package) => Reasons.ContainsKey(package);

    public static string? GetReason(string package) =>
        Reasons.TryGetValue(package, out var reason) ? reason : null;

    public static IReadOnlyList<string> FilterProtected(IEnumerable<string> packages) =>
        packages.Where(IsProtected).ToList();
}