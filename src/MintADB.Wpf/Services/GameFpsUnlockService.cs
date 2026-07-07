using System.IO;
using System.Text;

namespace MintADB.Wpf.Services;

/// <summary>
/// Reverse-engineered from 120 FPS Unlock v6.7 (com.modaov.unlock60fps).
/// Không hook game — chỉ copy config vào Android/data qua ADB (giống SAF/Shizuku/Root của app gốc).
/// </summary>
public enum PubgRegion
{
    Global,
    Vietnam,
    India,
    China,
    Korea,
    Taiwan,
}

public enum AovRegion
{
    Vietnam,
    Taiwan,
    Thailand,
    Indonesia,
    Sea,
}

public sealed record GameFpsStep(string Label, bool Ok, string Detail);

public sealed record AovFpsOptions(bool XiaomiBoost = true, bool LockHz = true);

public sealed record GameFpsResult(IReadOnlyList<GameFpsStep> Steps, string? Advisory);

public sealed class GameFpsUnlockService(AdbService adb)
{
    private const string Ue4Root = "files/UE4Game/ShadowTrackerExtra/ShadowTrackerExtra";
    private const string PubgActiveSavTemplate = "pubg_fps/pgt_pro/Active.sav";

    private static readonly IReadOnlyDictionary<PubgRegion, (string Package, string Label)> PubgRegions =
        new Dictionary<PubgRegion, (string, string)>
        {
            [PubgRegion.Global] = ("com.tencent.ig", "Global"),
            [PubgRegion.Vietnam] = ("com.vng.pubgmobile", "Việt Nam"),
            [PubgRegion.India] = ("com.pubg.imobile", "India"),
            [PubgRegion.China] = ("com.tencent.tmgp.pubgmhd", "Trung Quốc"),
            [PubgRegion.Korea] = ("com.pubg.krmobile", "Hàn Quốc"),
            [PubgRegion.Taiwan] = ("com.rekoo.pubgm", "Đài Loan"),
        };

    private static readonly IReadOnlyDictionary<AovRegion, (string Package, string Label, char Prefix)> AovRegions =
        new Dictionary<AovRegion, (string, string, char)>
        {
            [AovRegion.Vietnam] = ("com.garena.game.kgvn", "Việt Nam", '0'),
            [AovRegion.Taiwan] = ("com.garena.game.kgtw", "Đài Loan", '1'),
            [AovRegion.Thailand] = ("com.garena.game.kgth", "Thái Lan", '2'),
            [AovRegion.Indonesia] = ("com.garena.game.kgid", "Indonesia", '3'),
            [AovRegion.Sea] = ("com.garena.game.kgsam", "SEA", '4'),
        };

    /// <summary>FPSLevel / BattleFPS / LobbyFPS trong Active.sav (GVAS IntProperty).</summary>
    private static readonly IReadOnlyDictionary<int, int> PubgSavLevel = new Dictionary<int, int>
    {
        [30] = 2,
        [40] = 3,
        [60] = 4,
        [90] = 7,
        [120] = 8,
    };

    private static readonly IReadOnlyDictionary<int, string> PubgSystemAsset = new Dictionary<int, string>
    {
        [30] = "pubg_fps/gfx_tool/es3",
        [40] = "pubg_fps/gfx_tool/es4",
        [60] = "pubg_fps/gfx_tool/es5",
        [90] = "pubg_fps/gfx_tool/es6",
    };

    /// <summary>dn_turbo/1/XYZ — 2 số cuối = tier FPS, số đầu = nhóm region.</summary>
    private static readonly IReadOnlyDictionary<int, string> AovFpsSuffix = new Dictionary<int, string>
    {
        [30] = "01",
        [40] = "11",
        [60] = "21",
        [90] = "41",
        [120] = "61",
    };

    public static IReadOnlyList<int> SupportedFps { get; } = [30, 40, 60, 90, 120];

    public static string GetPubgPackage(PubgRegion region) => PubgRegions[region].Package;
    public static string GetPubgLabel(PubgRegion region) => PubgRegions[region].Label;
    public static string GetAovPackage(AovRegion region) => AovRegions[region].Package;
    public static string GetAovLabel(AovRegion region) => AovRegions[region].Label;

    public static string DescribePubgMechanism() =>
        """
        PUBG — 3 lớp config (không hook):
        1. UserSettings.ini [SystemSettings] — r.PUBGDeviceFPS* (cap FPS thiết bị)
        2. Active.sav (GVAS) — FPSLevel, BattleFPS, LobbyFPS
        3. UserCustom.ini — CVars đồ họa (XOR 121), không set FPS trực tiếp
        """;

    public static string DescribeAovMechanism() =>
        """
        Liên Quân — inject qua dn_turbo assets:
        1. Active.sav — preset GVAS theo region + FPS (vd: 061 = VN 120fps)
        2. UserCustom/UserSettings.ini — SoundQuality + ArtQuality (20=90fps, 30=120fps)
        """;

    private static string AssetsDir => Path.Combine(AppContext.BaseDirectory, "Assets");

    private static string WorkDir
    {
        get
        {
            var dir = Path.Combine(AdbToolsService.MintAdbDir, "GameFps");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public async Task<IReadOnlyList<GameFpsStep>> ApplyPubgAsync(
        string serial,
        PubgRegion region,
        int fps,
        Action<string>? log = null,
        CancellationToken ct = default)
    {
        ValidateFps(fps);
        var pkg = GetPubgPackage(region);
        log?.Invoke(DescribePubgMechanism().Trim());

        var paths = BuildPubgPaths(pkg);
        var steps = new List<GameFpsStep>();

        steps.Add(await ForceStopAsync(serial, pkg, "Tắt PUBG", ct));
        steps.Add(await ShellStepAsync(serial, $"Kiểm tra PUBG ({pkg})", async () =>
            await PackageInstalledAsync(serial, pkg, ct), ct));

        steps.AddRange(await EnsureDirsAsync(serial, paths.Values, ct));
        steps.AddRange(await DeletePubgOldConfigsAsync(serial, paths, ct));

        var userSettings = BuildPubgUserSettings(fps);
        var userCustom = ReadAssetText("pubg_fps/gfx_tool/UserCustom.ini");
        var activeSav = BuildPubgActiveSav(fps);

        steps.Add(await WriteTextAsync(serial,
            $"[L1] UserSettings.ini — r.PUBGDeviceFPS* = {fps}",
            paths.UserSettings, userSettings, ct));
        steps.Add(await WriteTextAsync(serial,
            "[L3] UserCustom.ini — CVars đồ họa (XOR)",
            paths.UserCustom, userCustom, ct));
        steps.Add(await WriteBinaryAsync(serial,
            $"[L2] Active.sav — FPSLevel/BattleFPS/LobbyFPS = {PubgSavLevel[fps]}",
            paths.ActiveSav, activeSav, ct));

        steps.Add(await VerifyFileAsync(serial, paths.ActiveSav, "Active.sav", ct));
        LogSavLevels(activeSav, log);

        LogSteps(steps, log);
        return steps;
    }

    public Task<GameFpsResult> ApplyAovAsync(
        string serial,
        AovRegion region,
        int fps,
        Action<string>? log = null,
        CancellationToken ct = default)
        => ApplyAovAsync(serial, region, fps, new AovFpsOptions(), log, ct);

    public async Task<GameFpsResult> ApplyAovAsync(
        string serial,
        AovRegion region,
        int fps,
        AovFpsOptions options,
        Action<string>? log = null,
        CancellationToken ct = default)
    {
        ValidateFps(fps);
        var (pkg, _, prefix) = AovRegions[region];
        log?.Invoke(DescribeAovMechanism().Trim());

        var suffix = AovFpsSuffix[fps];
        var savName = $"{prefix}{suffix}";
        var profileName = fps >= 120 ? "30" : "20";

        var paths = BuildAovPaths(pkg);
        var steps = new List<GameFpsStep>();
        var device = await ReadDeviceContextAsync(serial, ct);

        log?.Invoke($"[Máy] {device.Model} · SoC {device.Soc} · {device.Brand}");
        if (device.IsXiaomi)
            log?.Invoke("[Xiaomi] App Unlock gốc có fix_fps_xiaomi — sẽ áp dụng tối ưu MIUI nếu bật.");

        steps.Add(await ForceStopAsync(serial, pkg, "Tắt Liên Quân", ct));
        steps.Add(await ShellStepAsync(serial, $"Kiểm tra Liên Quân ({pkg})", async () =>
            await PackageInstalledAsync(serial, pkg, ct), ct));

        if (options.XiaomiBoost && device.IsXiaomi)
            steps.AddRange(await ApplyXiaomiGameBoostAsync(serial, fps, ct));

        if (options.LockHz && fps >= 90)
            steps.AddRange(await ApplyDisplayHzBoostAsync(serial, fps >= 120 ? 120 : 90, device.IsXiaomi, ct));

        steps.AddRange(await EnsureDirsAsync(serial, [paths.ActiveSav, paths.UserCustom, paths.UserSettings], ct));
        steps.AddRange(await DeleteAovOldConfigsAsync(serial, paths, ct));

        var activeSav = ReadAssetBytes($"aov_fps/dn_turbo/1/{savName}");
        var qualityIni = ReadAssetText($"aov_fps/dn_turbo/1/{profileName}");

        steps.Add(await WriteBinaryAsync(serial,
            $"[L1] Active.sav — preset {savName} (FPSLevel={ReadSavFpsLevel(activeSav)})",
            paths.ActiveSav, activeSav, ct));
        steps.Add(await WriteTextAsync(serial,
            $"[L2] UserCustom.ini — profile {profileName} (SoundQualityType={(fps >= 120 ? 2 : 1)})",
            paths.UserCustom, qualityIni, ct));
        steps.Add(await WriteTextAsync(serial,
            $"[L2] UserSettings.ini — profile {profileName}",
            paths.UserSettings, qualityIni, ct));

        steps.Add(await VerifyFileAsync(serial, paths.ActiveSav, "Active.sav", ct));
        LogSavLevels(activeSav, log);

        var advisory = BuildAovAdvisory(fps, device);
        if (!string.IsNullOrEmpty(advisory))
            log?.Invoke(advisory);

        LogSteps(steps, log);
        return new GameFpsResult(steps, advisory);
    }

    private static string? BuildAovAdvisory(int fps, DeviceContext device)
    {
        if (fps < 90) return null;

        if (fps >= 120 && !device.LikelySupports120Menu)
        {
            return """
                [Lưu ý] Inject file đã OK (FPSLevel=8 trong Active.sav) nhưng menu 120 FPS
                phụ thuộc whitelist thiết bị game đọc từ ro.product.* / GPU.
                Redmi tầm trung (SD765G/SM7250…) thường KHÔNG hiện 120 FPS chỉ bằng inject.
                → Fake máy gaming (ROG / SD8 Gen3) ở mục bên dưới + Magisk resetprop + reboot
                → Inject lại Liên Quân sau reboot
                Hoặc thử 90 FPS trước (preset 041, FPSLevel=7).
                """;
        }

        if (fps >= 90 && device.IsXiaomi)
            return "[Lưu ý] Đã bật fix_fps_xiaomi (tối ưu MIUI). Nếu vẫn thiếu mức FPS, cần fake thiết bị flagship.";

        return null;
    }

    private async Task<DeviceContext> ReadDeviceContextAsync(string serial, CancellationToken ct)
    {
        var model = await adb.GetPropAsync(serial, "ro.product.model", ct);
        var brand = await adb.GetPropAsync(serial, "ro.product.brand", ct);
        var soc = await adb.GetPropAsync(serial, "ro.soc.model", ct);
        if (string.IsNullOrWhiteSpace(soc))
            soc = await adb.GetPropAsync(serial, "ro.board.platform", ct);

        var isXiaomi = brand.Contains("xiaomi", StringComparison.OrdinalIgnoreCase)
            || brand.Contains("redmi", StringComparison.OrdinalIgnoreCase)
            || brand.Contains("poco", StringComparison.OrdinalIgnoreCase)
            || model.Contains("Redmi", StringComparison.OrdinalIgnoreCase);

        var likely120 = IsFlagshipSoc(soc) || IsFlagshipSoc(model);
        return new DeviceContext(model, brand, soc, isXiaomi, likely120);
    }

    private static bool IsFlagshipSoc(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var v = value.ToUpperInvariant();
        string[] flagship =
        [
            "SM8750", "SM8650", "SM8550", "SM8475", "SM8450", "SM8350",
            "SD8GEN3", "SD8GEN2", "SD8ELITE", "SD888", "SD865",
            "MT699", "MT689", "DIMENSITY 9", "DIMENSITY 9200", "DIMENSITY 9300",
            "Tensor G4", "Tensor G3",
        ];
        return flagship.Any(f => v.Contains(f, StringComparison.Ordinal));
    }

    private async Task<IReadOnlyList<GameFpsStep>> ApplyXiaomiGameBoostAsync(
        string serial, int fps, CancellationToken ct)
    {
        var steps = new List<GameFpsStep>();
        var commands = new List<(string Label, string Ns, string Key, string Value)>
        {
            ("fix_fps_xiaomi: smart fps off", "global", "miui_smart_fps_mode", "0"),
            ("fix_fps_xiaomi: game booster off", "global", "miui_game_booster", "0"),
            ("fix_fps_xiaomi: power save 120hz off", "global", "power_save_120hz_mode", "0"),
            ("fix_fps_xiaomi: game mode off", "global", "game_mode_intervention_enabled", "0"),
            ("fix_fps_xiaomi: game driver", "global", "game_driver_all_apps", "1"),
            ("fix_fps_xiaomi: enhanced mode", "global", "enhanced_mode", "1"),
        };

        var (ok, fail) = await adb.ApplySettingsBatchAsync(serial, commands, null, ct);
        steps.Add(new GameFpsStep($"Tối ưu Xiaomi (fix_fps_xiaomi) — {ok} OK", fail == 0, fail == 0 ? "OK" : $"{fail} WARN"));
        return steps;
    }

    private async Task<IReadOnlyList<GameFpsStep>> ApplyDisplayHzBoostAsync(
        string serial, int hz, bool miui, CancellationToken ct)
    {
        var steps = new List<GameFpsStep>();
        var hzStr = hz.ToString();
        var commands = new List<(string Label, string Ns, string Key, string Value)>
        {
            ("Khóa peak Hz", "system", "peak_refresh_rate", hzStr),
            ("Khóa min Hz", "system", "min_refresh_rate", hzStr),
            ("refresh_rate_mode", "secure", "refresh_rate_mode", "1"),
            ("adaptive_refresh off", "secure", "adaptive_refresh_rate", "0"),
        };
        if (miui)
        {
            commands.Add(("miui_refresh_rate", "system", "miui_refresh_rate", hzStr));
            commands.Add(("user_refresh_rate", "system", "user_refresh_rate", hzStr));
        }

        var (ok, fail) = await adb.ApplySettingsBatchAsync(serial, commands, null, ct);
        steps.Add(new GameFpsStep($"Khóa tần số quét {hz} Hz", fail == 0, fail == 0 ? "OK" : $"{fail} WARN"));
        return steps;
    }

    private async Task<IReadOnlyList<GameFpsStep>> DeleteAovOldConfigsAsync(
        string serial, AovGamePaths paths, CancellationToken ct)
    {
        var steps = new List<GameFpsStep>();
        foreach (var (label, path) in new (string, string)[]
                 {
                     ("Xóa Active.sav cũ", paths.ActiveSav),
                     ("Xóa UserCustom.ini cũ", paths.UserCustom),
                     ("Xóa UserSettings.ini cũ", paths.UserSettings),
                 })
        {
            steps.Add(await ShellStepAsync(serial, label, async () =>
            {
                var r = await adb.ShellAsync($"rm -f \"{path}\"", serial, ct);
                return r.Ok;
            }, ct));
        }
        return steps;
    }

    private static int? ReadSavFpsLevel(byte[] sav) => GvasSavPatcher.ReadInt(sav, "FPSLevel");

    private readonly record struct DeviceContext(
        string Model, string Brand, string Soc, bool IsXiaomi, bool LikelySupports120Menu);

    private static void ValidateFps(int fps)
    {
        if (!SupportedFps.Contains(fps))
            throw new ArgumentOutOfRangeException(nameof(fps), $"FPS không hỗ trợ: {fps}");
    }

    private static PubgGamePaths BuildPubgPaths(string pkg)
    {
        var basePath = $"/storage/emulated/0/Android/data/{pkg}";
        var root = $"{basePath}/{Ue4Root}";
        return new PubgGamePaths(
            $"{root}/Saved/SaveGames/Active.sav",
            $"{root}/Saved/SaveGames/SettingConfig_Slot.sav",
            $"{root}/Saved/Config/Android/UserSettings.ini",
            $"{root}/Saved/Config/Android/UserCustom.ini");
    }

    private static AovGamePaths BuildAovPaths(string pkg)
    {
        var basePath = $"/storage/emulated/0/Android/data/{pkg}";
        var root = $"{basePath}/{Ue4Root}";
        return new AovGamePaths(
            $"{root}/Saved/SaveGames/Active.sav",
            $"{root}/Saved/Config/Android/UserCustom.ini",
            $"{root}/Saved/Config/Android/UserSettings.ini");
    }

    private async Task<bool> PackageInstalledAsync(string serial, string pkg, CancellationToken ct)
    {
        var r = await adb.ShellAsync($"pm path {pkg}", serial, ct);
        return r.Ok && r.Output.Contains("package:", StringComparison.Ordinal);
    }

    private async Task<GameFpsStep> ForceStopAsync(
        string serial, string pkg, string label, CancellationToken ct)
    {
        var r = await adb.ShellAsync($"am force-stop {pkg}", serial, ct);
        return new GameFpsStep(label, r.Ok, r.Ok ? "OK" : r.Combined);
    }

    private async Task<IReadOnlyList<GameFpsStep>> EnsureDirsAsync(
        string serial, IEnumerable<string> filePaths, CancellationToken ct)
    {
        var steps = new List<GameFpsStep>();
        foreach (var dir in filePaths.Select(p => p[..p.LastIndexOf('/')]).Distinct())
        {
            steps.Add(await ShellStepAsync(serial, $"Tạo thư mục {dir}", async () =>
            {
                var r = await adb.ShellAsync($"mkdir -p \"{dir}\"", serial, ct);
                return r.Ok;
            }, ct));
        }
        return steps;
    }

    private async Task<IReadOnlyList<GameFpsStep>> DeletePubgOldConfigsAsync(
        string serial, PubgGamePaths paths, CancellationToken ct)
    {
        var steps = new List<GameFpsStep>();
        foreach (var (label, path) in new (string, string)[]
                 {
                     ("Xóa Active.sav cũ", paths.ActiveSav),
                     ("Xóa UserSettings.ini cũ", paths.UserSettings),
                     ("Xóa SettingConfig_Slot.sav", paths.SettingSlot),
                 })
        {
            steps.Add(await ShellStepAsync(serial, label, async () =>
            {
                var r = await adb.ShellAsync($"rm -f \"{path}\"", serial, ct);
                return r.Ok;
            }, ct));
        }
        return steps;
    }

    private async Task<GameFpsStep> VerifyFileAsync(
        string serial, string remote, string name, CancellationToken ct)
    {
        return await ShellStepAsync(serial, $"Xác minh {name}", async () =>
        {
            var r = await adb.ShellAsync($"ls -l \"{remote}\"", serial, ct);
            return r.Ok && r.Output.Contains(name, StringComparison.Ordinal);
        }, ct);
    }

    private static string BuildPubgUserSettings(int fps)
    {
        if (PubgSystemAsset.TryGetValue(fps, out var asset))
            return ReadAssetText(asset);

        // 120 FPS: native force_120fps trong app gốc — tất cả r.PUBGDeviceFPS* = 120
        return """
            [SystemSettings]
            r.AllowHDR
            r.Vsync=0
            r.PUBGMaxSupportQualityLevel=3
            r.PUBGDeviceFPSDef=120
            r.PUBGDeviceFPSLow=120
            r.PUBGDeviceFPSMid=120
            r.PUBGDeviceFPSHigh=120
            r.PUBGDeviceFPSHDR=120
            r.PUBGDeviceFPSUltra=120
            """ + "\r\n";
    }

    private static byte[] BuildPubgActiveSav(int fps)
    {
        var template = ReadAssetBytes(PubgActiveSavTemplate);
        var level = PubgSavLevel[fps];
        return GvasSavPatcher.Patch(template, level, "FPSLevel", "BattleFPS", "LobbyFPS");
    }

    private static void LogSavLevels(byte[] sav, Action<string>? log)
    {
        if (log is null) return;
        foreach (var prop in new[] { "FPSLevel", "BattleFPS", "LobbyFPS" })
        {
            var val = GvasSavPatcher.ReadInt(sav, prop);
            if (val is not null)
                log($"  Active.sav {prop} = {val}");
        }
    }

    private static void LogSteps(IReadOnlyList<GameFpsStep> steps, Action<string>? log)
    {
        if (log is null) return;
        foreach (var step in steps)
            log(step.Ok ? $"[OK] {step.Label}" : $"[FAIL] {step.Label}: {step.Detail}");
    }

    private static string ReadAssetText(string relativePath)
    {
        var path = ResolveAssetPath(relativePath);
        return File.ReadAllText(path);
    }

    private static byte[] ReadAssetBytes(string relativePath)
    {
        var path = ResolveAssetPath(relativePath);
        return File.ReadAllBytes(path);
    }

    private static string ResolveAssetPath(string relativePath)
    {
        var path = Path.Combine(AssetsDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
            throw new FileNotFoundException($"Asset không tìm thấy: {relativePath}", path);
        return path;
    }

    private async Task<GameFpsStep> ShellStepAsync(
        string serial, string label, Func<Task<bool>> test, CancellationToken ct)
    {
        try
        {
            var ok = await test();
            return new GameFpsStep(label, ok, ok ? "OK" : "Thất bại");
        }
        catch (Exception ex)
        {
            return new GameFpsStep(label, false, ex.Message);
        }
    }

    private async Task<GameFpsStep> WriteTextAsync(
        string serial, string label, string remote, string content, CancellationToken ct)
    {
        var local = Path.Combine(WorkDir, $"tmp_{Guid.NewGuid():N}.ini");
        try
        {
            await File.WriteAllTextAsync(local, content, ct);
            return await PushFileAsync(serial, label, local, remote, ct);
        }
        finally { TryDelete(local); }
    }

    private async Task<GameFpsStep> WriteBinaryAsync(
        string serial, string label, string remote, byte[] bytes, CancellationToken ct)
    {
        var local = Path.Combine(WorkDir, $"tmp_{Guid.NewGuid():N}.sav");
        try
        {
            await File.WriteAllBytesAsync(local, bytes, ct);
            return await PushFileAsync(serial, label, local, remote, ct);
        }
        finally { TryDelete(local); }
    }

    private async Task<GameFpsStep> PushFileAsync(
        string serial, string label, string local, string remote, CancellationToken ct)
    {
        var push = await adb.RunAsync(["push", local, remote], serial, ct);
        if (!push.Ok)
            return new GameFpsStep(label, false, push.Combined);

        var chmod = await adb.ShellAsync($"chmod 644 \"{remote}\"", serial, ct);
        return new GameFpsStep(label, chmod.Ok, chmod.Ok ? "OK" : chmod.Combined);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* ignore */ }
    }

    private readonly record struct PubgGamePaths(
        string ActiveSav, string SettingSlot, string UserSettings, string UserCustom)
    {
        public IEnumerable<string> Values => [ActiveSav, SettingSlot, UserSettings, UserCustom];
    }

    private readonly record struct AovGamePaths(string ActiveSav, string UserCustom, string UserSettings);
}

/// <summary>UE4 GVAS IntProperty patcher — offset = len(prop) + 26 (little-endian).</summary>
internal static class GvasSavPatcher
{
    public static byte[] Patch(byte[] template, int level, params string[] props)
    {
        var buf = (byte[])template.Clone();
        var missing = new List<string>();
        foreach (var prop in props)
        {
            if (!TryPatchInt(buf, prop, level))
                missing.Add(prop);
        }
        if (missing.Count > 0)
            throw new InvalidOperationException($"Không tìm thấy trong Active.sav: {string.Join(", ", missing)}");
        return buf;
    }

    public static int? ReadInt(byte[] data, string prop)
    {
        var idx = IndexOf(data, Encoding.UTF8.GetBytes(prop + "\0"));
        if (idx is null) return null;
        var off = idx.Value + prop.Length + 26;
        if (off + 4 > data.Length) return null;
        return BitConverter.ToInt32(data, off);
    }

    private static bool TryPatchInt(byte[] data, string prop, int value)
    {
        var idx = IndexOf(data, Encoding.UTF8.GetBytes(prop + "\0"));
        if (idx is null) return false;
        var off = idx.Value + prop.Length + 26;
        if (off + 4 > data.Length) return false;
        var bytes = BitConverter.GetBytes(value);
        if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
        Buffer.BlockCopy(bytes, 0, data, off, 4);
        return true;
    }

    private static int? IndexOf(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0 || haystack.Length < needle.Length) return null;
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return null;
    }
}