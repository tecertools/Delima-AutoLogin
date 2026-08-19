namespace Delima.Launcher.Theming;

/// <summary>
/// Resource dictionary keys and tokens for Delima Smart Launcher runtime theming.
/// </summary>
public static class Tokens
{
    public const string PageBackgroundBrush = "PageBackgroundBrush";
    public const string SurfaceBrush = "SurfaceBrush";
    public const string SoftSurfaceBrush = "SoftSurfaceBrush";
    public const string BorderBrush = "BorderBrush";
    public const string PrimaryTextBrush = "PrimaryTextBrush";
    public const string SecondaryTextBrush = "SecondaryTextBrush";
    public const string PrimaryBrush = "PrimaryBrush";
    public const string AccentBrush = "AccentBrush";
    public const string AlertBrush = "AlertBrush";
    public const string FocusRingBrush = "FocusRingBrush";

    public const string ClassColour0 = "ClassColour0";
    public const string ClassColour1 = "ClassColour1";
    public const string ClassColour2 = "ClassColour2";
    public const string ClassColour3 = "ClassColour3";
    public const string ClassColour4 = "ClassColour4";
    public const string ClassColour5 = "ClassColour5";
    public const string ClassColour6 = "ClassColour6";
    public const string ClassColour7 = "ClassColour7";

    public static string GetClassColourKey(int index)
    {
        int normalized = Math.Clamp(index, 0, 7);
        return $"ClassColour{normalized}";
    }
}
