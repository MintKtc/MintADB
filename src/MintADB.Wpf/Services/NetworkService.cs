namespace MintADB.Wpf.Services;

public sealed class NetworkService(AdbService adb)
{
    // ── WiFi ──
    public async Task<bool> GetWifiEnabledAsync(string serial, CancellationToken ct = default)
    {
        var r = await adb.ShellAsync("settings get global wifi_on", serial, ct);
        return r.Output.Trim() == "1";
    }

    public async Task<ProcessResult> SetWifiEnabledAsync(string serial, bool enable, CancellationToken ct = default)
        => await adb.ShellAsync($"svc wifi {(enable ? "enable" : "disable")}", serial, ct);

    public async Task<string> GetWifiStatusAsync(string serial, CancellationToken ct = default)
    {
        var enabled = await GetWifiEnabledAsync(serial, ct);
        var ssid = (await adb.ShellAsync("dumpsys wifi | grep \"mWifiInfo\" | head -1", serial, ct)).Output.Trim();
        var ip = await adb.GetWlanIpAsync(serial, ct);
        var signal = (await adb.ShellAsync("dumpsys wifi | grep \"mWifiInfo\" | grep -o \"RSSI: [0-9-]*\"", serial, ct)).Output.Trim();

        var result = $"WiFi: {(enabled ? "ON" : "OFF")}";
        if (!string.IsNullOrEmpty(ssid) && ssid.Contains("SSID"))
        {
            var ssidMatch = System.Text.RegularExpressions.Regex.Match(ssid, @"SSID:\s*([^\s,]+)");
            if (ssidMatch.Success) result += $" · SSID={ssidMatch.Groups[1].Value}";
        }
        if (!string.IsNullOrEmpty(ip)) result += $" · IP={ip}";
        if (!string.IsNullOrEmpty(signal)) result += $" · {signal}";
        return result;
    }

    public async Task<string> ScanWifiAsync(string serial, CancellationToken ct = default)
    {
        await adb.ShellAsync("cmd wifi start-scan", serial, ct);
        await Task.Delay(2000, ct);
        var r = await adb.ShellAsync("cmd wifi list-scan-results | head -20", serial, ct);
        return r.Output.Trim();
    }

    public async Task<ProcessResult> ConnectWifiAsync(string serial, string ssid, string? password = null, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(password))
            return await adb.ShellAsync($"cmd wifi connect-network \"{ssid}\" open", serial, ct);
        return await adb.ShellAsync($"cmd wifi connect-network \"{ssid}\" wpa2 \"{password}\"", serial, ct);
    }

    public async Task<ProcessResult> DisconnectWifiAsync(string serial, CancellationToken ct = default)
        => await adb.ShellAsync("cmd wifi disconnect-network", serial, ct);

    // ── Bluetooth ──
    public async Task<bool> GetBluetoothEnabledAsync(string serial, CancellationToken ct = default)
    {
        var r = await adb.ShellAsync("settings get global bluetooth_on", serial, ct);
        return r.Output.Trim() == "1";
    }

    public async Task<ProcessResult> SetBluetoothEnabledAsync(string serial, bool enable, CancellationToken ct = default)
        => await adb.ShellAsync($"svc bluetooth {(enable ? "enable" : "disable")}", serial, ct);

    public async Task<string> GetBluetoothStatusAsync(string serial, CancellationToken ct = default)
    {
        var enabled = await GetBluetoothEnabledAsync(serial, ct);
        var name = (await adb.ShellAsync("settings get secure bluetooth_name", serial, ct)).Output.Trim();
        var bonded = (await adb.ShellAsync("dumpsys bluetooth_manager | grep -c \"bondState=BONDED\"", serial, ct)).Output.Trim();

        var result = $"BT: {(enabled ? "ON" : "OFF")}";
        if (!string.IsNullOrEmpty(name) && name != "null") result += $" · Name={name}";
        if (!string.IsNullOrEmpty(bonded) && int.TryParse(bonded, out var count) && count > 0) result += $" · {count} paired";
        return result;
    }

    public async Task<string> ScanBluetoothAsync(string serial, CancellationToken ct = default)
    {
        var r = await adb.ShellAsync("dumpsys bluetooth_manager | grep -A2 \"name:\" | head -30", serial, ct);
        return r.Output.Trim();
    }

    // ── Airplane Mode ──
    public async Task<bool> GetAirplaneModeAsync(string serial, CancellationToken ct = default)
    {
        var r = await adb.ShellAsync("settings get global airplane_mode_on", serial, ct);
        return r.Output.Trim() == "1";
    }

    public async Task<ProcessResult> SetAirplaneModeAsync(string serial, bool enable, CancellationToken ct = default)
    {
        var r = await adb.ShellAsync($"settings put global airplane_mode_on {(enable ? "1" : "0")}", serial, ct);
        if (r.Ok)
            await adb.ShellAsync("am broadcast -a android.intent.action.AIRPLANE_MODE --ez state " + (enable ? "true" : "false"), serial, ct);
        return r;
    }

    public async Task<string> GetAirplaneModeStatusAsync(string serial, CancellationToken ct = default)
    {
        var enabled = await GetAirplaneModeAsync(serial, ct);
        return $"Airplane: {(enabled ? "ON" : "OFF")}";
    }

    // ── Hotspot ──
    public async Task<bool> GetHotspotEnabledAsync(string serial, CancellationToken ct = default)
    {
        var r = await adb.ShellAsync("settings get global tethering_on", serial, ct);
        return r.Output.Trim() == "1";
    }

    public async Task<ProcessResult> SetHotspotEnabledAsync(string serial, bool enable, CancellationToken ct = default)
        => await adb.ShellAsync($"svc wifi tether {(enable ? "start" : "stop")}", serial, ct);

    public async Task<string> GetHotspotStatusAsync(string serial, CancellationToken ct = default)
    {
        var enabled = await GetHotspotEnabledAsync(serial, ct);
        var ssid = (await adb.ShellAsync("settings get global soft_ap_ssid", serial, ct)).Output.Trim();
        var password = (await adb.ShellAsync("settings get global soft_ap_psk", serial, ct)).Output.Trim();

        var result = $"Hotspot: {(enabled ? "ON" : "OFF")}";
        if (!string.IsNullOrEmpty(ssid) && ssid != "null") result += $" · SSID={ssid}";
        if (!string.IsNullOrEmpty(password) && password != "null") result += $" · Pass={password}";
        return result;
    }

    // ── Network Status ──
    public async Task<string> GetFullNetworkStatusAsync(string serial, CancellationToken ct = default)
    {
        var lines = new List<string>();

        // WiFi
        var wifi = await GetWifiStatusAsync(serial, ct);
        lines.Add(wifi);

        // Bluetooth
        var bt = await GetBluetoothStatusAsync(serial, ct);
        lines.Add(bt);

        // Airplane
        var airplane = await GetAirplaneModeStatusAsync(serial, ct);
        lines.Add(airplane);

        // Hotspot
        var hotspot = await GetHotspotStatusAsync(serial, ct);
        lines.Add(hotspot);

        // Mobile
        var mobile = (await adb.ShellAsync("dumpsys telephony.registry | grep mServiceState | head -1", serial, ct)).Output.Trim();
        if (!string.IsNullOrEmpty(mobile))
        {
            var stateMatch = System.Text.RegularExpressions.Regex.Match(mobile, @"mVoiceRegState=(\d+)");
            var dataMatch = System.Text.RegularExpressions.Regex.Match(mobile, @"mDataRegState=(\d+)");
            if (stateMatch.Success) lines.Add($"Voice: {(stateMatch.Groups[1].Value == "0" ? "In service" : "Out of service")}");
            if (dataMatch.Success) lines.Add($"Data: {(dataMatch.Groups[1].Value == "0" ? "In service" : "Out of service")}");
        }

        // IP (shared helper — same shell as GetWifiStatus)
        var ip = await adb.GetWlanIpAsync(serial, ct);
        if (!string.IsNullOrEmpty(ip)) lines.Add($"IP: {ip}");

        return string.Join("\n", lines);
    }
}
