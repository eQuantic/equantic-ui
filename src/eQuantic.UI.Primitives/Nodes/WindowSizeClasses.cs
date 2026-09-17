namespace eQuantic.UI.Primitives;

public static class WindowSizeClasses
{
    public const float MediumMinDp = 600;
    public const float ExpandedMinDp = 840;

    public static WindowSizeClass FromWidth(float dp) => dp switch
    {
        >= ExpandedMinDp => WindowSizeClass.Expanded,
        >= MediumMinDp => WindowSizeClass.Medium,
        _ => WindowSizeClass.Compact,
    };
}
