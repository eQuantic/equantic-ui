namespace eQuantic.UI.Components;

/// <summary>
/// The SDK's OWN user-facing strings — every built-in label a component shows or announces,
/// behind one seam (I18N-PLAN D14). Components never hardcode a UI string: they read a property
/// here, and the property reads <c>SdkResources</c> — an ordinary ResXFileCodeGenerator accessor
/// over <c>SdkResources.resx</c> and its translations. Properties, never consts: a const crosses
/// assemblies by VALUE, and inlined call sites would keep announcing the build machine's language.
/// <para>
/// Localized on every target by the SAME mechanism an app's own strings use — the real
/// <c>ResourceManager</c> over satellite assemblies on server and native (D10), and eqc's accessor
/// rewrite into the culture catalog on the web (D2). An app with ZERO resx of its own still gets a
/// Checkbox that announces "Marcado" to a pt-BR screen reader, because the SDK's keys ride the
/// app's catalogs the moment a component that uses them is reachable.
/// </para>
/// </summary>
public static class SdkStrings
{
    // Toggle state announcements (Checkbox, Switch) — assistive-tech wording, not visible text.
    public static string Checked => SdkResources.Checked;
    public static string Unchecked => SdkResources.Unchecked;
    public static string PartlySelected => SdkResources.PartlySelected;
    public static string On => SdkResources.On;
    public static string Off => SdkResources.Off;

    /// <summary>The scrim/affordance label every dismissible surface shares (Banner, Dialog,
    /// Drawer, BottomSheet) — one string, one casing, for all of them.</summary>
    public static string Dismiss => SdkResources.Dismiss;

    /// <summary>An Input chip's trailing remove affordance.</summary>
    public static string Remove => SdkResources.Remove;

    // SearchField.
    public static string SearchPlaceholder => SdkResources.SearchPlaceholder;
    public static string ClearSearch => SdkResources.ClearSearch;

    // The code editor's find bar.
    public static string Find => SdkResources.Find;
    public static string PreviousMatch => SdkResources.PreviousMatch;
    public static string NextMatch => SdkResources.NextMatch;

    /// <summary>The sheet surface's accessible name when the app supplies none.</summary>
    public static string Spreadsheet => SdkResources.Spreadsheet;
}
