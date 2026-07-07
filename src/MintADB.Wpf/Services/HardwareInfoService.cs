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

        if ((currentUah is null or <= 0) && maxUah is > 0 && levelPercent is > 0)
            currentUah = maxUah.Value * levelPercent.Value / 100;

        if ((maxUah is null or <= 0) && currentUah is > 0 && levelPercent is > 0)
            maxUah = currentUah.Value * 100 / levelPercent.Value;

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
        var candidates = new List<string>();

        foreach (var prop in PanelTechProps)
        {
            var val = await adb.GetPropAsync(serial, prop, ct);
            if (!string.IsNullOrWhiteSpace(val))
                candidates.Add(val);
        }

        var props = await adb.ShellAsync(
            "getprop | grep -iE 'display|panel|oled|lcd|amoled'", serial, ct);
        if (props.Ok)
        {
            foreach (var line in props.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                var m = Regex.Match(line, @"\[.*?\]:\s*\[(.*?)\]");
                if (m.Success && !string.IsNullOrWhiteSpace(m.Groups[1].Value))
                    candidates.Add(m.Groups[1].Value);
            }
        }

        if (!string.IsNullOrWhiteSpace(displayDumpsys))
        {
            foreach (var line in displayDumpsys.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                var t = line.Trim();
                if (ContainsPanelKeyword(t))
                    candidates.Add(t);
            }
        }

        string? bestNormalized = null;
        string? bestRaw = null;
        var bestScore = -1;
        foreach (var raw in candidates)
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
        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var t = line.Trim();
            var idx = t.IndexOf(':');
            if (idx <= 0) continue;
            map[t[..idx].Trim()] = t[(idx + 1)..].Trim();
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
}