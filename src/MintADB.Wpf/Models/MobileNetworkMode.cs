namespace MintADB.Wpf.Models;

public static class MobileNetworkMode
{
    public static readonly (string Label, int Value)[] Presets =
    [
        ("5G + LTE + 3G (tự động)", 33),
        ("5G + LTE", 26),
        ("Chỉ 5G", 23),
        ("LTE (4G)", 9),
        ("Chỉ LTE", 11),
        ("3G", 2),
    ];

    public static string Describe(int mode) =>
        Presets.FirstOrDefault(p => p.Value == mode).Label ?? $"mode {mode}";

    public static string SettingsKey(int simSlot) => simSlot switch
    {
        1 => "preferred_network_mode1",
        2 => "preferred_network_mode2",
        _ => "preferred_network_mode",
    };
}