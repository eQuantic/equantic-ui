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
    // Checked/Unchecked/PartlySelected/On/Off lived here while a check's STATE went into its
    // accessible NAME. The state rides aria-checked / SemanticNode.Checked now — the platform
    // announces it in the user's language, which no resx of ours could ever have matched.

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

    // The calendar's own chrome. The month and day NAMES are not here — those come from the
    // culture itself (Primitives.CalendarNames), the way .NET reads them, and no resx of ours
    // could keep up with every locale. What a resx owns is the wording AROUND them.

    /// <summary>The calendar's step-back affordance, announced to assistive tech.</summary>
    public static string PreviousMonth => SdkResources.PreviousMonth;

    /// <summary>The calendar's step-forward affordance.</summary>
    public static string NextMonth => SdkResources.NextMonth;

    /// <summary>What today's cell is called beyond its date — the ring is paint, this is the word.</summary>
    public static string Today => SdkResources.Today;

    /// <summary>The sheet surface's accessible name when the app supplies none.</summary>
    public static string Spreadsheet => SdkResources.Spreadsheet;

    // ListDetail's compact pane: going back to the list, and the wide pane with nothing chosen.
    public static string Back => SdkResources.Back;
    public static string NothingSelected => SdkResources.NothingSelected;
}
