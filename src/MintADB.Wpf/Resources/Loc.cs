using System.Windows;

namespace MintADB.Wpf.Resources;

/// <summary>
/// Lấy chuỗi theo ngôn ngữ hiện tại: ưu tiên XAML DynamicResource dictionary,
/// fallback <see cref="Strings"/> (resx), rồi fallback cứng.
/// </summary>
public static class Loc
{
    public static string Get(string key, string? fallback = null)
    {
        try
        {
            if (Application.Current?.TryFindResource(key) is string s && s.Length > 0)
                return s;
        }
        catch
        {
            // ignore
        }

        try
        {
            var fromResx = Strings.ResourceManager.GetString(key, Strings.Culture);
            if (!string.IsNullOrEmpty(fromResx))
                return fromResx;
        }
        catch
        {
            // ignore
        }

        return fallback ?? key;
    }

    public static string Format(string key, params object[] args)
    {
        var fmt = Get(key);
        try { return string.Format(fmt, args); }
        catch { return fmt; }
    }
}
