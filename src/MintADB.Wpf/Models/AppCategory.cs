namespace MintADB.Wpf.Models;

public enum AppCategory
{
    System,
    PlayStore,
    UserInstalled,
    RomBloat,
}

public static class AppCategoryExtensions
{
    public static string Label(this AppCategory c) => c switch
    {
        AppCategory.System => "Hệ thống",
        AppCategory.PlayStore => "Play Store",
        AppCategory.UserInstalled => "User tự cài",
        AppCategory.RomBloat => "Rác ROM",
        _ => c.ToString(),
    };

    public static int SortOrder(this AppCategory c) => c switch
    {
        AppCategory.PlayStore => 0,
        AppCategory.UserInstalled => 1,
        AppCategory.RomBloat => 2,
        AppCategory.System => 3,
        _ => 9,
    };

    /// <summary>Row background — An toàn / Thông tin / Cảnh báo / Nguy hiểm</summary>
    public static string RowBackground(this AppCategory c) => c switch
    {
        AppCategory.PlayStore => "#1C2A22",
        AppCategory.UserInstalled => "#1A2533",
        AppCategory.RomBloat => "#2A1C1C",
        AppCategory.System => "#2A2418",
        _ => "#323232",
    };

    public static string RowBorder(this AppCategory c) => c switch
    {
        AppCategory.PlayStore => "#2D6B45",
        AppCategory.UserInstalled => "#2B5A7A",
        AppCategory.RomBloat => "#8A3030",
        AppCategory.System => "#8A6B1E",
        _ => "#484848",
    };

    public static string AccentColor(this AppCategory c) => c switch
    {
        AppCategory.PlayStore => "#5DD68A",
        AppCategory.UserInstalled => "#7EC8FF",
        AppCategory.RomBloat => "#FF8A82",
        AppCategory.System => "#FFD060",
        _ => "#AEAEB2",
    };

    public static string BadgeBackground(this AppCategory c) => c switch
    {
        AppCategory.PlayStore => "#1A3D2A",
        AppCategory.UserInstalled => "#1A3050",
        AppCategory.RomBloat => "#3D1A1A",
        AppCategory.System => "#3D3018",
        _ => "#3A3A3C",
    };

    public static string BadgeForeground(this AppCategory c) => AccentColor(c);
}