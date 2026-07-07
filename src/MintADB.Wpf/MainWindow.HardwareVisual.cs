using System.Windows.Media;
using MintADB.Wpf.Models;

namespace MintADB.Wpf;

public partial class MainWindow
{
    private const double BatteryFillMaxHeight = 102;

    private void ResetBatteryVisual()
    {
        BatteryLevelBig.Text = "--%";
        BatteryFill.Height = 0;
        BatteryFill.Background = (Brush)FindResource("MacGreenBrush");
        BatteryTechText.Text = "Công nghệ pin";
        BatteryCapacityLine.Text = "Dung lượng: —";
        BatteryMaxCapacityLine.Text = "Dung lượng tối đa: —";
        BatteryStatusLine.Text = "Trạng thái: —";
    }

    private void UpdateBatteryVisual(BatteryInfoResult info)
    {
        if (info.LevelPercent is int pct)
        {
            BatteryLevelBig.Text = $"{pct}%";
            BatteryFill.Height = BatteryFillMaxHeight * pct / 100.0;
            BatteryFill.Background = pct switch
            {
                < 15 => new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x3A)),
                < 35 => new SolidColorBrush(Color.FromRgb(0xFF, 0x9F, 0x0A)),
                _ => (Brush)FindResource("MacGreenBrush"),
            };
        }
        else
        {
            BatteryLevelBig.Text = "--%";
            BatteryFill.Height = 0;
        }

        BatteryTechText.Text = string.IsNullOrWhiteSpace(info.Technology)
            ? "Công nghệ pin: —"
            : info.Technology;

        BatteryCapacityLine.Text = info.CurrentMah is > 0 && info.MaxMah is > 0
            ? $"Dung lượng: {info.CurrentMah:N0} / {info.MaxMah:N0} mAh"
            : info.MaxMah is > 0
                ? $"Dung lượng tối đa: {info.MaxMah:N0} mAh"
                : "Dung lượng: —";

        BatteryMaxCapacityLine.Text = info.MaxMah is > 0
            ? $"Dung lượng tối đa: {info.MaxMah:N0} mAh"
            : "Dung lượng tối đa: —";

        if (info.CurrentMah is > 0 && info.MaxMah is > 0)
        {
            var healthPct = (int)Math.Round(info.CurrentMah.Value * 100.0 / info.MaxMah.Value);
            BatteryMaxCapacityLine.Text += $" ({healthPct}% so với thiết kế)";
        }

        var statusParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(info.Status))
            statusParts.Add(info.Status);
        if (!string.IsNullOrWhiteSpace(info.Health))
            statusParts.Add(info.Health);
        BatteryStatusLine.Text = statusParts.Count > 0
            ? string.Join(" · ", statusParts)
            : "Trạng thái: —";
    }

    private void ResetDisplayVisual()
    {
        DisplayResBig.Text = "—×—";
        DisplayHzBig.Text = "— Hz";
        DisplayPanelText.Text = "Tấm nền";
        DisplayPanelBadge.Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x25, 0x40));
        DisplayPanelText.Foreground = new SolidColorBrush(Color.FromRgb(0xB3, 0x88, 0xFF));
        DisplayPanelDetailLine.Text = "Tấm nền: —";
        DisplayDpiLine.Text = "DPI: —";
        DisplayHzLine.Text = "Tần số quét: —";
    }

    private void UpdateDisplayVisual(DisplayInfoResult info)
    {
        DisplayResBig.Text = string.IsNullOrWhiteSpace(info.Resolution)
            ? "—×—"
            : info.Resolution.Replace(" ", "");

        DisplayHzBig.Text = info.RefreshHz is float hz
            ? $"{hz:0.#} Hz"
            : "— Hz";

        var panel = info.PanelTech ?? "—";
        DisplayPanelText.Text = panel;
        ApplyPanelBadgeStyle(panel);

        DisplayPanelDetailLine.Text = !string.IsNullOrWhiteSpace(info.PanelName)
            ? $"Tấm nền: {info.PanelName}"
            : "Tấm nền: —";

        DisplayDpiLine.Text = string.IsNullOrWhiteSpace(info.Dpi)
            ? "DPI: —"
            : info.Dpi.StartsWith("DPI", StringComparison.OrdinalIgnoreCase) ? info.Dpi : $"DPI: {info.Dpi}";

        if (info.RefreshHz is float current)
        {
            var extra = new List<string> { $"{current:0.#} Hz" };
            if (info.PeakHz is float peak && Math.Abs(peak - current) > 0.5f)
                extra.Add($"max {peak:0.#}");
            if (info.MinHz is float min)
                extra.Add($"min {min:0.#}");
            DisplayHzLine.Text = $"Tần số quét: {string.Join(" · ", extra)}";
        }
        else
            DisplayHzLine.Text = "Tần số quét: —";
    }

    private void ApplyPanelBadgeStyle(string panelTech)
    {
        var u = panelTech.ToUpperInvariant();
        if (u.Contains("OLED") || u.Contains("AMOLED"))
        {
            DisplayPanelBadge.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x1F, 0x3D));
            DisplayPanelText.Foreground = new SolidColorBrush(Color.FromRgb(0xCE, 0x93, 0xD8));
            return;
        }

        if (u.Contains("LCD") || u.Contains("IPS") || u.Contains("MINI"))
        {
            DisplayPanelBadge.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x2A, 0x33));
            DisplayPanelText.Foreground = new SolidColorBrush(Color.FromRgb(0x90, 0xCA, 0xF9));
            return;
        }

        DisplayPanelBadge.Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x25, 0x40));
        DisplayPanelText.Foreground = new SolidColorBrush(Color.FromRgb(0xB3, 0x88, 0xFF));
    }

    private void ResetDeviceVisual()
    {
        DeviceBrandGlyph.Text = "—";
        DeviceBrandBig.Text = "—";
        DeviceModelLine.Text = "Model: —";
        DeviceAndroidBig.Text = "Android —";
        DeviceOsText.Text = "Hệ điều hành";
        DeviceRomLine.Text = "ROM: —";
        DeviceBuildLine.Text = "Build: —";
        DeviceCodenameLine.Text = "—";
        DeviceOsBadge.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x22, 0x10));
        DeviceOsText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xCC, 0x80));
    }

    private void UpdateDeviceVisual(DeviceInfoResult info)
    {
        var brand = info.Brand ?? info.Manufacturer ?? "—";
        DeviceBrandGlyph.Text = brand.Length >= 2
            ? brand[..2].ToUpperInvariant()
            : brand.ToUpperInvariant();
        DeviceBrandBig.Text = brand;

        var model = info.MarketName ?? info.Model;
        DeviceModelLine.Text = string.IsNullOrWhiteSpace(model) ? "Model: —" : model;

        DeviceCodenameLine.Text = info.DeviceCodename ?? info.Model ?? "—";

        if (!string.IsNullOrWhiteSpace(info.AndroidVersion))
        {
            DeviceAndroidBig.Text = info.SdkInt is int sdk
                ? $"Android {info.AndroidVersion}"
                : $"Android {info.AndroidVersion}";
            if (info.SdkInt is int api)
                DeviceAndroidBig.Text += $" · API {api}";
        }
        else
            DeviceAndroidBig.Text = info.SdkInt is int sdkOnly ? $"API {sdkOnly}" : "Android —";

        var osLabel = string.IsNullOrWhiteSpace(info.OsVersion)
            ? info.OsName ?? "Hệ điều hành"
            : $"{info.OsName} {info.OsVersion}";
        DeviceOsText.Text = osLabel;
        ApplyDeviceOsBadgeStyle(info.OsName, info.Brand ?? info.Manufacturer);

        var romParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(info.RomType))
            romParts.Add(info.RomType);
        if (!string.IsNullOrWhiteSpace(info.RomRegion))
            romParts.Add(info.RomRegion);
        DeviceRomLine.Text = romParts.Count > 0
            ? $"ROM: {string.Join(" · ", romParts)}"
            : "ROM: —";

        DeviceBuildLine.Text = string.IsNullOrWhiteSpace(info.RomBuild)
            ? "Build: —"
            : $"Build: {info.RomBuild}";
    }

    private void ApplyDeviceOsBadgeStyle(string? osName, string? brand)
    {
        var os = (osName ?? "").ToUpperInvariant();
        var b = (brand ?? "").ToLowerInvariant();

        if (os.Contains("HYPER") || os.Contains("MIUI") || b is "xiaomi" or "redmi" or "poco")
        {
            DeviceOsBadge.Background = new SolidColorBrush(Color.FromRgb(0x3D, 0x28, 0x10));
            DeviceOsText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x40));
            return;
        }

        if (os.Contains("ONE UI") || b is "samsung")
        {
            DeviceOsBadge.Background = new SolidColorBrush(Color.FromRgb(0x12, 0x2A, 0x45));
            DeviceOsText.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0xB5, 0xF6));
            return;
        }

        if (os.Contains("COLOR") || b is "oppo" or "realme" or "oneplus")
        {
            DeviceOsBadge.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x33, 0x1A));
            DeviceOsText.Foreground = new SolidColorBrush(Color.FromRgb(0x81, 0xC7, 0x84));
            return;
        }

        DeviceOsBadge.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x22, 0x10));
        DeviceOsText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xCC, 0x80));
    }
}