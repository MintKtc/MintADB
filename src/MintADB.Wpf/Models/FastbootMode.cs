namespace MintADB.Wpf.Models;

public enum FastbootMode
{
    Reboot,
    Bootloader,
    Recovery,
    Fastboot,
    Continue,
}

public static class FastbootModeExtensions
{
    public static string[] Args(this FastbootMode mode) => mode switch
    {
        FastbootMode.Reboot => ["reboot"],
        FastbootMode.Bootloader => ["reboot-bootloader"],
        FastbootMode.Recovery => ["reboot-recovery"],
        FastbootMode.Fastboot => ["reboot-fastboot"],
        FastbootMode.Continue => ["continue"],
        _ => ["reboot"],
    };

    public static string Label(this FastbootMode mode) => mode switch
    {
        FastbootMode.Reboot => "Reboot",
        FastbootMode.Bootloader => "Bootloader",
        FastbootMode.Recovery => "Recovery",
        FastbootMode.Fastboot => "Fastbootd",
        FastbootMode.Continue => "Continue",
        _ => "Reboot",
    };
}