using MintADB.Wpf.Models;

namespace MintADB.Wpf.Services;

public static class AppPermissionCatalog
{
    public static readonly string[] BasicPresetIds =
    [
        "notif_post", "notif_boot", "notif_appop",
        "bg_fg", "bg_run", "bg_run_any", "bg_wake", "bg_fg_start",
        "bat_ignore_req", "bat_ignore", "miui_autostart", "net_state", "net_wifi",
    ];

    public static readonly string[] ChatPresetIds =
    [
        "notif_post", "notif_boot", "notif_appop", "bg_run", "bg_run_any", "bg_wake", "bg_fg_start",
        "bat_ignore_req", "bat_ignore", "net_state", "net_wifi",
    ];

    public static readonly string[] BatteryPresetIds =
    [
        "bat_ignore_req", "bat_ignore", "bg_run", "bg_run_any", "bg_wake", "miui_autostart",
    ];

    public static readonly string[] MiuiAutostartPresetIds =
    [
        "miui_autostart", "notif_boot", "bat_ignore_req", "bat_ignore", "bg_run", "bg_run_any",
    ];

    public static IReadOnlyList<AppPermissionOption> CreateDefaults() =>
    [
        Opt("notif_post", "Thông báo", "Hiển thị thông báo trên Android 13+", "Thông báo",
            PermissionGrantKind.Runtime, "android.permission.POST_NOTIFICATIONS", true),
        Opt("notif_boot", "Tự khởi động", "App chạy lại sau khi reboot máy", "Thông báo",
            PermissionGrantKind.Runtime, "android.permission.RECEIVE_BOOT_COMPLETED", true),
        Opt("notif_vibrate", "Rung", "Rung khi có thông báo", "Thông báo",
            PermissionGrantKind.Runtime, "android.permission.VIBRATE", false),
        Opt("notif_appop", "Gửi thông báo", "Cho phép app đăng ký kênh thông báo", "Thông báo",
            PermissionGrantKind.AppOp, "POST_NOTIFICATION", true),

        Opt("bg_fg", "Foreground service", "Chạy service nền có thông báo hệ thống", "Chạy nền",
            PermissionGrantKind.Runtime, "android.permission.FOREGROUND_SERVICE", true),
        Opt("bg_fg_sync", "FGS đồng bộ", "Đồng bộ dữ liệu ở foreground service", "Chạy nền",
            PermissionGrantKind.Runtime, "android.permission.FOREGROUND_SERVICE_DATA_SYNC", false),
        Opt("bg_fg_media", "FGS media", "Phát nhạc/video ở foreground service", "Chạy nền",
            PermissionGrantKind.Runtime, "android.permission.FOREGROUND_SERVICE_MEDIA_PLAYBACK", false),
        Opt("bg_fg_loc", "FGS vị trí", "Theo dõi vị trí ở foreground service", "Chạy nền",
            PermissionGrantKind.Runtime, "android.permission.FOREGROUND_SERVICE_LOCATION", false),
        Opt("bg_run", "Chạy nền", "Không bị hệ thống dừng khi ẩn app", "Chạy nền",
            PermissionGrantKind.AppOp, "RUN_IN_BACKGROUND", true),
        Opt("bg_run_any", "Chạy nền không giới hạn", "Cho phép chạy nền mọi lúc", "Chạy nền",
            PermissionGrantKind.AppOp, "RUN_ANY_IN_BACKGROUND", true),
        Opt("bg_wake", "Wake lock", "Giữ CPU/radio hoạt động khi cần", "Chạy nền",
            PermissionGrantKind.AppOp, "WAKE_LOCK", true),
        Opt("bg_fg_start", "Khởi chạy foreground", "Cho phép lên foreground từ nền", "Chạy nền",
            PermissionGrantKind.AppOp, "START_FOREGROUND", true),

        Opt("bat_ignore_req", "Yêu cầu bỏ qua pin", "Quyền runtime để xin bỏ tối ưu pin", "Pin & năng lượng",
            PermissionGrantKind.Runtime, "android.permission.REQUEST_IGNORE_BATTERY_OPTIMIZATIONS", true),
        Opt("bat_ignore", "Bỏ qua tối ưu pin", "Không bị Doze/App Standby hạn chế", "Pin & năng lượng",
            PermissionGrantKind.AppOp, "IGNORE_BATTERY_OPTIMIZATIONS", true),

        Opt("miui_autostart", "Tự khởi động MIUI", "Bật Auto Start — app không bị MIUI chặn khi tiết kiệm pin / tắt nền", "MIUI & pin",
            PermissionGrantKind.Miui, "autostart", true),

        Opt("net_inet", "Internet", "Truy cập mạng", "Mạng",
            PermissionGrantKind.Runtime, "android.permission.INTERNET", false),
        Opt("net_state", "Trạng thái mạng", "Biết khi có/không có mạng", "Mạng",
            PermissionGrantKind.Runtime, "android.permission.ACCESS_NETWORK_STATE", true),
        Opt("net_wifi", "Trạng thái Wi‑Fi", "Biết trạng thái Wi‑Fi", "Mạng",
            PermissionGrantKind.Runtime, "android.permission.ACCESS_WIFI_STATE", true),

        Opt("loc_fine", "Vị trí chính xác", "GPS vị trí chi tiết", "Vị trí",
            PermissionGrantKind.Runtime, "android.permission.ACCESS_FINE_LOCATION", false),
        Opt("loc_coarse", "Vị trí gần đúng", "Vị trí theo cell/Wi‑Fi", "Vị trí",
            PermissionGrantKind.Runtime, "android.permission.ACCESS_COARSE_LOCATION", false),
        Opt("loc_bg", "Vị trí nền", "Theo dõi vị trí khi app ẩn", "Vị trí",
            PermissionGrantKind.Runtime, "android.permission.ACCESS_BACKGROUND_LOCATION", false),

        Opt("cam", "Camera", "Chụp ảnh / quay video", "Camera & mic",
            PermissionGrantKind.Runtime, "android.permission.CAMERA", false),
        Opt("mic", "Micro", "Ghi âm / gọi thoại", "Camera & mic",
            PermissionGrantKind.Runtime, "android.permission.RECORD_AUDIO", false),

        Opt("stor_read", "Đọc bộ nhớ", "Đọc file trên bộ nhớ ngoài", "Lưu trữ",
            PermissionGrantKind.Runtime, "android.permission.READ_EXTERNAL_STORAGE", false),
        Opt("stor_write", "Ghi bộ nhớ", "Ghi file ra bộ nhớ ngoài", "Lưu trữ",
            PermissionGrantKind.Runtime, "android.permission.WRITE_EXTERNAL_STORAGE", false),
        Opt("stor_img", "Đọc ảnh", "Truy cập thư viện ảnh (Android 13+)", "Lưu trữ",
            PermissionGrantKind.Runtime, "android.permission.READ_MEDIA_IMAGES", false),
        Opt("stor_vid", "Đọc video", "Truy cập thư viện video", "Lưu trữ",
            PermissionGrantKind.Runtime, "android.permission.READ_MEDIA_VIDEO", false),
        Opt("stor_aud", "Đọc nhạc", "Truy cập thư viện nhạc", "Lưu trữ",
            PermissionGrantKind.Runtime, "android.permission.READ_MEDIA_AUDIO", false),

        Opt("overlay", "Hiển thị trên app", "Bubble, popup trên app khác", "Hiển thị",
            PermissionGrantKind.AppOp, "SYSTEM_ALERT_WINDOW", false),

        Opt("alarm", "Báo thức chính xác", "Báo thức/hẹn giờ chính xác", "Khác",
            PermissionGrantKind.Runtime, "android.permission.SCHEDULE_EXACT_ALARM", false),
        Opt("fsi", "Thông báo toàn màn hình", "Cuộc gọi / báo động full screen", "Khác",
            PermissionGrantKind.Runtime, "android.permission.USE_FULL_SCREEN_INTENT", false),
        Opt("install", "Cài APK", "Cho phép app cài APK khác", "Khác",
            PermissionGrantKind.Runtime, "android.permission.REQUEST_INSTALL_PACKAGES", false),
        Opt("contacts", "Danh bạ", "Đọc danh bạ liên hệ", "Khác",
            PermissionGrantKind.Runtime, "android.permission.READ_CONTACTS", false),
        Opt("phone", "Điện thoại", "Đọc trạng thái cuộc gọi/SIM", "Khác",
            PermissionGrantKind.Runtime, "android.permission.READ_PHONE_STATE", false),
        Opt("sms", "SMS", "Nhận tin nhắn SMS", "Khác",
            PermissionGrantKind.Runtime, "android.permission.RECEIVE_SMS", false),
        Opt("usage", "Thống kê sử dụng", "Xem thời gian dùng app khác", "Khác",
            PermissionGrantKind.AppOp, "GET_USAGE_STATS", false),
    ];

    private static AppPermissionOption Opt(
        string id, string label, string description, string group,
        PermissionGrantKind kind, string value, bool selected) =>
        new()
        {
            Id = id,
            Label = label,
            Description = description,
            Group = group,
            Kind = kind,
            Value = value,
            Selected = selected,
        };
}