using MintADB.Wpf.Models;

namespace MintADB.Wpf.Services;

public static class DeviceSpoofCatalog
{
    public static readonly string[] SpoofPropKeys =
    [
        "ro.product.model",
        "ro.product.device",
        "ro.product.brand",
        "ro.product.manufacturer",
        "ro.product.name",
        "ro.product.marketname",
        "ro.product.board",
        "ro.build.product",
        "ro.build.fingerprint",
        "ro.build.description",
        "ro.hardware",
        "ro.boot.hardware",
        "ro.board.platform",
        "ro.soc.model",
        "ro.soc.manufacturer",
    ];

    public static IReadOnlyList<DeviceSpoofProfile> Profiles { get; } =
    [
        // ── 2025–2026 · Snapdragon 8 Elite ──
        EliteProfile(
            "s25u",
            "Samsung Galaxy S25 Ultra",
            "Flagship 2025 — SD8 Elite, game thường nhận 120 FPS",
            "SM-S938B", "pa3q", "samsung", "samsung", "pa3qxxx", "Galaxy S25 Ultra", "pa3q",
            "samsung/pa3qxxx/pa3q:15/AP3A.240905.015/S938BXXU1AYA3:user/release-keys",
            "pa3qxxx-user 15 AP3A.240905.015 S938BXXU1AYA3 release-keys"),

        EliteProfile(
            "mi15u",
            "Xiaomi 15 Ultra",
            "Flagship 2025 — Leica, profile quốc tế cao cấp",
            "25010PN30G", "xuanyuan", "Xiaomi", "Xiaomi", "xuanyuan", "Xiaomi 15 Ultra", "xuanyuan",
            "Xiaomi/xuanyuan/xuanyuan:15/AQ3A.240829.003/OS2.0.5.0.VOAMIXM:user/release-keys",
            "xuanyuan-user 15 AQ3A.240829.003 OS2.0.5.0.VOAMIXM release-keys"),

        EliteProfile(
            "op13",
            "OnePlus 13",
            "Flagship 2025 — OxygenOS, tương thích game global",
            "CPH2653", "OP6113L1", "OnePlus", "OnePlus", "OP6113L1", "OnePlus 13", "OP6113L1",
            "OnePlus/OP6113L1/OP6113L1:15/TP1A.220905.001/R.1e8f3_2f2f2f:user/release-keys",
            "OP6113L1-user 15 TP1A.220905.001 R.1e8f3 release-keys"),

        EliteProfile(
            "rog9",
            "ASUS ROG Phone 9 Pro",
            "Gaming 2025 — 185Hz, game nhận 144 FPS",
            "AI2501", "ASUSAI2501", "asus", "asus", "WWAI2501", "ROG Phone 9 Pro", "ASUSAI2501",
            "asus/WWAI2501/ASUSAI2501:15/AQ3A.240829.003/35.1810.1810.220-0:user/release-keys",
            "qssi_64-user 15 AQ3A.240829.003 35.1810.1810.220-0 release-keys"),

        EliteProfile(
            "rm10p",
            "Red Magic 10 Pro",
            "Gaming 2025 — active cooling, profile FPS cao",
            "NX789J", "NX789J", "nubia", "nubia", "NX789J", "Red Magic 10 Pro", "NX789J",
            "nubia/NX789J/NX789J:15/AQ3A.240812.002/RedMagicOS10.0.15_NX789J:user/release-keys",
            "qssi_64-user 15 AQ3A.240812.002 RedMagicOS10.0.15_NX789J release-keys"),

        EliteProfile(
            "px9pxl",
            "Google Pixel 9 Pro XL",
            "Flagship Google 2024 — Tensor G4, game hỗ trợ tốt",
            "Pixel 9 Pro XL", "komodo", "google", "Google", "komodo", "Pixel 9 Pro XL", "komodo",
            "google/komodo/komodo:15/AP31.240617.009/12850291:user/release-keys",
            "komodo-user 15 AP31.240617.009 12850291 release-keys",
            socModel: "Tensor G4", socMfr: "Google", platform: "zumapro"),

        EliteProfile(
            "vivo_x200u",
            "vivo X200 Ultra",
            "Flagship camera 2025 — SD8 Elite quốc tế",
            "V2405A", "PD2405", "vivo", "vivo", "PD2405", "vivo X200 Ultra", "PD2405",
            "vivo/PD2405/PD2405:15/TP1A.220624.014/PD2405_A_15.0.12.0.W10:user/release-keys",
            "PD2405-user 15 TP1A.220624.014 PD2405_A_15.0.12.0 release-keys"),

        EliteProfile(
            "iqoo13",
            "iQOO 13",
            "Gaming vivo 2025 — SD8 Elite, 144Hz",
            "V2408A", "PD2408", "iQOO", "vivo", "PD2408", "iQOO 13", "PD2408",
            "iQOO/PD2408/PD2408:15/TP1A.220624.014/PD2408_A_15.0.10.0.W10:user/release-keys",
            "PD2408-user 15 TP1A.220624.014 PD2408_A_15.0.10.0 release-keys"),

        EliteProfile(
            "findx8u",
            "OPPO Find X8 Ultra",
            "Flagship OPPO 2025 — ColorOS quốc tế",
            "CPH2651", "OP6063L1", "OPPO", "OPPO", "OP6063L1", "Find X8 Ultra", "OP6063L1",
            "OPPO/OP6063L1/OP6063L1:15/TP1A.220905.001/CPH2651_15.0.0.500:user/release-keys",
            "OP6063L1-user 15 TP1A.220905.001 CPH2651_15.0.0.500 release-keys"),

        EliteProfile(
            "magic7p",
            "Honor Magic 7 Pro",
            "Flagship Honor 2025 — MagicOS",
            "BVL-AN16", "BVL", "HONOR", "HONOR", "BVL-AN16", "Magic 7 Pro", "BVL",
            "HONOR/BVL/BVL-AN16:15/HONORBVL-AN16/7.0.0.100:user/release-keys",
            "BVL-user 15 HONORBVL-AN16 7.0.0.100 release-keys"),

        // ── 2024 · Snapdragon 8 Gen 3 ──
        Gen3Profile(
            "s24u",
            "Samsung Galaxy S24 Ultra",
            "SD8 Gen 3 — unlock 120 FPS PUBG / Genshin",
            "SM-S928B", "e3q", "samsung", "samsung", "e3qxxx", "Galaxy S24 Ultra", "e3q",
            "samsung/e3qxxx/e3q:14/UP1A.231005.007/S928BXXU1AWM9:user/release-keys",
            "e3qxxx-user 14 UP1A.231005.007 S928BXXU1AWM9 release-keys"),

        Gen3Profile(
            "mi14u",
            "Xiaomi 14 Ultra",
            "SD8 Gen 3 — Leica, profile flagship",
            "24031PN0DC", "aurora", "Xiaomi", "Xiaomi", "aurora", "Xiaomi 14 Ultra", "aurora",
            "Xiaomi/aurora/aurora:14/UKQ1.230804.001/V816.0.6.0.UNACNXM:user/release-keys",
            "aurora-user 14 UKQ1.230804.001 V816.0.6.0.UNACNXM release-keys"),

        Gen3Profile(
            "mi14pro",
            "Xiaomi 14 Pro",
            "SD8 Gen 3 — profile quốc tế",
            "23116PN5BC", "shennong", "Xiaomi", "Xiaomi", "shennong", "Xiaomi 14 Pro", "shennong",
            "Xiaomi/shennong/shennong:14/UKQ1.230804.001/V816.0.4.0.UNBCNXM:user/release-keys",
            "shennong-user 14 UKQ1.230804.001 V816.0.4.0.UNBCNXM release-keys"),

        Gen3Profile(
            "op12",
            "OnePlus 12",
            "SD8 Gen 3 — tương thích game quốc tế",
            "CPH2581", "OP5929L1", "OnePlus", "OnePlus", "OP5929L1", "OnePlus 12", "OP5929L1",
            "OnePlus/OP5929L1/OP5929L1:14/UKQ1.230924.001/R.40e8f2_1e1e1e1:user/release-keys",
            "OP5929L1-user 14 UKQ1.230924.001 R.40e8f2 release-keys"),

        Gen3Profile(
            "rog8",
            "ASUS ROG Phone 8 Pro",
            "Gaming 2024 — 165Hz, 120/144 FPS",
            "AI2401", "AI2401", "asus", "asus", "AI2401", "ROG Phone 8 Pro", "AI2401",
            "asus/AI2401/AI2401:14/UKQ1.230924.001/AI2401_34.0610.0610.248:user/release-keys",
            "AI2401-user 14 UKQ1.230924.001 AI2401_34 release-keys"),

        Gen3Profile(
            "rm9sp",
            "Red Magic 9S Pro",
            "Gaming 2024 — SD8 Gen 3 leading version",
            "NX769J", "NX769J", "nubia", "nubia", "NX769J", "Red Magic 9S Pro", "NX769J",
            "nubia/NX769J/NX769J:14/UKQ1.230917.001/RedMagicOS9.0.12_NX769J:user/release-keys",
            "NX769J-user 14 UKQ1.230917.001 RedMagicOS9.0.12 release-keys"),

        // ── Fallback ──
        Gen3Profile(
            "sd8g2",
            "Samsung Galaxy S23 Ultra",
            "SD8 Gen 2 — nhẹ, ít xung đột ROM cũ",
            "SM-S918B", "dm3q", "samsung", "samsung", "dm3qxxx", "Galaxy S23 Ultra", "dm3q",
            "samsung/dm3qxxx/dm3q:14/UP1A.231005.007/S918BXXU3AWM9:user/release-keys",
            "dm3qxxx-user 14 UP1A.231005.007 S918BXXU3AWM9 release-keys",
            socModel: "SM8550"),

        // ── Game-Specific Profiles ──
        GameProfile(
            "genshin_120",
            "Genshin Impact 120 FPS",
            "Genshin Impact — unlock 120 FPS trên thiết bị hỗ trợ",
            "ro.genshin.fps_cap=120",
            "ro.genshin.render_quality=2"),

        GameProfile(
            "pubg_120",
            "PUBG Mobile 120 FPS",
            "PUBG Mobile — unlock 90/120 FPS + HDR",
            "ro.pubg.fps=120",
            "ro.pubg.graphics=hdr"),

        GameProfile(
            "codm_120",
            "Call of Duty Mobile 120 FPS",
            "COD Mobile — unlock max FPS + Ultra graphics",
            "ro.codm.fps=120",
            "ro.codm.quality=ultra"),

        GameProfile(
            "mlbb_120",
            "Mobile Legends 120 FPS",
            "MLBB — unlock 120 FPS mode",
            "ro.mlbb.fps=120"),

        GameProfile(
            "apex_90",
            "Apex Legends Mobile 90 FPS",
            "Apex Legends — unlock 90 FPS + HDR",
            "ro.apex.fps=90"),

        GameProfile(
            "fortnite_120",
            "Fortnite 120 FPS",
            "Fortnite — unlock 120 FPS trên flagship",
            "ro.fortnite.fps=120"),
    ];

    private static DeviceSpoofProfile EliteProfile(
        string id, string name, string desc,
        string model, string device, string brand, string manufacturer, string productName,
        string marketname, string buildProduct, string fingerprint, string description,
        string? socModel = "SM8750", string? socMfr = "QTI", string? platform = "sun") =>
        Profile(id, name, desc, "Snapdragon 8 Elite", BuildProps(
            model, device, brand, manufacturer, productName, marketname, buildProduct,
            fingerprint, description, platform!, socModel!, socMfr!));

    private static DeviceSpoofProfile Gen3Profile(
        string id, string name, string desc,
        string model, string device, string brand, string manufacturer, string productName,
        string marketname, string buildProduct, string fingerprint, string description,
        string? socModel = "SM8650", string? socMfr = "QTI", string? platform = "kalama") =>
        Profile(id, name, desc, "Snapdragon 8 Gen 3", BuildProps(
            model, device, brand, manufacturer, productName, marketname, buildProduct,
            fingerprint, description, platform!, socModel!, socMfr!));

    private static Dictionary<string, string> BuildProps(
        string model, string device, string brand, string manufacturer, string productName,
        string marketname, string buildProduct, string fingerprint, string description,
        string platform, string socModel, string socMfr) =>
        new(StringComparer.Ordinal)
        {
            ["ro.product.model"] = model,
            ["ro.product.device"] = device,
            ["ro.product.brand"] = brand,
            ["ro.product.manufacturer"] = manufacturer,
            ["ro.product.name"] = productName,
            ["ro.product.marketname"] = marketname,
            ["ro.product.board"] = platform,
            ["ro.build.product"] = buildProduct,
            ["ro.build.fingerprint"] = fingerprint,
            ["ro.build.description"] = description,
            ["ro.hardware"] = platform is "zumapro" ? "komodo" : "qcom",
            ["ro.boot.hardware"] = platform is "zumapro" ? "komodo" : "qcom",
            ["ro.board.platform"] = platform,
            ["ro.soc.model"] = socModel,
            ["ro.soc.manufacturer"] = socMfr,
        };

    private static DeviceSpoofProfile Profile(
        string id, string name, string desc, string chip, Dictionary<string, string> props) =>
        new()
        {
            Id = id,
            DisplayName = name,
            Description = desc,
            Chip = chip,
            Props = props,
        };

    private static DeviceSpoofProfile GameProfile(
        string id, string name, string desc, params string[] gameProps) =>
        new()
        {
            Id = id,
            DisplayName = name,
            Description = desc,
            Chip = "Game-Specific",
            Props = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ro.game.fps_unlock"] = "1",
            },
        };
}