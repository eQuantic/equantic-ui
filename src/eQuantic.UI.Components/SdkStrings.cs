namespace eQuantic.UI.Components;

/// <summary>
/// The SDK's OWN user-facing strings — every built-in label a component shows or announces,
/// behind one seam (I18N-PLAN D14). Components never hardcode a UI string: they read a property
/// here, so the neutral English lives in exactly one place and Track L (W8) swaps the bodies for
/// the resx-backed accessor without touching a single component. Properties, never consts — a
/// const crosses assemblies by VALUE, and inlined call sites would keep announcing the build
/// machine's language after the seam learns cultures.
/// </summary>
public static class SdkStrings
{
    // Toggle state announcements (Checkbox, Switch) — assistive-tech wording, not visible text.
    public static string Checked => "Checked";
    public static string Unchecked => "Unchecked";
    public static string PartlySelected => "Partly selected";
    public static string On => "On";
    public static string Off => "Off";

    /// <summary>The scrim/affordance label every dismissible surface shares (Banner, Dialog,
    /// Drawer, BottomSheet) — one string, one casing, for all of them.</summary>
    public static string Dismiss => "Dismiss";

    /// <summary>An Input chip's trailing remove affordance.</summary>
    public static string Remove => "Remove";

    // SearchField.
    public static string SearchPlaceholder => "Search…";
    public static string ClearSearch => "Clear search";

    // The code editor's find bar.
    public static string Find => "Find";
    public static string PreviousMatch => "Previous match";
    public static string NextMatch => "Next match";

    /// <summary>The sheet surface's accessible name when the app supplies none.</summary>
    public static string Spreadsheet => "Spreadsheet";
}
