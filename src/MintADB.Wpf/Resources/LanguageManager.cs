using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace MintADB.Wpf.Resources;

public static class LanguageManager
{
    private const string SettingsFile = "language_settings.json";
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MintADB",
        SettingsFile);

    public static event Action<string>? LanguageChanged;

    /// <summary>Mã UI: "vi" | "en"</summary>
    public static string CurrentLanguage { get; private set; } = "vi";

    static LanguageManager()
    {
        LoadSettings();
    }

    /// <summary>Áp dụng ngôn ngữ đã lưu (gọi khi app start, trước khi show UI).</summary>
    public static void ApplySavedLanguage()
    {
        ApplyLanguage(CurrentLanguage, save: false, force: true);
    }

    public static void SetLanguage(string lang)
    {
        lang = Normalize(lang);
        if (CurrentLanguage == lang)
        {
            // Vẫn force refresh dictionary (tránh UI lệch)
            ApplyLanguage(lang, save: true, force: true);
            return;
        }

        ApplyLanguage(lang, save: true, force: true);
    }

    public static void ToggleLanguage() =>
        SetLanguage(CurrentLanguage == "vi" ? "en" : "vi");

    private static void ApplyLanguage(string lang, bool save, bool force)
    {
        lang = Normalize(lang);
        var changed = !string.Equals(CurrentLanguage, lang, StringComparison.Ordinal);
        CurrentLanguage = lang;

        var culture = CultureFor(lang);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        // resx: en-US / default (vi)
        Strings.Culture = lang == "en"
            ? CultureInfo.GetCultureInfo("en-US")
            : CultureInfo.GetCultureInfo("vi-VN");

        if (Application.Current is not null)
            SwapStringsDictionary(lang);

        if (save)
            SaveSettings();

        if (force || changed)
            LanguageChanged?.Invoke(lang);
    }

    private static void SwapStringsDictionary(string lang)
    {
        var app = Application.Current;
        if (app is null) return;

        var path = lang == "en"
            ? "Resources/Strings.en-US.xaml"
            : "Resources/Strings.xaml";

        ResourceDictionary dict;
        try
        {
            dict = new ResourceDictionary { Source = new Uri(path, UriKind.Relative) };
        }
        catch
        {
            dict = new ResourceDictionary { Source = new Uri("Resources/Strings.xaml", UriKind.Relative) };
        }

        var merged = app.Resources.MergedDictionaries;

        // Gỡ mọi dictionary Strings cũ (tránh chồng key / index sai)
        for (var i = merged.Count - 1; i >= 0; i--)
        {
            var src = merged[i].Source?.OriginalString ?? "";
            if (src.Contains("Strings", StringComparison.OrdinalIgnoreCase))
                merged.RemoveAt(i);
        }

        // Chèn đầu: ưu tiên hơn theme khi tìm key string
        merged.Insert(0, dict);
    }

    private static string Normalize(string lang) =>
        lang.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en" : "vi";

    private static CultureInfo CultureFor(string lang) =>
        lang == "en"
            ? CultureInfo.GetCultureInfo("en-US")
            : CultureInfo.GetCultureInfo("vi-VN");

    private static void LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<LanguageSettings>(json);
            if (settings?.Language is { Length: > 0 } lang)
                CurrentLanguage = Normalize(lang);
        }
        catch
        {
            // default vi
        }
    }

    private static void SaveSettings()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (dir is not null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(
                new LanguageSettings { Language = CurrentLanguage },
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // ignore
        }
    }

    private sealed class LanguageSettings
    {
        public string Language { get; set; } = "vi";
    }
}
