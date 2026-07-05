namespace MintADB.Wpf.Models;

public enum RebootMode
{
    Normal,
    Recovery,
    Bootloader,
    Sideload,
    Edl,
}

public static class RebootModeExtensions
{
    public static string AdbArg(this RebootMode mode) => mode switch
    {
        RebootMode.Normal => "reboot",
        RebootMode.Recovery => "reboot recovery",
        RebootMode.Bootloader => "reboot bootloader",
        RebootMode.Sideload => "reboot sideload",
        RebootMode.Edl => "reboot edl",
        _ => "reboot",
    };

    public static string Label(this RebootMode mode) => mode switch
    {
        RebootMode.Normal => "Reboot",
        RebootMode.Recovery => "Recovery",
        RebootMode.Bootloader => "Bootloader",
        RebootMode.Sideload => "Sideload",
        RebootMode.Edl => "EDL",
        _ => mode.ToString(),
    };
}