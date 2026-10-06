using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A custom date picture is drawn as .NET's <c>FormatCustomized</c> draws it (#470): <c>/</c> and
/// <c>:</c> are the culture's date and time separators (de-DE writes <c>dd/MM/yyyy</c> as
/// <c>24.09.2026</c>, <c>ar</c> puts a right-to-left mark before its slash), <c>z</c>, <c>zz</c> and
/// <c>zzz</c> the offset (the host's for a DateTime of no kind, and both sides run on the same host),
/// <c>g</c> the era, and a run of a letter is one token whose length decides what it writes
/// (<c>yyyyy</c> is a five-digit year, <c>hhh</c> a two-digit hour). They were written as they stand.
/// </summary>
public class DatePictureConformanceTests
{
    private const string Moment = "var d = new DateTime(2026, 9, 24, 10, 30, 15, 250); ";

    private static readonly string[] Cases =
    [
        // The separators, and the same characters quoted or escaped, which are text.
        Moment + "return $\"{d.ToString(\"dd/MM/yyyy\")}|{d.ToString(\"HH:mm:ss\")}|{d.ToString(\"d/M/yy\")}|{d.ToString(\"dd'/'MM\")}|{d.ToString(\"dd\\\\/MM\")}|{d.ToString(\"H:m:s\")}\";",
        // The era, at any length, of a DateTime and a DateOnly.
        Moment + "return $\"{d.ToString(\"yyyy g\")}|{d.ToString(\"gg yyyy\")}|{d.ToString(\"%g\")}|{new DateOnly(2026, 9, 24).ToString(\"ggg\")}\";",
        // The offset of a DateTime of no kind is the host's at that time, in summer and in winter.
        Moment + "return $\"{d.ToString(\"%z\")}|{d.ToString(\"zz\")}|{d.ToString(\"zzz\")}|{d.ToString(\"HH:mm zzz\")}|[{d.ToString(\"%K\")}]|{new DateTime(2026, 1, 15).ToString(\"zzz\")}\";",
        // A run is one token, its length deciding what it writes.
        Moment + "return $\"{d.ToString(\"yyyyy\")}|{d.ToString(\"yyy\")}|{d.ToString(\"%y\")}|{d.ToString(\"MMMMM\")}|{d.ToString(\"ddddd\")}|{d.ToString(\"hhh HHH mmm sss\")}|{d.ToString(\"ttt\")}\";",
        Moment + "return $\"{d.ToString(\"fffffff\")}|{d.ToString(\"FFFFFFF\")}|{d.ToString(\"%f\")}|{d.ToString(\"%F\")}|{d.ToString(\"%d\")}|{d.ToString(\"%M\")}|{d.ToString(\"%h\")}|{d.ToString(\"%t\")}|{d.ToString(\"%s\")}\";",
        // Quoted text, an escape inside it, and the other quote.
        Moment + "return $\"{d.ToString(\"'at' HH\\\\:mm\")}|{d.ToString(\"dd 'de' MMMM\")}|{d.ToString(\"\\\"day\\\" d\")}|{d.ToString(\"'it\\\\'s' HH\")}\";",
        // What .NET refuses: a one-letter format that is no specifier, a fraction past seven digits,
        // an unclosed quote, a trailing escape and a bare percent.
        Moment + "try { return d.ToString(\"z\"); } catch (Exception e) { return e.Message; }",
        Moment + "try { return d.ToString(\"ffffffff\"); } catch (Exception e) { return e.Message; }",
        Moment + "try { return d.ToString(\"dd 'open\"); } catch (Exception e) { return e.Message; }",
        Moment + "try { return d.ToString(\"dd \\\\\"); } catch (Exception e) { return e.Message; }",
        Moment + "try { return d.ToString(\"dd%\"); } catch (Exception e) { return e.Message; }",
    ];

    public static IEnumerable<object?[]> CasesInEveryCulture() =>
        Cases.SelectMany(statements =>
            NumberTextInTheCultureConformanceTests.Cultures.Select(culture => new object?[] { statements, culture }));

    [SkippableTheory]
    [MemberData(nameof(CasesInEveryCulture))]
    public void ACustomDatePicture_IsDrawnAsDotNetDrawsIt(string statements, string? culture)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, culture: culture);
    }
}
