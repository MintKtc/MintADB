namespace MintADB.Wpf.Helpers;

public enum LogLevel
{
    Normal,
    Success,
    Error,
    Warning,
    Running,
    Header,
    Info,
}

public static class LogClassifier
{
    public static LogLevel Classify(string msg)
    {
        if (string.IsNullOrWhiteSpace(msg)) return LogLevel.Normal;

        if (msg.Contains("[FAIL]", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("Lỗi:", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("error", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("failed", StringComparison.OrdinalIgnoreCase))
            return LogLevel.Error;

        if (msg.Contains("[OK]", StringComparison.OrdinalIgnoreCase)
            || msg.StartsWith("Quét xong", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("thành công", StringComparison.OrdinalIgnoreCase))
            return LogLevel.Success;

        if (msg.Contains("Đang chạy", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("Đang quét", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("Đang thu thập", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("Đang mở", StringComparison.OrdinalIgnoreCase))
            return LogLevel.Running;

        if (msg.StartsWith("---", StringComparison.Ordinal)
            || msg.StartsWith("$ ", StringComparison.Ordinal))
            return LogLevel.Header;

        if (msg.Contains("[INFO]", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("(trống)", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("(không có output)", StringComparison.OrdinalIgnoreCase))
            return LogLevel.Info;

        if (msg.Contains("[WARN]", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("cần reboot", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("Xác nhận", StringComparison.OrdinalIgnoreCase))
            return LogLevel.Warning;

        return LogLevel.Normal;
    }
}