namespace MintADB.Wpf.Helpers;

public static class HzPresetHelper
{
    public static int FromComboIndex(int index) => index switch
    {
        0 => 60,
        1 => 90,
        2 => 120,
        3 => 144,
        _ => 120,
    };
}