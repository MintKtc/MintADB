using System.Text.RegularExpressions;
using MintADB.Wpf.Models;

namespace MintADB.Wpf.Services;

public sealed class HardwareInfoService(AdbService adb)
{
    private static readonly (string Ns, string Key, string Label)[] RefreshRateKeys =
    [
        ("system", "screen_refresh_rate", "current"),
        ("secure", "refresh_rate", "current"),
        ("system", "peak_refresh_rate", "peak"),
        ("global", "peak_refresh_rate", "peak"),
        ("system", "min_refresh_rate", "min"),
        ("global", "min_refresh_rate", "min"),
    ];

    private static readonly string[] BatteryTechPaths =
    [
        "/sys/class/power_supply/battery/technology",
        "/sys/class/power_supply/bms/technology",
    ];

    private static readonly string[] BatteryCurrentUahPaths =
    [
        "/sys/class/power_supply/battery/charge_counter",
        "/sys/class/power_supply/battery/charge_now",
        "/sys/class/power_supply/bms/charge_counter",
        "/sys/class/power_supply/bms/charge_now",
    ];

    private static readonly string[] BatteryMaxUahPaths =
    [
        "/sys/class/power_supply/battery/charge_full",
        "/sys/class/power_supply/bms/charge_full",
        "/sys/class/power_supply/battery/charge_full_design",
        "/sys/class/power_supply/bms/charge_full_design",
    ];

    private static readonly string[] PanelTechProps =
    [
        "ro.vendor.display.type",
        "ro.display.type",
        "ro.vendor.display.panel",
        "ro.screen.type",
        "persist.vendor.disp.panel",
        "ro.vendor.panel.display",
        "ro.vendor.display.lcd_type",
        "ro.boot.panel",
    ];

    public async Task<BatteryInfoResult> GetBatteryInfoAsync(string serial, CancellationToken ct = default)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var dumpsys = await adb.ShellAsync("dumpsys battery", serial, ct);
        if (dumpsys.Ok)
            ParseBatteryDumpsys(dumpsys.Output, map);

        var technology = map.GetValueOrDefault("technology");
        if (string.IsNullOrWhiteSpace(technology))
        {
            foreach (var path in BatteryTechPaths)
            {
                var tech = await adb.ShellAsync($"cat {path}", serial, ct);
                var val = tech.Output.Trim();
                if (tech.Ok && val.Length > 0 && !val.Contains("No such file", StringComparison.OrdinalIgnoreCase))
                {
                    technology = val;
                    break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(technology))
        {
            foreach (var prop in new[] { "ro.battery.technology", "persist.vendor.battery.technology" })
            {
                var val = await adb.GetPropAsync(serial, prop, ct);
                if (!string.IsNullOrWhiteSpace(val))
                {
                    technology = val;
                    break;
                }
            }
        }

        int? levelPercent = null;
        if (map.TryGetValue("level", out var lv) && map.TryGetValue("scale", out var sc)
            && int.TryParse(lv, out var l) && int.TryParse(sc, out var s) && s > 0)
            levelPercent = l * 100 / s;

        var (currentMah, maxMah) = await ReadBatteryCapacityAsync(serial, map, levelPercent, ct);

        string? level = levelPercent is not null ? $"{levelPercent}%" : null;

        string? status = map.TryGetValue("status", out var st) && int.TryParse(st, out var si)
            ? DescribeBatteryStatus(si) : null;

        string? health = map.TryGetValue("health", out var hl) && int.TryParse(hl, out var hi)
            ? DescribeBatteryHealth(hi) : null;

        string? voltage = map.TryGetValue("voltage", out var v) && int.TryParse(v, out var mv)
            ? $"{mv / 1000.0:F2} V" : null;

        string? temperature = map.TryGetValue("temperature", out var t) && int.TryParse(t, out var t10)
            ? $"{t10 / 10.0:F1} °C" : null;

        string? current = map.TryGetValue("current", out var c) && int.TryParse(c, out var ma)
            ? $"{Math.Abs(ma)} mA" : null;

        return new BatteryInfoResult
        {
            LevelPercent = levelPercent,
            Technology = technology,
            Status = status,
            Health = health,
            CurrentMah = currentMah,
            MaxMah = maxMah,
            Voltage = voltage,
            Temperature = temperature,
            Current = current,
            MetricsText = FormatMetrics(
            [
                ("Công nghệ pin", technology),
                ("Dung lượng hiện tại", FormatMah(currentMah)),
                ("Dung lượng tối đa", FormatMah(maxMah)),
                ("Mức pin", level),
                ("Trạng thái", status),
                ("Sức khỏe pin", health),
                ("Điện áp", voltage),
                ("Nhiệt độ", temperature),
                ("Dòng điện", current),
            ]),
        };
    }

    private async Task<(int? Current, int? Max)> ReadBatteryCapacityAsync(
        string serial,
        Dictionary<string, string> batteryMap,
        int? levelPercent,
        CancellationToken ct)
    {
        int? currentUah = null;
        int? maxUah = null;

        if (batteryMap.TryGetValue("charge counter", out var cc) && int.TryParse(cc, out var ccVal) && ccVal > 0)
            currentUah = ccVal;

        foreach (var path in BatteryCurrentUahPaths)
        {
            var val = await ReadSysfsIntAsync(serial, path, ct);
            if (val is > 0)
            {
                currentUah = val;
                break;
            }
        }

        foreach (var path in BatteryMaxUahPaths)
        {
            var val = await ReadSysfsIntAsync(serial, path, ct);
            if (val is > 0)
            {
                maxUah = val;
                break;
            }
        }

        if (maxUah is null or <= 0)
        {
            var design = await ReadSysfsIntAsync(serial, "/sys/class/power_supply/battery/charge_full_design", ct)
                         ?? await ReadSysfsIntAsync(serial, "/sys/class/power_supply/bms/charge_full_design", ct);
            if (design is > 0)
                maxUah = design;
        }

        if (maxUah is null or <= 0)
        {
            var model = await adb.GetPropAsync(serial, "ro.product.model", ct);
            var device = await adb.GetPropAsync(serial, "ro.product.device", ct);
            var market = await adb.GetPropAsync(serial, "ro.product.marketname", ct);
            var combined = $"{model} {device} {market}".ToUpperInvariant();

            // Known battery capacities (uAh)
            maxUah = combined switch
            {
                // Xiaomi 2025 flagships
                var m when m.Contains("DASH") || m.Contains("2602BRT") => 9_000_000,  // Redmi Turbo 5 series - 9000mAh
                var m when m.Contains("SHENG") || m.Contains("25010PN") => 6_000_000, // Xiaomi 15 series
                var m when m.Contains("XUANYUAN") || m.Contains("25030F") => 5_800_000, // Xiaomi 15 Ultra

                // Xiaomi 2024 flagships
                var m when m.Contains("AURORA") || m.Contains("24031PN") => 5_300_000, // Xiaomi 14 Ultra
                var m when m.Contains("SHENNONG") || m.Contains("23116PN") => 4_880_000, // Xiaomi 14 Pro
                var m when m.Contains("HOUJI") || m.Contains("24015RN") => 4_610_000, // Xiaomi 14

                // Redmi K series
                var m when m.Contains("VERMEER") || m.Contains("23013RK") => 5_000_000, // Redmi K70
                var m when m.Contains("MANET") || m.Contains("2304FPN") => 5_000_000, // Redmi K70 Pro
                var m when m.Contains("SOCRATES") || m.Contains("2210132") => 5_000_000, // Redmi K60
                var m when m.Contains("MARBLE") || m.Contains("2210131") => 5_000_000, // Redmi Note 12 Turbo

                // Redmi Note series
                var m when m.Contains("PHOENIX") || m.Contains("2211131") => 5_000_000, // Redmi Note 12
                var m when m.Contains("SAGITarius") || m.Contains("23021RA98") => 5_000_000, // Redmi Note 13
                var m when m.Contains("MONDRIAN") || m.Contains("23116PN") => 5_000_000, // Redmi Note 13 Pro

                // POCO
                var m when m.Contains("MONDRIAN") => 5_000_000, // POCO X6 Pro
                var m when m.Contains("VERMEER") => 5_000_000, // POCO F6

                // Samsung
                var m when m.Contains("SM-S928") => 5_000_000, // S24 Ultra
                var m when m.Contains("SM-S938") => 5_000_000, // S25 Ultra
                var m when m.Contains("SM-A55") || m.Contains("SM-A54") => 5_000_000, // A55/A54

                // OnePlus
                var m when m.Contains("CPH265") => 6_000_000, // OnePlus 13
                var m when m.Contains("CPH258") => 5_400_000, // OnePlus 12

                // Pixel
                var m when m.Contains("KOMODO") || m.Contains("PIXEL 9") => 5_060_000, // Pixel 9
                var m when m.Contains("CACTUS") || m.Contains("PIXEL 8") => 4_575_000, // Pixel 8

                _ => null
            };
        }

        // If still no max, estimate from charge_counter and level
        if (maxUah is null or <= 0 && currentUah is > 0 && levelPercent is > 0 && levelPercent < 95)
        {
            var estimated = currentUah.Value * 100 / levelPercent.Value;
            if (estimated is > 2_000_000 and <= 15_000_000)
                maxUah = estimated;
        }

        if (currentUah is > 0 && maxUah is > 0 && currentUah > maxUah * 1.5)
            currentUah = null;

        if ((currentUah is null or <= 0) && maxUah is > 0 && levelPercent is > 0)
            currentUah = maxUah.Value * levelPercent.Value / 100;

        if (maxUah is null or <= 0 && currentUah is > 0 && levelPercent is > 0)
        {
            var estimated = currentUah.Value * 100 / levelPercent.Value;
            if (estimated is > 2000000 and <= 10000000)
                maxUah = estimated;
        }

        return (UahToMah(currentUah), UahToMah(maxUah));
    }

    private async Task<int?> ReadSysfsIntAsync(string serial, string path, CancellationToken ct)
    {
        var r = await adb.ShellAsync($"cat {path}", serial, ct);
        var text = r.Output.Trim();
        if (!r.Ok || text.Length == 0
            || text.Contains("No such file", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Permission denied", StringComparison.OrdinalIgnoreCase))
            return null;

        return int.TryParse(text, out var val) ? val : null;
    }

    private static int? UahToMah(int? uah) =>
        uah is > 0 ? (int)Math.Round(uah.Value / 1000.0) : null;

    private static string? FormatMah(int? mah) =>
        mah is > 0 ? $"{mah:N0} mAh" : null;

    public async Task<DisplayInfoResult> GetDisplayInfoAsync(string serial, CancellationToken ct = default)
    {
        string? resolution = null;
        string? dpi = null;

        var size = await adb.ShellAsync("wm size", serial, ct);
        if (size.Ok)
            resolution = ParseWmSize(size.Output);

        var densityResult = await adb.ShellAsync("wm density", serial, ct);
        if (densityResult.Ok)
            dpi = ParseWmDensity(densityResult.Output);

        if (string.IsNullOrWhiteSpace(dpi))
        {
            var dpiProp = await adb.GetPropAsync(serial, "ro.sf.lcd_density", ct);
            if (!string.IsNullOrWhiteSpace(dpiProp)) dpi = $"{dpiProp} dpi";
        }

        float? currentHz = null;
        float? peakHz = null;
        float? minHz = null;

        foreach (var (ns, key, kind) in RefreshRateKeys)
        {
            var r = await adb.ShellAsync($"settings get {ns} {key}", serial, ct);
            var hz = ParseHzValue(r.Output.Trim());
            if (hz is null) continue;

            switch (kind)
            {
                case "current" when currentHz is null:
                    currentHz = hz;
                    break;
                case "peak":
                    peakHz = hz;
                    break;
                case "min":
                    minHz = hz;
                    break;
            }
        }

        var display = await adb.ShellAsync("dumpsys display", serial, ct);
        if (display.Ok)
        {
            var fromDumpsys = ExtractRefreshRates(display.Output);
            if (currentHz is null && fromDumpsys.ActiveHz is not null)
                currentHz = fromDumpsys.ActiveHz;
            if (peakHz is null && fromDumpsys.PeakHz is not null)
                peakHz = fromDumpsys.PeakHz;
            if (resolution is null && fromDumpsys.Resolution is not null)
                resolution = fromDumpsys.Resolution;
        }

        var (panelTech, panelName) = await DetectPanelTechnologyAsync(serial, display.Ok ? display.Output : null, ct);

        var refresh = currentHz ?? peakHz;
        var refreshText = refresh is not null ? $"{refresh:0.#} Hz" : null;
        var peakText = peakHz is not null && peakHz != refresh ? $"{peakHz:0.#} Hz" : null;
        var minText = minHz is not null ? $"{minHz:0.#} Hz" : null;

        return new DisplayInfoResult
        {
            Resolution = resolution,
            Dpi = dpi,
            RefreshHz = refresh,
            PeakHz = peakHz,
            MinHz = minHz,
            PanelTech = panelTech,
            PanelName = panelName,
            MetricsText = FormatMetrics(
            [
                ("Độ phân giải", resolution),
                ("DPI", dpi),
                ("Tần số quét", refreshText),
                ("Hz tối đa", peakText),
                ("Hz tối thiểu", minText),
                ("Công nghệ tấm nền", panelTech),
                ("Panel", panelName),
            ]),
        };
    }

    private async Task<(string? Normalized, string? Raw)> DetectPanelTechnologyAsync(
        string serial, string? displayDumpsys, CancellationToken ct)
    {
        var fromProps = new List<string>();

        // Kiểm tra các prop đặc trưng OLED
        var oledWp = await adb.GetPropAsync(serial, "ro.boot.oled_wp", ct);
        var screenType = await adb.GetPropAsync(serial, "ro.display.screen_type", ct);
        if (!string.IsNullOrWhiteSpace(oledWp) || screenType == "1")
            fromProps.Add("OLED (system)");

        foreach (var prop in PanelTechProps)
        {
            var val = await adb.GetPropAsync(serial, prop, ct);
            if (!string.IsNullOrWhiteSpace(val))
                fromProps.Add(val);
        }

        var props = await adb.ShellAsync(
            "getprop | grep -iE 'display|panel|oled|lcd|amoled|screen_type'", serial, ct);
        if (props.Ok)
        {
            foreach (var line in props.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                var m = Regex.Match(line, @"\[.*?\]:\s*\[(.*?)\]");
                if (m.Success && !string.IsNullOrWhiteSpace(m.Groups[1].Value))
                    fromProps.Add(m.Groups[1].Value);
            }
        }

        // Ưu tiên getprop, fallback dumpsys
        string? bestNormalized = null;
        string? bestRaw = null;
        var bestScore = -1;

        foreach (var raw in fromProps)
        {
            var normalized = NormalizePanelTech(raw);
            if (normalized is null) continue;
            var score = ScorePanelTech(normalized);
            if (score > bestScore)
            {
                bestScore = score;
                bestNormalized = normalized;
                bestRaw = raw;
            }
        }

        if (bestNormalized is null && !string.IsNullOrWhiteSpace(displayDumpsys))
        {
            foreach (var line in displayDumpsys.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                var t = line.Trim();
                if (ContainsPanelKeyword(t))
                {
                    var normalized = NormalizePanelTech(t);
                    if (normalized is null) continue;
                    var score = ScorePanelTech(normalized);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestNormalized = normalized;
                        bestRaw = t;
                    }
                }
            }
        }

        bestRaw = SanitizePanelRaw(bestRaw, bestNormalized);

        return (bestNormalized, bestRaw);
    }

    private static bool ContainsPanelKeyword(string text) =>
        text.Contains("oled", StringComparison.OrdinalIgnoreCase)
        || text.Contains("amoled", StringComparison.OrdinalIgnoreCase)
        || text.Contains("lcd", StringComparison.OrdinalIgnoreCase)
        || text.Contains("ips", StringComparison.OrdinalIgnoreCase)
        || text.Contains("ltpo", StringComparison.OrdinalIgnoreCase)
        || text.Contains("miniled", StringComparison.OrdinalIgnoreCase)
        || text.Contains("mini led", StringComparison.OrdinalIgnoreCase);

    private static string? NormalizePanelTech(string raw)
    {
        var u = raw.ToUpperInvariant();

        if (u.Contains("LTPO") && u.Contains("AMOLED")) return "LTPO AMOLED";
        if (u.Contains("LTPO") && u.Contains("OLED")) return "LTPO OLED";
        if (u.Contains("AMOLED") || u.Contains("AM OLED") || u.Contains("AM-OLED")) return "AMOLED";
        if (u.Contains("SUPER AMOLED")) return "Super AMOLED";
        if (u.Contains("OLED")) return "OLED";
        if (u.Contains("MINI LED") || u.Contains("MINILED")) return "Mini-LED LCD";
        if (u.Contains("IPS")) return "IPS LCD";
        if (u.Contains("TFT") || u.Contains("LCD")) return "LCD";

        return null;
    }

    private static string? SanitizePanelRaw(string? raw, string? normalized)
    {
        if (string.IsNullOrWhiteSpace(raw)) return normalized;

        if (Regex.IsMatch(raw, @"\bm[A-Z]"))
            return normalized;

        if (raw.Length <= 80) return raw;

        var m = Regex.Match(raw, @"(?:panel|display)\s*(?:type|technology|name|model)?\s*[=:]\s*(\w[\w\s.\-/]*)", RegexOptions.IgnoreCase);
        if (m.Success) return m.Groups[1].Value;

        foreach (var part in raw.Split([',', ';', '\t'], StringSplitOptions.TrimEntries))
        {
            if (ContainsPanelKeyword(part))
                return part;
        }

        return normalized;
    }

    private static int ScorePanelTech(string tech) => tech switch
    {
        "LTPO AMOLED" => 120,
        "Super AMOLED" => 115,
        "AMOLED" => 110,
        "LTPO OLED" => 105,
        "OLED" => 100,
        "Mini-LED LCD" => 60,
        "IPS LCD" => 50,
        "LCD" => 40,
        _ => 0,
    };

    private static void ParseBatteryDumpsys(string output, Dictionary<string, string> map)
    {
        // Handle both single-line and multi-line formats
        var normalized = output.Replace("  ", "\n");
        foreach (var line in normalized.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var t = line.Trim();
            var idx = t.IndexOf(':');
            if (idx <= 0) continue;
            var key = t[..idx].Trim();
            var value = t[(idx + 1)..].Trim();
            if (!string.IsNullOrEmpty(key))
                map[key] = value;
        }
    }

    private static string? ParseWmSize(string output)
    {
        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var m = Regex.Match(line, @"(\d+)\s*[xX×]\s*(\d+)");
            if (m.Success)
                return $"{m.Groups[1].Value} × {m.Groups[2].Value}";
        }
        return null;
    }

    private static string? ParseWmDensity(string output)
    {
        var m = Regex.Match(output, @"(\d{2,4})");
        return m.Success ? $"{m.Groups[1].Value} dpi" : null;
    }

    private static float? ParseHzValue(string raw)
    {
        if (raw is "null" or "") return null;
        if (!float.TryParse(raw, out var hz)) return null;
        if (hz is > 0 and < 1) hz *= 100;
        return hz is > 0 and <= 360 ? hz : null;
    }

    private static (float? ActiveHz, float? PeakHz, string? Resolution) ExtractRefreshRates(string output)
    {
        float? active = null;
        float? peak = null;
        string? resolution = null;
        var allRates = new List<float>();

        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var t = line.Trim();

            var res = Regex.Match(t, @"(\d{3,4})\s*[xX×]\s*(\d{3,4})");
            if (res.Success && resolution is null)
                resolution = $"{res.Groups[1].Value} × {res.Groups[2].Value}";

            foreach (Match m in Regex.Matches(t, @"(?:refreshRate|refresh|fps)[=:\s]+(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase))
            {
                if (float.TryParse(m.Groups[1].Value, out var hz) && hz is > 0 and <= 360)
                {
                    allRates.Add(hz);
                    if (t.Contains("active", StringComparison.OrdinalIgnoreCase)
                        || t.Contains("mActive", StringComparison.OrdinalIgnoreCase))
                        active = hz;
                }
            }

            foreach (Match m in Regex.Matches(t, @"(\d+(?:\.\d+)?)\s*Hz", RegexOptions.IgnoreCase))
            {
                if (float.TryParse(m.Groups[1].Value, out var hz) && hz is > 0 and <= 360)
                    allRates.Add(hz);
            }
        }

        if (allRates.Count > 0)
            peak = allRates.Max();

        if (active is null && allRates.Count > 0)
            active = allRates.First();

        return (active, peak, resolution);
    }

    private static string FormatMetrics((string Label, string? Value)[] items)
    {
        var lines = items
            .Where(i => !string.IsNullOrWhiteSpace(i.Value))
            .Select(i => $"{i.Label}: {i.Value}");
        var text = string.Join(Environment.NewLine, lines);
        return string.IsNullOrEmpty(text) ? "Không đọc được chỉ số." : text;
    }

    private static string DescribeBatteryStatus(int status) => status switch
    {
        2 => "Đang sạc",
        3 => "Đang xả",
        4 => "Không sạc",
        5 => "Đầy pin",
        _ => $"Mã {status}",
    };

    private static string DescribeBatteryHealth(int health) => health switch
    {
        2 => "Tốt",
        3 => "Quá nóng",
        4 => "Hỏng / thay pin",
        5 => "Quá áp",
        6 => "Lỗi không xác định",
        7 => "Quá lạnh",
        _ => $"Mã {health}",
    };

    public async Task<DeviceInfoResult> GetDeviceInfoAsync(string serial, CancellationToken ct = default)
    {
        async Task<string?> P(string prop)
        {
            var val = await adb.GetPropAsync(serial, prop, ct);
            return CleanProp(val);
        }

        var manufacturer = await P("ro.product.manufacturer");
        var brand = await P("ro.product.brand");
        var model = await P("ro.product.model");
        var marketName = await P("ro.product.marketname")
                         ?? await P("ro.product.odm.marketname")
                         ?? await P("ro.vendor.oplus.market.name");
        var deviceCodename = await P("ro.product.device");
        var androidVersion = await P("ro.build.version.release");
        var sdkRaw = await P("ro.build.version.sdk");
        int? sdkInt = int.TryParse(sdkRaw, out var sdk) ? sdk : null;
        var securityPatch = await P("ro.build.version.security_patch");
        var build = await P("ro.build.display.id");
        var buildId = await P("ro.build.id");
        var hyper = await P("ro.mi.os.version.name");
        var miui = await P("ro.miui.ui.version.name");
        var oneUi = await P("ro.build.version.oneui");
        var colorOs = await P("ro.build.version.oplusrom");
        var region = await P("ro.miui.region") ?? await P("ro.product.locale.region");
        var serialNo = await P("ro.serialno");
        var socModel = await P("ro.soc.model");
        var socMfr = await P("ro.soc.manufacturer");
        var chipset = socModel is not null ? (socMfr is not null ? $"{socMfr} {socModel}" : socModel) : null;

        string? osName;
        string? osVersion;
        if (!string.IsNullOrEmpty(hyper))
        {
            osName = "HyperOS";
            osVersion = hyper;
        }
        else if (!string.IsNullOrEmpty(miui))
        {
            osName = "MIUI";
            osVersion = miui;
        }
        else if (!string.IsNullOrEmpty(oneUi))
        {
            osName = "One UI";
            osVersion = oneUi;
        }
        else if (!string.IsNullOrEmpty(colorOs))
        {
            osName = "ColorOS";
            osVersion = colorOs;
        }
        else
        {
            osName = "Android";
            osVersion = null;
        }

        var romType = DescribeRomType(manufacturer, brand, region, build);
        var androidLabel = FormatAndroidLabel(androidVersion, sdkInt);
        var displayName = marketName ?? model;

        return new DeviceInfoResult
        {
            Manufacturer = manufacturer,
            Brand = brand,
            Model = model,
            DeviceCodename = deviceCodename,
            MarketName = displayName,
            AndroidVersion = androidVersion,
            SdkInt = sdkInt,
            SecurityPatch = securityPatch,
            OsName = osName,
            OsVersion = osVersion,
            RomRegion = region,
            RomBuild = build,
            RomType = romType,
            Serial = serialNo,
            MetricsText = FormatMetrics(
            [
                ("Hãng", FormatBrand(manufacturer, brand)),
                ("Model", displayName),
                ("Mã máy", deviceCodename),
                ("Chipset", chipset),
                ("Phiên bản Android", androidLabel),
                ("Bản vá bảo mật", securityPatch),
                ("Hệ điều hành", FormatOs(osName, osVersion)),
                ("Loại ROM", romType),
                ("Vùng ROM", region),
                ("Bản ROM", build),
                ("Build ID", buildId),
                ("Serial", serialNo),
            ]),
        };
    }

    private static string? CleanProp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value is "null" or "unknown")
            return null;
        return value.Trim();
    }

    private static string? FormatBrand(string? manufacturer, string? brand)
    {
        if (!string.IsNullOrEmpty(brand) && !string.IsNullOrEmpty(manufacturer)
            && !brand.Equals(manufacturer, StringComparison.OrdinalIgnoreCase))
            return $"{brand} ({manufacturer})";
        return brand ?? manufacturer;
    }

    private static string? FormatOs(string? osName, string? osVersion)
    {
        if (string.IsNullOrEmpty(osName)) return null;
        return string.IsNullOrEmpty(osVersion) ? osName : $"{osName} {osVersion}";
    }

    private static string? FormatAndroidLabel(string? version, int? sdk)
    {
        if (string.IsNullOrEmpty(version)) return sdk is not null ? $"API {sdk}" : null;
        return sdk is not null ? $"Android {version} (API {sdk})" : $"Android {version}";
    }

    private static string? DescribeRomType(string? manufacturer, string? brand, string? region, string? build)
    {
        var m = (manufacturer ?? "").ToLowerInvariant();
        var b = (brand ?? "").ToLowerInvariant();
        var isXiaomi = m is "xiaomi" or "redmi" or "poco" || b is "xiaomi" or "redmi" or "poco";

        if (isXiaomi)
        {
            var blob = $"{region}{build}".ToLowerInvariant();
            if (blob.Contains("cn") || blob.Contains("china")
                || (build?.EndsWith("CNXM", StringComparison.OrdinalIgnoreCase) ?? false))
                return "ROM China";
            if (!string.IsNullOrEmpty(region))
                return $"ROM {region}";
            return "ROM Global / EU";
        }

        if (!string.IsNullOrEmpty(build))
        {
            if (build.Contains("userdebug", StringComparison.OrdinalIgnoreCase))
                return "ROM userdebug";
            if (build.Contains("eng", StringComparison.OrdinalIgnoreCase))
                return "ROM engineering";
        }

        return "ROM stock";
    }

    // ── Storage Info ──

    public async Task<StorageInfoResult> GetStorageInfoAsync(string serial, CancellationToken ct = default)
    {
        var result = new StorageInfoResult();

        // Internal storage
        var df = await adb.ShellAsync("df /data | tail -1", serial, ct);
        if (df.Ok)
        {
            var parts = df.Output.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 4)
            {
                if (long.TryParse(parts[1], out var totalKb)) result.InternalTotal = totalKb * 1024;
                if (long.TryParse(parts[2], out var usedKb)) result.InternalUsed = usedKb * 1024;
                if (long.TryParse(parts[3], out var availKb)) result.InternalAvail = availKb * 1024;
            }
        }

        // SD card
        var sd = await adb.ShellAsync("df /storage/sdcard1 2>/dev/null | tail -1", serial, ct);
        if (sd.Ok && !sd.Output.Contains("No such file"))
        {
            var parts = sd.Output.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 4)
            {
                if (long.TryParse(parts[1], out var totalKb)) result.SdTotal = totalKb * 1024;
                if (long.TryParse(parts[2], out var usedKb)) result.SdUsed = usedKb * 1024;
                if (long.TryParse(parts[3], out var availKb)) result.SdAvail = availKb * 1024;
            }
        }

        // Storage type
        var emulated = await adb.ShellAsync("ls /storage/emulated/0/ 2>/dev/null | head -5", serial, ct);
        result.HasEmulatedStorage = emulated.Ok && !string.IsNullOrWhiteSpace(emulated.Output);

        return result;
    }

    // ── RAM Info ──

    public async Task<RamInfoResult> GetRamInfoAsync(string serial, CancellationToken ct = default)
    {
        var result = new RamInfoResult();

        var meminfo = await adb.ShellAsync("cat /proc/meminfo", serial, ct);
        if (meminfo.Ok)
        {
            foreach (var line in meminfo.Output.Split('\n', '\r'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("MemTotal:"))
                    result.TotalKb = ParseMemInfoValue(trimmed);
                else if (trimmed.StartsWith("MemFree:"))
                    result.FreeKb = ParseMemInfoValue(trimmed);
                else if (trimmed.StartsWith("MemAvailable:"))
                    result.AvailableKb = ParseMemInfoValue(trimmed);
                else if (trimmed.StartsWith("Buffers:"))
                    result.BuffersKb = ParseMemInfoValue(trimmed);
                else if (trimmed.StartsWith("Cached:"))
                    result.CachedKb = ParseMemInfoValue(trimmed);
                else if (trimmed.StartsWith("SwapTotal:"))
                    result.SwapTotalKb = ParseMemInfoValue(trimmed);
                else if (trimmed.StartsWith("SwapFree:"))
                    result.SwapFreeKb = ParseMemInfoValue(trimmed);
            }
        }

        // Used = Total - Available
        if (result.TotalKb > 0 && result.AvailableKb > 0)
            result.UsedKb = result.TotalKb - result.AvailableKb;

        return result;
    }

    private static long ParseMemInfoValue(string line)
    {
        var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && long.TryParse(parts[1], out var kb))
            return kb * 1024; // Convert KB to bytes
        return 0;
    }

    // ── CPU Info ──

    public async Task<CpuInfoResult> GetCpuInfoAsync(string serial, CancellationToken ct = default)
    {
        var result = new CpuInfoResult();

        // CPU info from /proc/cpuinfo
        var cpuinfo = await adb.ShellAsync("cat /proc/cpuinfo", serial, ct);
        if (cpuinfo.Ok)
        {
            var cores = 0;
            foreach (var line in cpuinfo.Output.Split('\n', '\r'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("processor:"))
                    cores++;
            }
            result.CoreCount = cores;
        }

        // CPU frequencies
        var freqs = await adb.ShellAsync("cat /sys/devices/system/cpu/cpu*/cpufreq/scaling_max_freq 2>/dev/null", serial, ct);
        if (freqs.Ok)
        {
            var maxFreqs = new List<long>();
            foreach (var line in freqs.Output.Split('\n', '\r'))
            {
                var trimmed = line.Trim();
                if (long.TryParse(trimmed, out var freq) && freq > 0)
                    maxFreqs.Add(freq);
            }
            if (maxFreqs.Count > 0)
                result.MaxFreqKhz = maxFreqs.Max();
        }

        // CPU model from props - try multiple sources
        result.SocModel = await adb.GetPropAsync(serial, "ro.soc.model", ct);
        result.SocManufacturer = await adb.GetPropAsync(serial, "ro.soc.manufacturer", ct);

        if (string.IsNullOrWhiteSpace(result.SocModel))
            result.SocModel = await adb.GetPropAsync(serial, "ro.hardware", ct);

        if (string.IsNullOrWhiteSpace(result.SocModel))
            result.SocModel = await adb.GetPropAsync(serial, "ro.board.platform", ct);

        // Try to get a human-readable SoC name
        if (!string.IsNullOrWhiteSpace(result.SocModel))
        {
            var socUpper = result.SocModel.ToUpperInvariant();
            result.SocModel = socUpper switch
            {
                "MT6991" => "MediaTek Dimensity 9400",
                "MT6989" => "MediaTek Dimensity 9300",
                "MT6985" => "MediaTek Dimensity 9200",
                "SM8750" => "Snapdragon 8 Elite",
                "SM8650" => "Snapdragon 8 Gen 3",
                "SM8550" => "Snapdragon 8 Gen 2",
                "SM8450" => "Snapdragon 8 Gen 1",
                "SM8350" => "Snapdragon 888",
                "SM8250" => "Snapdragon 865",
                _ => result.SocModel
            };
        }

        return result;
    }

    // ── GPU Info ──

    public async Task<GpuInfoResult> GetGpuInfoAsync(string serial, CancellationToken ct = default)
    {
        var result = new GpuInfoResult();

        // GPU renderer from SurfaceFlinger
        var renderer = await adb.ShellAsync("dumpsys SurfaceFlinger | grep -i 'GLES' | head -1", serial, ct);
        if (renderer.Ok)
        {
            var match = Regex.Match(renderer.Output, @"GLES:\s*(.+?)(?:,\s*OpenGL|$)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var gpuName = match.Groups[1].Value.Trim();
                // Clean up - remove "ARM," prefix if present
                gpuName = Regex.Replace(gpuName, @"^ARM,\s*", "", RegexOptions.IgnoreCase);
                result.Renderer = gpuName;
            }
            else
            {
                // Try simpler match
                var match2 = Regex.Match(renderer.Output, @"GLES:\s*(.+)", RegexOptions.IgnoreCase);
                if (match2.Success)
                    result.Renderer = match2.Groups[1].Value.Trim().Split(',')[0].Trim();
            }
        }

        // GPU frequency from various paths
        var gpuFreqPaths = new[]
        {
            "/sys/class/kgsl/kgsl-3d0/max_gpuclk",
            "/sys/kernel/gpu/gpu_clock",
            "/sys/devices/platform/gpu/max_freq",
            "/sys/class/devfreq/gpufreq/max_freq",
        };

        foreach (var path in gpuFreqPaths)
        {
            var gpuFreq = await adb.ShellAsync($"cat {path} 2>/dev/null", serial, ct);
            if (gpuFreq.Ok && long.TryParse(gpuFreq.Output.Trim(), out var freq) && freq > 0)
            {
                result.MaxFreqHz = freq;
                break;
            }
        }

        // GPU model from props
        result.GpuModel = await adb.GetPropAsync(serial, "ro.hardware.gpu", ct);
        if (string.IsNullOrWhiteSpace(result.GpuModel))
            result.GpuModel = await adb.GetPropAsync(serial, "ro.board.platform", ct);

        // Try to get a human-readable GPU name from renderer
        if (!string.IsNullOrWhiteSpace(result.Renderer))
        {
            var gpuLower = result.Renderer.ToLowerInvariant();
            if (gpuLower.Contains("mali") && gpuLower.Contains("g925"))
                result.Renderer = "Mali-G925 Immortalis";
            else if (gpuLower.Contains("mali") && gpuLower.Contains("g720"))
                result.Renderer = "Mali-G720";
            else if (gpuLower.Contains("adreno") && gpuLower.Contains("750"))
                result.Renderer = "Adreno 750";
            else if (gpuLower.Contains("adreno") && gpuLower.Contains("740"))
                result.Renderer = "Adreno 740";
            else if (gpuLower.Contains("adreno") && gpuLower.Contains("730"))
                result.Renderer = "Adreno 730";
        }

        return result;
    }

    // ── Touch Info ──

    public async Task<TouchInfoResult> GetTouchInfoAsync(string serial, CancellationToken ct = default)
    {
        var result = new TouchInfoResult();

        // Touch screen info
        var touch = await adb.ShellAsync("dumpsys input | grep -A5 'Touch Input' | head -10", serial, ct);
        if (touch.Ok)
        {
            var match = Regex.Match(touch.Output, @"Touch Input.*?size\s*[:=]\s*(\d+)", RegexOptions.Singleline);
            if (match.Success)
                result.TouchScreenSize = match.Groups[1].Value;
        }

        // Touch sampling rate
        var sampling = await adb.ShellAsync("cat /sys/class/touch/touch_dev/report_rate 2>/dev/null || cat /sys/class/input/input*/report_rate 2>/dev/null", serial, ct);
        if (sampling.Ok && int.TryParse(sampling.Output.Trim(), out var rate))
            result.SamplingRateHz = rate;

        return result;
    }
}