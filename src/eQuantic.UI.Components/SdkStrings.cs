using System.Text;
using eQuantic.UI.Primitives;

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
    // CookieConsent — the question, the two answers and the policy link.
    public static string CookieConsentTitle => SdkResources.CookieConsentTitle;
    public static string CookieConsentBody => SdkResources.CookieConsentBody;
    public static string AcceptCookies => SdkResources.AcceptCookies;
    public static string RejectCookies => SdkResources.RejectCookies;
    public static string PrivacyPolicy => SdkResources.PrivacyPolicy;

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

    /// <summary>A date picker's accessible name when the app supplies none.</summary>
    public static string ChooseDate => SdkResources.ChooseDate;

    /// <summary>A time picker's accessible name when the app supplies none.</summary>
    public static string ChooseTime => SdkResources.ChooseTime;

    /// <summary>
    /// What shape the typed row expects: <c>MM/DD/YYYY</c> in the United States,
    /// <c>DD/MM/AAAA</c> in Brazil, <c>YYYY-MM-DD</c> in Sweden.
    /// <para>
    /// Half derived, half translated, because the hint IS two facts. The ORDER and the separators
    /// belong to the culture's data — <see cref="CalendarNames.ShortDatePattern"/>, the same
    /// string the parser reads, so the hint can never ask for an order the field refuses. The
    /// LETTERS belong to the language: Portuguese writes the year AAAA, for ano.
    /// </para>
    /// </summary>
    public static string DateFormatHint => Hint(CalendarNames.ShortDatePattern, DateFormatLetters);

    /// <summary>The three letters this language stands the parts of a date on, in the fixed order
    /// day, month, year — "DMY" in English, "DMA" in Portuguese. Three characters rather than a
    /// finished hint, because the arrangement is the culture's job and not the translator's.
    /// </summary>
    public static string DateFormatLetters => SdkResources.DateFormatLetters;

    /// <summary>
    /// The pattern with each component widened to the shape people write placeholders in
    /// (<c>M</c> and <c>MM</c> both read MM) and everything else — separators, the era marker
    /// some calendars lead with — kept exactly as the culture has it.
    /// </summary>
    private static string Hint(string pattern, string letters)
    {
        var day = letters.Length > 0 ? letters[0] : 'D';
        var month = letters.Length > 1 ? letters[1] : 'M';
        var year = letters.Length > 2 ? letters[2] : 'Y';

        var hint = new StringBuilder();
        var i = 0;
        while (i < pattern.Length)
        {
            var letter = pattern[i];
            // A QUOTED literal is text, not a component: Bulgarian's short pattern ends in
            // `'г'.` for "година", and reading its letters would both mangle the word and leave
            // the quote marks standing in the hint.
            if (letter == '\'')
            {
                var close = pattern.IndexOf('\'', i + 1);
                if (close < 0)
                {
                    hint.Append(pattern.Substring(i + 1));
                    break;
                }
                hint.Append(pattern.Substring(i + 1, close - i - 1));
                i = close + 1;
                continue;
            }

            var run = 1;
            while (i + run < pattern.Length && pattern[i + run] == letter) run++;
            if (letter == 'd') hint.Append(day).Append(day);
            else if (letter == 'M') hint.Append(month).Append(month);
            else if (letter == 'y') hint.Append(year).Append(year).Append(year).Append(year);
            else hint.Append(pattern.Substring(i, run));
            i += run;
        }
        return hint.ToString();
    }

    /// <summary>The sheet surface's accessible name when the app supplies none.</summary>
    public static string Spreadsheet => SdkResources.Spreadsheet;

    // ListDetail's compact pane: going back to the list, and the wide pane with nothing chosen.
    public static string Back => SdkResources.Back;
    public static string NothingSelected => SdkResources.NothingSelected;

    // A chart's footer: the switch to its table view (the WCAG twin of every chart) and back.
    public static string ShowAsTable => SdkResources.ShowAsTable;
    public static string ShowAsChart => SdkResources.ShowAsChart;
}
