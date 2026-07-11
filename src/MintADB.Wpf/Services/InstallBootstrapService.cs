using System.IO;
using System.Text.Json;
using MintADB.Wpf.Models;

namespace MintADB.Wpf.Services;

public sealed class InstallBootstrapService(AdbService adb)
{
    public const string BootstrapOnlyArg = "--bootstrap-only";
    public const string BootstrapArg = "--bootstrap";

    private static string StateDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MintADB");

    private static string StatePath => Path.Combine(StateDir, "install-state.json");

    public static bool ShouldRun(string[] args)
    {
        if (args.Contains(BootstrapOnlyArg, StringComparer.OrdinalIgnoreCase)
            || args.Contains(BootstrapArg, StringComparer.OrdinalIgnoreCase))
            return true;
        return !IsComplete();
    }

    public static bool IsComplete()
    {
        if (!File.Exists(StatePath)) return false;
        try
        {
            var json = File.ReadAllText(StatePath);
            var state = JsonSerializer.Deserialize<InstallState>(json);
            return state?.Version >= CurrentVersion && state.ToolsDeployed;
        }
        catch
        {
            return false;
        }
    }

    // Bump when deploy layout changes (e.g. scrcpy must be re-copied after upgrade).
    private const int CurrentVersion = 2;

    public async Task<BootstrapResult> RunAsync(
        bool offerDriverInstall,
        Action<string>? log = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(StateDir);
        Directory.CreateDirectory(PlatformToolsLocator.InstalledToolsDir);
        Directory.CreateDirectory(AdbToolsService.MintAdbDir);
        Directory.CreateDirectory(Path.Combine(AdbToolsService.MintAdbDir, "Screenshots"));
        Directory.CreateDirectory(Path.Combine(AdbToolsService.MintAdbDir, "Recordings"));

        log?.Invoke("[Cai dat] Chuan bi moi truong MintADB...");

        var toolsOk = false;
        var toolsMessage = "";
        if (PlatformToolsLocator.GetStatus(adb.AdbPath).BundledToolsPresent)
        {
            (toolsOk, toolsMessage) = await PlatformToolsLocator.DeployToolsToLocalAsync(ct);
            log?.Invoke(toolsOk ? $"[OK] {toolsMessage}" : $"[WARN] {toolsMessage}");
        }
        else
        {
            toolsMessage = "Thieu PlatformTools cạnh app.";
            log?.Invoke($"[WARN] {toolsMessage}");
            // Still try to repair scrcpy from any remaining local copy.
            PlatformToolsLocator.EnsureScrcpyDeployed();
        }

        ScrcpyLocator.ClearCache();
        var scrcpy = ScrcpyLocator.Find(adb.AdbPath);
        log?.Invoke(scrcpy is not null
            ? $"[OK] scrcpy: {scrcpy}"
            : "[WARN] scrcpy chua co — can cai ban MintADB co kem scrcpy (v1.0.1+).");

        adb.ReloadPaths();

        string? adbVersion = null;
        var toolsReady = false;
        try
        {
            await adb.KillServerAsync(ct);
            var server = await adb.StartServerAsync(ct);
            log?.Invoke(server.Ok ? "[OK] ADB server da khoi dong" : $"[WARN] ADB server: {server.Combined}");

            adbVersion = await adb.GetVersionAsync(ct);
            var toolsStatus = PlatformToolsLocator.GetStatus(adb.AdbPath);
            toolsReady = toolsStatus.AdbFound && toolsStatus.FastbootFound && adbVersion is not null;
        }
        catch (Exception ex)
        {
            log?.Invoke($"[WARN] ADB server: {ex.Message}");
        }

        UsbDriverCheckResult? driverCheck = null;
        if (offerDriverInstall)
        {
            try
            {
                if (UsbDriverService.DriverBundlePresent)
                    driverCheck = await UsbDriverService.CheckAsync(adb, ct);
            }
            catch (Exception ex)
            {
                log?.Invoke($"[WARN] Kiem tra driver: {ex.Message}");
            }
        }

        SaveState(toolsOk, toolsReady, adbVersion);
        WriteBootstrapLog(toolsOk, toolsReady, adbVersion);

        log?.Invoke("[Cai dat] Hoan tat cau hinh noi bo.");

        return new BootstrapResult(
            toolsOk,
            toolsReady,
            toolsMessage,
            adbVersion,
            driverCheck,
            offerDriverInstall);
    }

    private static void SaveState(bool toolsDeployed, bool toolsReady, string? adbVersion)
    {
        Directory.CreateDirectory(StateDir);

        var state = new InstallState
        {
            Version = CurrentVersion,
            CompletedAt = DateTime.Now,
            ToolsDeployed = toolsDeployed,
            ToolsReady = toolsReady,
            AdbVersion = adbVersion,
            AppPath = AppContext.BaseDirectory,
        };

        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(StatePath, json);
    }

    private static void WriteBootstrapLog(bool toolsDeployed, bool toolsReady, string? adbVersion)
    {
        try
        {
            var lines = new[]
            {
                $"MintADB bootstrap {DateTime.Now:u}",
                $"App: {AppContext.BaseDirectory}",
                $"Tools deployed: {toolsDeployed}",
                $"Tools ready: {toolsReady}",
                $"ADB: {adbVersion ?? "n/a"}",
                $"State: {StatePath}",
            };
            File.WriteAllLines(Path.Combine(StateDir, "bootstrap.log"), lines);
        }
        catch
        {
            // Best-effort trace for installer troubleshooting.
        }
    }

    private sealed class InstallState
    {
        public int Version { get; init; }
        public DateTime CompletedAt { get; init; }
        public bool ToolsDeployed { get; init; }
        public bool ToolsReady { get; init; }
        public string? AdbVersion { get; init; }
        public string? AppPath { get; init; }
    }
}

public readonly record struct BootstrapResult(
    bool ToolsDeployed,
    bool ToolsReady,
    string ToolsMessage,
    string? AdbVersion,
    UsbDriverCheckResult? DriverCheck,
    bool OfferDriverInstall);