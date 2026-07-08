using MintADB.Wpf.Models;

namespace MintADB.Wpf.Services;

public static class AppClassifier
{
    private static readonly HashSet<string> PlayStoreInstallers =
    [
        "com.android.vending",
        "com.google.android.packageinstaller",
    ];

    private static readonly string[] EssentialSystemPrefixes =
    [
        "android",
        "com.android.systemui",
        "com.android.settings",
        "com.android.phone",
        "com.android.server.",
        "com.android.providers.",
        "com.android.incallui",
        "com.android.camera",
        "com.android.bluetooth",
        "com.android.nfc",
        "com.android.keychain",
        "com.android.location",
        "com.android.wifi",
        "com.android.shell",
        "com.android.dynsystem",
        "com.android.localtransport",
        "com.google.android.gms",
        "com.google.android.gsf",
        "com.google.android.ext.",
        "com.google.android.onetimeinitializer",
        "com.google.android.configupdater",
        "com.google.android.partnersetup",
        "com.miui.home",
        "com.miui.securitycenter",
        "com.miui.securitycore",
        "com.miui.powerkeeper",
        "com.miui.gallery",
        "com.miui.miwallpaper",
        "com.miui.cloudservice",
        "com.miui.core",
        "com.miui.system",
        "com.miui.systemui",
        "com.miui.daemon",
        "com.miui.aod",
        "com.miui.notification",
        "com.xiaomi.account",
        "com.xiaomi.micloudsdk",
        "com.xiaomi.finddevice",
        "com.android.vending",
    ];

    private static readonly string[] BloatPrefixes =
    [
        // MIUI Apps
        "com.miui.cleaner",
        "com.miui.analytics",
        "com.miui.bugreport",
        "com.miui.hybrid",
        "com.miui.videoplayer",
        "com.miui.player",
        "com.miui.newmidrive",
        "com.miui.newhome",
        "com.miui.virtualsim",
        "com.miui.vsimcore",
        "com.miui.yellowpage",
        "com.miui.weather",
        "com.miui.weather2",
        "com.miui.compass",
        "com.miui.screenrecorder",
        "com.miui.mediaeditor",
        "com.miui.notes",
        "com.miui.calculator",
        "com.miui.fm",
        "com.miui.miservice",
        "com.miui.misound",
        "com.miui.greenguard",
        "com.miui.phrase",
        "com.miui.touchassistant",
        "com.miui.smarttravel",
        "com.miui.barcodescanner",
        "com.miui.contentextension",
        "com.miui.mishare.",
        "com.miui.huanji",        // Chuyển dữ liệu cũ
        "com.miui.backup",        // Backup system
        "com.miui.mediaexplorer", // Trình duyệt file
        "com.miui.mediaviewer",   // Xem file
        "com.miui.mirror",        // Mirror screen
        "com.miui.rombackup",     // Backup ROM
        "com.miui.autostart",     // Autostart manager
        "com.miui.daemon",        // MIUI daemon
        "com.miui.personalassistant", // Personal assistant
        "com.miui.serviceextension",  // Service extension
        "com.miui.spd",           // SPD service
        "com.miui.transferservice", // Transfer service
        "com.miui.voiceassist",   // Voice assistant

        // Xiaomi Apps
        "com.xiaomi.market",
        "com.xiaomi.mipicks",
        "com.xiaomi.gamecenter",
        "com.xiaomi.scanner",
        "com.xiaomi.shop",
        "com.xiaomi.payment",
        "com.xiaomi.misettings",
        "com.xiaomi.mibrain.",
        "com.xiaomi.simactivate", // Kích hoạt SIM
        "com.xiaomi.discover",
        "com.xiaomi.mircs",
        "com.xiaomi.xmsf",        // Push service
        "com.xiaomi.xmsfkeeper",  // Giữ push

        // Chinese Apps
        "com.baidu.",
        "com.tencent.",
        "com.sina.",
        "com.taobao.",
        "com.alibaba.",
        "com.qiyi.",
        "com.youku.",
        "com.kugou.",
        "com.netease.",
        "com.zhihu.",
        "com.dianping.",
        "com.autonavi.",
        "com.UCMobile",
        "com.smile.gifmaker",
        "com.ss.android.",
        "com.eg.android.AlipayGphone",
        "com.qihoo.",
        "com.funshion.",
        "com.pplive.",
        "com.letv.",
        "com.hunantv.",
        "com.chaozh.",
        "com.iflytek.",
        "com.mi.health",
        "com.mipay.",
        "com.duokan.",
    ];

    private static readonly HashSet<string> KnownBloatPackages =
    [
        "com.miui.findmy",
        "com.miui.tsmclient",
        "com.miui.securitymanager",
        "com.lbe.security.miui",
        "com.android.soundrecorder",
        "com.miui.mediafeature",
        "com.miui.msa.global",
        "com.miui.systemAdSolution",
        "com.miui.android.fashiongallery",
        "com.xiaomi.discover",
        "com.xiaomi.mircs",
        "com.xiaomi.xmsf",
        "com.xiaomi.xmsfkeeper",
        "com.miui.qmservice",
        "com.miui.carlink",
        "com.miui.drm",
        "com.miui.gallery.lock",
        "com.miui.mishare.connectivity",
        "com.miui.screenrecorder.service",
        "com.xiaomi.account",
        "com.xiaomi.finddevice",
        "com.xiaomi.joyose",
        "com.xiaomi.mipicks",
        "com.xiaomi.payment",
        "com.xiaomi.shop",
        "com.xiaomi.simactivate",
    ];

    private static readonly string[] PreinstalledVendorPrefixes =
    [
        "com.miui.",
        "com.xiaomi.",
        "com.baidu.",
        "com.tencent.",
        "com.sina.",
        "com.taobao.",
        "com.alibaba.",
    ];

    public static AppCategory Classify(string package, bool isSystem, string? installer)
    {
        if (IsEssentialSystem(package))
            return AppCategory.System;

        if (IsRomBloat(package, isSystem, installer))
            return AppCategory.RomBloat;

        if (isSystem)
            return AppCategory.System;

        if (IsPlayStoreInstaller(installer))
            return AppCategory.PlayStore;

        return AppCategory.UserInstalled;
    }

    public static string DisplayName(string package)
    {
        var preset = AppPreset.Defaults.FirstOrDefault(p => p.Package == package);
        if (preset is not null) return preset.Name;

        var last = package.Split('.')[^1];
        if (last.Length <= 2) return package;
        return char.ToUpper(last[0]) + last[1..];
    }

    private static bool IsPlayStoreInstaller(string? installer) =>
        !string.IsNullOrWhiteSpace(installer) && PlayStoreInstallers.Contains(installer);

    private static bool IsEssentialSystem(string package) =>
        EssentialSystemPrefixes.Any(p => package.StartsWith(p, StringComparison.Ordinal) || package == p);

    private static bool IsRomBloat(string package, bool isSystem, string? installer)
    {
        if (KnownBloatPackages.Contains(package))
            return true;

        if (isSystem)
            return BloatPrefixes.Any(p => package.StartsWith(p, StringComparison.Ordinal));

        if (IsPlayStoreInstaller(installer))
            return false;

        var noInstaller = string.IsNullOrWhiteSpace(installer) || installer is "null";
        if (noInstaller && PreinstalledVendorPrefixes.Any(p => package.StartsWith(p, StringComparison.Ordinal)))
            return true;

        if (installer is "com.miui.packageinstaller" or "com.xiaomi.market")
            return true;

        return false;
    }
}