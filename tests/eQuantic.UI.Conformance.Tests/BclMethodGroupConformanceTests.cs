using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A method group of a .NET member is the delegate its call is, both sides executed, in the invariant
/// culture, pt-BR and sv-SE (#839). The group went out as a name, unbound or called with no argument:
/// <c>Func&lt;string, string&gt; f = ts.ToString</c> was <c>ts.toString</c>, a TypeError reading
/// <c>this.ticks</c>; <c>items.Select(DateTime.Parse)</c> called the twin's parse once with no text,
/// <c>items.Select(Convert.ToDateTime)</c> named a <c>Convert</c> nothing defines, and
/// <c>Select(Math.Round)</c> rounded a half away from zero, JavaScript's way. Each group now answers
/// through its call, over the delegate's own parameters, so the index <c>Select</c> hands a callback never
/// reaches a twin's culture flag, and its receiver is read once, where the delegate is made.
/// </summary>
public class BclMethodGroupConformanceTests
{
    private static readonly string?[] Cultures = [null, "pt-BR", "sv-SE"];

    private static readonly string[] Cases =
    [
        // A TimeSpan's ToString, with a format and with none, its receiver read when the delegate is made.
        """
        var t = new TimeSpan(1, 2, 3, 4, 500); Func<string, string> f = t.ToString; Func<string> g = t.ToString;
        t = TimeSpan.Zero;
        return f("g") + "|" + f("c") + "|" + f(@"hh\:mm") + "|" + f(null) + "|" + g();
        """,
        // A receiver that is a call runs once, however many times the delegate is called.
        """
        var calls = 0; TimeSpan Make() { calls++; return TimeSpan.FromMinutes(90); }
        Func<string, string> f = Make().ToString;
        var a = f("c"); var b = f("g");
        return calls + "|" + a + "|" + b;
        """,
        // The readers of a date as Select's callback: the text in the page's culture, the index never
        // read as the invariant culture's flag.
        """
        var texts = new List<string> { "2024-01-15", "2024/01/15 21:30", "01/02/2024" };
        return string.Join("|", texts.Select(DateTime.Parse).Select(d => d.ToString("o"))) + "/"
            + string.Join("|", texts.Select(Convert.ToDateTime).Select(d => d.ToString("o"))) + "/"
            + string.Join("|", texts.Select(DateTimeOffset.Parse).Select(d => d.DateTime.ToString("o")));
        """,
        """
        var days = new List<string> { "2024-01-15", "01/02/2024" }; var clocks = new List<string> { "09:30", "21:30:15" };
        return string.Join("|", days.Select(DateOnly.Parse).Select(d => d.ToString("o"))) + "/"
            + string.Join("|", clocks.Select(TimeOnly.Parse).Select(t => t.ToString("o")));
        """,
        // A TimeSpan's reader, and the culture's own separator past the first text.
        """
        try { return string.Join("|", new List<string> { "1:02:03", "1:02:03.5", "1:02:03,5" }.Select(TimeSpan.Parse)); }
        catch (FormatException e) { return "FE:" + e.Message; }
        """,
        // The readers of a number and of a Guid.
        """
        var numbers = new List<string> { "15", "-2", "1000" };
        return string.Join("|", numbers.Select(int.Parse)) + "/" + string.Join("|", numbers.Select(decimal.Parse)) + "/"
            + string.Join("|", new List<string> { "2", "-3" }.Select(double.Parse)) + "/"
            + string.Join("|", new List<string> { "00000000-0000-0000-0000-000000000001" }.Select(Guid.Parse));
        """,
        // A static of a date or a span, called with the delegate's arguments.
        """
        Func<DateTime, DateTime, int> compare = DateTime.Compare; Func<int, int, int> days = DateTime.DaysInMonth;
        Func<int, bool> leap = DateTime.IsLeapYear; Func<double, TimeSpan> seconds = TimeSpan.FromSeconds;
        return compare(new DateTime(2024, 1, 2), new DateTime(2024, 1, 1)) + "|" + days(2024, 2) + "|" + leap(2023) + "|" + seconds(1.5);
        """,
        // A date's instance methods, its value read when the delegate is made.
        """
        var d = new DateTime(2024, 1, 15, 21, 30, 5); Func<string> shortDate = d.ToShortDateString; Func<string, string> text = d.ToString;
        Func<double, DateTime> add = d.AddDays;
        d = DateTime.MinValue;
        return shortDate() + "|" + text("o") + "|" + text("D") + "|" + text(null) + "|" + add(1.5).ToString("o");
        """,
        // A string's and a list's members.
        """
        var w = "Hello"; Func<string, bool> has = w.Contains; Func<string> upper = w.ToUpper; Func<string, bool> starts = w.StartsWith;
        Func<int, string> from = w.Substring; Func<char[], string> trim = w.Trim;
        w = "x";
        var list = new List<int> { 1, 2 }; Action<int> add = list.Add; Func<int, bool> holds = list.Contains; Func<int, int> at = list.IndexOf;
        add(3);
        return has("ell") + "|" + upper() + "|" + starts("He") + "|" + from(1) + "|" + trim(new[] { 'H', 'o' }) + "|"
            + holds(3) + "|" + at(2) + "|" + list.Count;
        """,
        // A number rounded as .NET rounds it, a half to the even neighbour.
        """
        Func<double, double> round = Math.Round; Func<double, double> abs = Math.Abs; Func<int, int, int> max = Math.Max;
        return string.Join("|", new[] { 0.5, 1.5, 2.5, -0.5, -1.5 }.Select(round)) + "/" + abs(-2.5) + "/" + max(3, 7);
        """,
        // A text's statics, and a char's.
        """
        var items = new List<string> { "a", "", null, " " };
        return items.Count(string.IsNullOrEmpty) + "|" + items.Count(string.IsNullOrWhiteSpace) + "|"
            + new List<string> { "a", "b", "c" }.Aggregate(string.Concat) + "|" + "a1b2".Count(char.IsDigit) + "|"
            + string.Concat("aB".Select(char.ToUpperInvariant));
        """,
        // An extension's group keeps the sequence it was made over.
        """
        var list = new List<int> { 1 }; Func<bool> any = list.Any; Func<int, bool> holds = list.Contains;
        var before = any(); list.Clear();
        return before + "|" + any() + "|" + holds(1);
        """,
        // A null receiver refused where the delegate is made: a method called directly, and one looked up on it.
        """
        string s = null; var r = "";
        try { Func<string> f = s.ToUpper; r += "made"; } catch (NullReferenceException e) { r += "NRE:" + e.Message; } catch (ArgumentException e) { r += "AE:" + e.Message; }
        IList<int> l = null;
        try { Func<int, bool> f = l.Contains; r += "|made"; } catch (NullReferenceException e) { r += "|NRE:" + e.Message; } catch (ArgumentException e) { r += "|AE:" + e.Message; }
        return r;
        """,
        // A delegate built with new is the function it is built from.
        """
        var parse = new Func<string, DateTime>(DateTime.Parse); var twice = new Func<int, int>(x => x * 2); var again = new Func<int, int>(twice);
        return parse("2024-01-15").Day + "|" + twice(3) + "|" + again(4);
        """,
    ];

    public static IEnumerable<object?[]> CasesInEachCulture() =>
        Cases.SelectMany(statements => Cultures.Select(culture => new object?[] { statements, culture }));

    [SkippableTheory]
    [MemberData(nameof(CasesInEachCulture))]
    public void AMethodGroupOfADotNetMember_IsTheDelegateItsCallIs(string statements, string? culture)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, culture: culture);
    }

    /// <summary>A group reached bare through <c>using static</c> is the group its qualified spelling is.</summary>
    [SkippableTheory]
    [InlineData("using static System.Math;", "Func<double, double> round = Round; return round(2.5) + \"|\" + new[] { -1.5, 3.0 }.Select((Func<double, double>)Abs).Sum();")]
    [InlineData("using static System.TimeSpan;", "Func<double, TimeSpan> minutes = FromMinutes; return minutes(1.5).ToString();")]
    public void AGroupThroughUsingStatic_IsItsQualifiedGroup(string usingStatic, string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, usingStatic);
    }
}
