namespace MintADB.Wpf.Models;

public sealed class AdbDevice
{
    public string Serial { get; init; } = "";
    public string State { get; init; } = "";
    public string Model { get; init; } = "";
    public string Product { get; init; } = "";

    public string Label =>
        string.IsNullOrWhiteSpace(Model) ? Serial : $"{Model} ({Serial})";

    public bool IsOnline => State == "device";

    public string ModelDisplay =>
        string.IsNullOrWhiteSpace(Model) ? "Unknown" : Model;

    public string StateLabel => State switch
    {
        "device" => "Sẵn sàng",
        "offline" => "Offline",
        "unauthorized" => "Chưa ủy quyền",
        "recovery" => "Recovery",
        "sideload" => "Sideload",
        "bootloader" => "Fastboot",
        _ => string.IsNullOrWhiteSpace(State) ? "—" : State,
    };

    public string StateAccentColor => State switch
    {
        "device" => "#5DD68A",
        "unauthorized" => "#FF6961",
        "offline" => "#AEAEB2",
        "recovery" or "sideload" => "#FFD060",
        "bootloader" => "#7EC8FF",
        _ => "#AEAEB2",
    };

    public string StateBadgeBackground => State switch
    {
        "device" => "#1A3D2A",
        "unauthorized" => "#3D1A1A",
        "offline" => "#3A3A3C",
        "recovery" or "sideload" => "#3D3018",
        "bootloader" => "#1A3050",
        _ => "#3A3A3C",
    };
}