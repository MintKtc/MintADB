namespace MintADB.Wpf.Models;

public enum BundledApkKind
{
    Other,
    Keyboard,
    Store,
    Settings,
    Tool,
    KeyboardCustom,
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

    public string IconEmoji => Kind switch
    {
        BundledApkKind.Keyboard => "\U0001F5AE",       // ⌨️
        BundledApkKind.KeyboardCustom => "\U0001F5AE", // ⌨️
        BundledApkKind.Store => "\U0001F6D2",          // 🛒
        BundledApkKind.Settings => "\u2699\uFE0F",     // ⚙️
        BundledApkKind.Tool => "\U0001F527",            // 🔧
        _ => GetAppIcon(),
    };

    public string IconBackground => Kind switch
    {
        BundledApkKind.Keyboard => "#1A3A5C",
        BundledApkKind.KeyboardCustom => "#1A3A5C",
        BundledApkKind.Store => "#1C2A22",
        BundledApkKind.Settings => "#2A2418",
        BundledApkKind.Tool => "#3D2A4A",
        _ => GetAppBgColor(),
    };

    private string GetAppIcon()
    {
        var lower = DisplayName.ToLowerInvariant();
        if (lower.Contains("shizuku")) return "\U0001F527";      // 🔧
        if (lower.Contains("gboard") || lower.Contains("keyboard")) return "\U0001F5AE"; // ⌨️
        if (lower.Contains("play") || lower.Contains("store")) return "\U0001F6D2"; // 🛒
        if (lower.Contains("settings")) return "\u2699\uFE0F";   // ⚙️
        if (lower.Contains("termux")) return "\U0001F4BB";        // 💻
        if (lower.Contains("vnc") || lower.Contains("remote")) return "\U0001F4F1"; // 📱
        if (lower.Contains("browser") || lower.Contains("chrome")) return "\U0001F310"; // 🌐
        if (lower.Contains("file") || lower.Contains("manager")) return "\U0001F4C1"; // 📁
        if (lower.Contains("music") || lower.Contains("audio")) return "\U0001F3B5"; // 🎵
        if (lower.Contains("video") || lower.Contains("player")) return "\U0001F3AC"; // 🎬
        if (lower.Contains("camera")) return "\U0001F4F7";        // 📷
        if (lower.Contains("gallery") || lower.Contains("photo")) return "\U0001F5BC"; // 🖼️
        if (lower.Contains("phone") || lower.Contains("dialer")) return "\U0001F4DE"; // 📞
        if (lower.Contains("message") || lower.Contains("sms")) return "\U0001F4AC"; // 💬
        if (lower.Contains("mail") || lower.Contains("gmail")) return "\u2709\uFE0F"; // ✉️
        if (lower.Contains("map") || lower.Contains("navigation")) return "\U0001F5FA"; // 🗺️
        if (lower.Contains("weather")) return "\U0001F324";        // 🌤️
        if (lower.Contains("clock") || lower.Contains("alarm")) return "\u23F0"; // ⏰
        if (lower.Contains("calculator")) return "\U0001F5A9";    // 🖩
        if (lower.Contains("note")) return "\U0001F4DD";          // 📝
        if (lower.Contains("calendar")) return "\U0001F4C5";      // 📅
        if (lower.Contains("contact")) return "\U0001F464";       // 👤
        if (lower.Contains("radio")) return "\U0001F4FB";         // 📻
        if (lower.Contains("game")) return "\U0001F3AE";          // 🎮
        if (lower.Contains("vpn")) return "\U0001F510";           // 🔐
        if (lower.Contains("keyboard") || lower.Contains("input")) return "\U0001F5AE"; // ⌨️
        return "\U0001F4E6";                                      // 📦
    }

    private string GetAppBgColor()
    {
        var lower = DisplayName.ToLowerInvariant();
        if (lower.Contains("shizuku")) return "#3D2A4A";
        if (lower.Contains("termux")) return "#1A2A1A";
        if (lower.Contains("play") || lower.Contains("store")) return "#1C2A22";
        if (lower.Contains("settings")) return "#2A2418";
        return "#1A3A5C";
    }

    public string KindLabel => Kind switch
    {
        BundledApkKind.Keyboard => "Bàn phím",
        BundledApkKind.KeyboardCustom => "Bàn phím",
        BundledApkKind.Store => "Cửa hàng",
        BundledApkKind.Settings => "Cài đặt",
        BundledApkKind.Tool => "Công cụ",
        _ => "APK",
    };

}