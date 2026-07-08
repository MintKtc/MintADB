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

    public static readonly string[] ShizukuPresetIds =
    [
        "shizuku_write_secure", "shizuku_write_settings", "shizuku_dump", "shizuku_read_logs",
        "shizuku_manage_ext_storage", "shizuku_usage", "shizuku_overlay", "shizuku_run_bg",
    ];

    public static IReadOnlyList<AppPermissionOption> CreateDefaults() =>
    [
        // ===== Thông báo =====
        Opt("notif_post", "Thông báo", "Hiển thị thông báo trên Android 13+", "Thông báo",
            PermissionGrantKind.Runtime, "android.permission.POST_NOTIFICATIONS", true),
        Opt("notif_boot", "Tự khởi động", "App chạy lại sau khi reboot máy", "Thông báo",
            PermissionGrantKind.Runtime, "android.permission.RECEIVE_BOOT_COMPLETED", true),
        Opt("notif_vibrate", "Rung", "Rung khi có thông báo", "Thông báo",
            PermissionGrantKind.Runtime, "android.permission.VIBRATE", false),
        Opt("notif_appop", "Gửi thông báo", "Cho phép app đăng ký kênh thông báo", "Thông báo",
            PermissionGrantKind.AppOp, "POST_NOTIFICATION", true),

        // ===== Chạy nền =====
        Opt("bg_fg", "Foreground service", "Chạy service nền có thông báo hệ thống", "Chạy nền",
            PermissionGrantKind.Runtime, "android.permission.FOREGROUND_SERVICE", true),
        Opt("bg_fg_sync", "FGS đồng bộ", "Đồng bộ dữ liệu ở foreground service", "Chạy nền",
            PermissionGrantKind.Runtime, "android.permission.FOREGROUND_SERVICE_DATA_SYNC", false),
        Opt("bg_fg_media", "FGS media", "Phát nhạc/video ở foreground service", "Chạy nền",
            PermissionGrantKind.Runtime, "android.permission.FOREGROUND_SERVICE_MEDIA_PLAYBACK", false),
        Opt("bg_fg_loc", "FGS vị trí", "Theo dõi vị trí ở foreground service", "Chạy nền",
            PermissionGrantKind.Runtime, "android.permission.FOREGROUND_SERVICE_LOCATION", false),
        Opt("bg_fg_phone", "FGS cuộc gọi", "Xử lý cuộc gọi ở foreground service", "Chạy nền",
            PermissionGrantKind.Runtime, "android.permission.FOREGROUND_SERVICE_PHONE_CALL", false),
        Opt("bg_fg_connect", "FGS kết nối", "Duy trì kết nối mạng ở foreground service", "Chạy nền",
            PermissionGrantKind.Runtime, "android.permission.FOREGROUND_SERVICE_CONNECTED_DEVICE", false),
        Opt("bg_fg_camera", "FGS camera", "Quay phim ở foreground service", "Chạy nền",
            PermissionGrantKind.Runtime, "android.permission.FOREGROUND_SERVICE_CAMERA", false),
        Opt("bg_fg_health", "FGS sức khỏe", "Theo dõi sức khỏe ở foreground service", "Chạy nền",
            PermissionGrantKind.Runtime, "android.permission.FOREGROUND_SERVICE_HEALTH", false),
        Opt("bg_fg_remote", "FGS từ xa", "Điều khiển thiết bị từ xa ở foreground service", "Chạy nền",
            PermissionGrantKind.Runtime, "android.permission.FOREGROUND_SERVICE_REMOTE_MESSAGING", false),
        Opt("bg_fg_special", "FGS đặc biệt", "Foreground service cho use-case đặc biệt", "Chạy nền",
            PermissionGrantKind.Runtime, "android.permission.FOREGROUND_SERVICE_SPECIAL_USE", false),
        Opt("bg_run", "Chạy nền", "Không bị hệ thống dừng khi ẩn app", "Chạy nền",
            PermissionGrantKind.AppOp, "RUN_IN_BACKGROUND", true),
        Opt("bg_run_any", "Chạy nền không giới hạn", "Cho phép chạy nền mọi lúc", "Chạy nền",
            PermissionGrantKind.AppOp, "RUN_ANY_IN_BACKGROUND", true),
        Opt("bg_wake", "Wake lock", "Giữ CPU/radio hoạt động khi cần", "Chạy nền",
            PermissionGrantKind.AppOp, "WAKE_LOCK", true),
        Opt("bg_fg_start", "Khởi chạy foreground", "Cho phép lên foreground từ nền", "Chạy nền",
            PermissionGrantKind.AppOp, "START_FOREGROUND", true),

        // ===== Pin & năng lượng =====
        Opt("bat_ignore_req", "Yêu cầu bỏ qua pin", "Quyền runtime để xin bỏ tối ưu pin", "Pin & năng lượng",
            PermissionGrantKind.Runtime, "android.permission.REQUEST_IGNORE_BATTERY_OPTIMIZATIONS", true),
        Opt("bat_ignore", "Bỏ qua tối ưu pin", "Không bị Doze/App Standby hạn chế", "Pin & năng lượng",
            PermissionGrantKind.AppOp, "IGNORE_BATTERY_OPTIMIZATIONS", true),
        Opt("bat_device_idle", "Whitelist Doze", "Thêm vào device idle whitelist", "Pin & năng lượng",
            PermissionGrantKind.AppOp, "RUN_IN_BACKGROUND", false),
        Opt("bat_temp_exempt", "Miễn nhiệt độ", "Không bị giới hạn khi máy nóng", "Pin & năng lượng",
            PermissionGrantKind.Runtime, "android.permission.DEVICE_POWER", false),

        // ===== MIUI & pin =====
        Opt("miui_autostart", "Tự khởi động MIUI", "Bật Auto Start — app không bị MIUI chặn khi tiết kiệm pin / tắt nền",
            "MIUI & pin", PermissionGrantKind.Miui, "autostart", true),

        // ===== Mạng =====
        Opt("net_inet", "Internet", "Truy cập mạng", "Mạng",
            PermissionGrantKind.Runtime, "android.permission.INTERNET", false),
        Opt("net_state", "Trạng thái mạng", "Biết khi có/không có mạng", "Mạng",
            PermissionGrantKind.Runtime, "android.permission.ACCESS_NETWORK_STATE", true),
        Opt("net_wifi", "Trạng thái Wi‑Fi", "Biết trạng thái Wi‑Fi", "Mạng",
            PermissionGrantKind.Runtime, "android.permission.ACCESS_WIFI_STATE", true),
        Opt("net_wifi_change", "Thay đổi Wi‑Fi", "Nhận biết thay đổi kết nối Wi‑Fi", "Mạng",
            PermissionGrantKind.Runtime, "android.permission.CHANGE_WIFI_STATE", false),
        Opt("net_nfc", "NFC", "Giao tiếp NFC", "Mạng",
            PermissionGrantKind.Runtime, "android.permission.NFC", false),
        Opt("net_bt", "Bluetooth", "Kết nối Bluetooth", "Mạng",
            PermissionGrantKind.Runtime, "android.permission.BLUETOOTH", false),
        Opt("net_bt_admin", "Bluetooth (quản lý)", "Quét/kết nối Bluetooth", "Mạng",
            PermissionGrantKind.Runtime, "android.permission.BLUETOOTH_ADMIN", false),
        Opt("net_bt_scan", "Quét Bluetooth", "Quét thiết bị Bluetooth (Android 12+)", "Mạng",
            PermissionGrantKind.Runtime, "android.permission.BLUETOOTH_SCAN", false),
        Opt("net_bt_connect", "Kết nối Bluetooth", "Kết nối thiết bị Bluetooth (Android 12+)", "Mạng",
            PermissionGrantKind.Runtime, "android.permission.BLUETOOTH_CONNECT", false),
        Opt("net_bt_advertise", "Quảng bá Bluetooth", "Phát hiện qua Bluetooth (Android 12+)", "Mạng",
            PermissionGrantKind.Runtime, "android.permission.BLUETOOTH_ADVERTISE", false),

        // ===== Vị trí =====
        Opt("loc_fine", "Vị trí chính xác", "GPS vị trí chi tiết", "Vị trí",
            PermissionGrantKind.Runtime, "android.permission.ACCESS_FINE_LOCATION", false),
        Opt("loc_coarse", "Vị trí gần đúng", "Vị trí theo cell/Wi‑Fi", "Vị trí",
            PermissionGrantKind.Runtime, "android.permission.ACCESS_COARSE_LOCATION", false),
        Opt("loc_bg", "Vị trí nền", "Theo dõi vị trí khi app ẩn", "Vị trí",
            PermissionGrantKind.Runtime, "android.permission.ACCESS_BACKGROUND_LOCATION", false),

        // ===== Camera & mic =====
        Opt("cam", "Camera", "Chụp ảnh / quay video", "Camera & mic",
            PermissionGrantKind.Runtime, "android.permission.CAMERA", false),
        Opt("mic", "Micro", "Ghi âm / gọi thoại", "Camera & mic",
            PermissionGrantKind.Runtime, "android.permission.RECORD_AUDIO", false),

        // ===== Lưu trữ =====
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
        Opt("stor_manage", "Quản lý bộ nhớ", "Toàn quyền đọc/ghi bộ nhớ (Android 11+)", "Lưu trữ",
            PermissionGrantKind.Runtime, "android.permission.MANAGE_EXTERNAL_STORAGE", false),
        Opt("stor_nearyby", "Thiết bị gần", "Truy cập thiết bị gần qua Wi‑Fi/Bluetooth", "Lưu trữ",
            PermissionGrantKind.Runtime, "android.permission.NEARBY_WIFI_DEVICES", false),

        // ===== Hiển thị =====
        Opt("overlay", "Hiển thị trên app", "Bubble, popup trên app khác", "Hiển thị",
            PermissionGrantKind.AppOp, "SYSTEM_ALERT_WINDOW", false),

        // ===== Khác =====
        Opt("alarm", "Báo thức chính xác", "Báo thức/hẹn giờ chính xác", "Khác",
            PermissionGrantKind.Runtime, "android.permission.SCHEDULE_EXACT_ALARM", false),
        Opt("alarm_use_exact", "Báo thức chính xác 2", "Báo thức chính xác (API 33+)", "Khác",
            PermissionGrantKind.Runtime, "android.permission.USE_EXACT_ALARM", false),
        Opt("fsi", "Thông báo toàn màn hình", "Cuộc gọi / báo động full screen", "Khác",
            PermissionGrantKind.Runtime, "android.permission.USE_FULL_SCREEN_INTENT", false),
        Opt("install", "Cài APK", "Cho phép app cài APK khác", "Khác",
            PermissionGrantKind.Runtime, "android.permission.REQUEST_INSTALL_PACKAGES", false),
        Opt("contacts", "Danh bạ", "Đọc danh bạ liên hệ", "Khác",
            PermissionGrantKind.Runtime, "android.permission.READ_CONTACTS", false),
        Opt("contacts_write", "Sửa danh bạ", "Thay đổi danh bạ liên hệ", "Khác",
            PermissionGrantKind.Runtime, "android.permission.WRITE_CONTACTS", false),
        Opt("phone", "Điện thoại", "Đọc trạng thái cuộc gọi/SIM", "Khác",
            PermissionGrantKind.Runtime, "android.permission.READ_PHONE_STATE", false),
        Opt("phone_call", "Gọi điện", "Thực hiện cuộc gọi", "Khác",
            PermissionGrantKind.Runtime, "android.permission.CALL_PHONE", false),
        Opt("phone_answer", "Trả lời cuộc gọi", "Trả lời cuộc gọi đến", "Khác",
            PermissionGrantKind.Runtime, "android.permission.ANSWER_PHONE_CALLS", false),
        Opt("phone_manage", "Quản lý cuộc gọi", "Quản lý danh sách cuộc gọi", "Khác",
            PermissionGrantKind.Runtime, "android.permission.MANAGE_OWN_CALLS", false),
        Opt("sms", "SMS", "Nhận tin nhắn SMS", "Khác",
            PermissionGrantKind.Runtime, "android.permission.RECEIVE_SMS", false),
        Opt("sms_send", "Gửi SMS", "Gửi tin nhắn SMS", "Khác",
            PermissionGrantKind.Runtime, "android.permission.SEND_SMS", false),
        Opt("sms_read", "Đọc SMS", "Đọc tin nhắn SMS", "Khác",
            PermissionGrantKind.Runtime, "android.permission.READ_SMS", false),
        Opt("usage", "Thống kê sử dụng", "Xem thời gian dùng app khác", "Khác",
            PermissionGrantKind.AppOp, "GET_USAGE_STATS", false),
        Opt("package_usage", "Truy cập package", "Biết app nào đang chạy", "Khác",
            PermissionGrantKind.Runtime, "android.permission.PACKAGE_USAGE_STATS", false),
        Opt("browser_history", "Lịch sử trình duyệt", "Đọc lịch sử trình duyệt mặc định", "Khác",
            PermissionGrantKind.Runtime, "android.permission.READ_HISTORY_BOOKMARKS", false),
        Opt("wallpaper", "Hình nền", "Thay đổi hình nền", "Khác",
            PermissionGrantKind.Runtime, "android.permission.SET_WALLPAPER", false),
        Opt("bind_widget", "Widget", "Cho phép app tạo widget màn hình chính", "Khác",
            PermissionGrantKind.Runtime, "android.permission.BIND_APPWIDGET", false),
        Opt("accessibility", "Trợ năng", "Quyền trợ năng (đọc màn hình)", "Khác",
            PermissionGrantKind.Runtime, "android.permission.BIND_ACCESSIBILITY_SERVICE", false),
        Opt("notification_listener", "Lắng nghe thông báo", "Đọc thông báo từ app khác", "Khác",
            PermissionGrantKind.Runtime, "android.permission.BIND_NOTIFICATION_LISTENER_SERVICE", false),
        Opt("device_admin", "Quản trị thiết bị", "Quyền admin thiết bị (khoá màn hình...)", "Khác",
            PermissionGrantKind.Runtime, "android.permission.BIND_DEVICE_ADMIN", false),

        // ===== Thêm mới =====
        Opt("call_log_read", "Đọc nhật ký cuộc gọi", "Xem lịch sử cuộc gọi", "Khác",
            PermissionGrantKind.Runtime, "android.permission.READ_CALL_LOG", false),
        Opt("call_log_write", "Ghi nhật ký cuộc gọi", "Thêm/sửa nhật ký cuộc gọi", "Khác",
            PermissionGrantKind.Runtime, "android.permission.WRITE_CALL_LOG", false),
        Opt("body_sensors", "Cảm biến cơ thể", "Đọc nhịp tim, SpO2, nhiệt độ", "Khác",
            PermissionGrantKind.Runtime, "android.permission.BODY_SENSORS", false),
        Opt("activity_recognition", "Nhận diện hoạt động", "Phát hiện đi bộ, chạy, xe đạp", "Khác",
            PermissionGrantKind.Runtime, "android.permission.ACTIVITY_RECOGNITION", false),
        Opt("phone_numbers", "Đọc số điện thoại", "Truy cập số điện thoại thiết bị", "Khác",
            PermissionGrantKind.Runtime, "android.permission.READ_PHONE_NUMBERS", false),
        Opt("process_outgoing", "Xử lý cuộc gọi đi", "拦截/thêm thông tin cuộc gọi đi", "Khác",
            PermissionGrantKind.Runtime, "android.permission.PROCESS_OUTGOING_CALLS", false),
        Opt("accept_handover", "Chuyển tiếp cuộc gọi", "Nhận cuộc gọi chuyển tiếp từ app khác", "Khác",
            PermissionGrantKind.Runtime, "android.permission.ACCEPT_HANDOVER", false),
        Opt("get_accounts", "Danh sách tài khoản", "Xem các tài khoản trên thiết bị", "Khác",
            PermissionGrantKind.Runtime, "android.permission.GET_ACCOUNTS", false),
        Opt("media_visual_selected", "Chọn ảnh (Android 14+)", "Chọn ảnh cụ thể từ thư viện", "Lưu trữ",
            PermissionGrantKind.Runtime, "android.permission.READ_MEDIA_VISUAL_USER_SELECTED", false),
        Opt("write_settings", "Ghi thiết lập hệ thống", "Thay đổi cài đặt hệ thống", "Khác",
            PermissionGrantKind.Runtime, "android.permission.WRITE_SETTINGS", false),
        Opt("clipboard_read", "Đọc clipboard", "Đọc nội dung clipboard", "Khác",
            PermissionGrantKind.AppOp, "READ_CLIPBOARD", false),
        Opt("clipboard_write", "Ghi clipboard", "Ghi nội dung clipboard", "Khác",
            PermissionGrantKind.AppOp, "WRITE_CLIPBOARD", false),
        Opt("wifi_scan", "Quét WiFi", "Quét mạng WiFi gần", "Mạng",
            PermissionGrantKind.AppOp, "WIFI_SCAN", false),
        Opt("monitor_location", "Theo dõi vị trí", "Giám sát vị trí liên tục", "Vị trí",
            PermissionGrantKind.AppOp, "MONITOR_LOCATION", false),
        Opt("monitor_high_power", "Theo dõi vị trí cao cấp", "Giám sát vị trí chính xác liên tục", "Vị trí",
            PermissionGrantKind.AppOp, "MONITOR_HIGH_POWER_LOCATION", false),
        Opt("turn_screen_on", "Bật màn hình", "Bật màn hình khi có thông báo/cuộc gọi", "Khác",
            PermissionGrantKind.Runtime, "android.permission.TURN_SCREEN_ON", false),
        Opt("control_audio", "Điều khiển âm thanh", "Điều khiển playback âm thanh", "Khác",
            PermissionGrantKind.AppOp, "CONTROL_AUDIO", false),
        Opt("take_audio_focus", "Lấy nét âm thanh", "Lấy audio focus từ app khác", "Khác",
            PermissionGrantKind.AppOp, "TAKE_AUDIO_FOCUS", false),
        Opt("restricted_settings", "Truy cập cài đặt hạn chế", "Truy cập cài đặt bị giới hạn trên Android 13+", "Khác",
            PermissionGrantKind.Runtime, "android.permission.ACCESS_RESTRICTED_SETTINGS", false),

        // ===== Shizuku (privileged) =====
        Opt("shizuku_write_secure", "WRITE_SECURE_SETTINGS", "Ghi thiết lập bảo mật (Shizuku)", "Shizuku (đặc quyền)",
            PermissionGrantKind.Shizuku, "android.permission.WRITE_SECURE_SETTINGS", false),
        Opt("shizuku_write_settings", "WRITE_SETTINGS", "Ghi thiết lập hệ thống (Shizuku)", "Shizuku (đặc quyền)",
            PermissionGrantKind.Shizuku, "android.permission.WRITE_SETTINGS", false),
        Opt("shizuku_dump", "DUMP", "Đọc dump hệ thống (Shizuku)", "Shizuku (đặc quyền)",
            PermissionGrantKind.Shizuku, "android.permission.DUMP", false),
        Opt("shizuku_read_logs", "READ_LOGS", "Đọc log hệ thống (Shizuku)", "Shizuku (đặc quyền)",
            PermissionGrantKind.Shizuku, "android.permission.READ_LOGS", false),
        Opt("shizuku_manage_ext_storage", "MANAGE_EXTERNAL_STORAGE", "Quản lý bộ nhớ ngoài (Shizuku)", "Shizuku (đặc quyền)",
            PermissionGrantKind.Shizuku, "android.permission.MANAGE_EXTERNAL_STORAGE", false),
        Opt("shizuku_notif_post", "POST_NOTIFICATIONS", "Cấp thông báo qua Shizuku", "Shizuku (đặc quyền)",
            PermissionGrantKind.Shizuku, "android.permission.POST_NOTIFICATIONS", false),
        Opt("shizuku_bg_loc", "Vị trí nền", "Vị trí nền qua Shizuku", "Shizuku (đặc quyền)",
            PermissionGrantKind.Shizuku, "android.permission.ACCESS_BACKGROUND_LOCATION", false),
        Opt("shizuku_usage", "Thống kê sử dụng", "Cấp quyền usage stats qua Shizuku", "Shizuku (đặc quyền)",
            PermissionGrantKind.Shizuku, "android:get_usage_stats", false),
        Opt("shizuku_overlay", "Hiển thị trên app", "Cấp overlay qua Shizuku", "Shizuku (đặc quyền)",
            PermissionGrantKind.Shizuku, "android:system_alert_window", false),
        Opt("shizuku_run_bg", "Chạy nền", "Cấp RUN_IN_BACKGROUND qua Shizuku", "Shizuku (đặc quyền)",
            PermissionGrantKind.Shizuku, "RUN_IN_BACKGROUND", false),
        Opt("shizuku_bat_ignore", "Bỏ qua tối ưu pin", "Cấp IGNORE_BATTERY qua Shizuku", "Shizuku (đặc quyền)",
            PermissionGrantKind.Shizuku, "android:request_ignore_battery_optimizations", false),
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