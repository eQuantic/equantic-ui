using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The members of a <c>DateTime</c>, a <c>DateOnly</c>, a <c>TimeOnly</c> and a <c>DateTimeOffset</c> that
/// their strategies wrote camel-cased on a twin that did not carry them (#839), each now as .NET answers
/// it, in the invariant culture, pt-BR and sv-SE: the statics <c>Compare</c>, <c>Equals</c> and
/// <c>UnixEpoch</c>, a date's <c>ToDateTime</c>, a time's <c>Add</c>, <c>IsBetween</c>, <c>ToTimeSpan</c>,
/// <c>Microsecond</c> and <c>Nanosecond</c>, an instant's <c>DayOfYear</c>, <c>TotalOffsetMinutes</c>,
/// <c>ToUniversalTime</c> and <c>EqualsExact</c>. <c>Subtract</c> of a date or an instant is the span
/// between them, where the twin's <c>subtract</c> took the other value as a span. The members no twin
/// carries are refused at the build (<c>UntranslatedNameTests</c>).
/// </summary>
public class DateMemberConformanceTests
{
    private static readonly string?[] Cultures = [null, "pt-BR", "sv-SE"];

    private static readonly string[] Cases =
    [
        """
        var a = new DateTime(2024, 1, 15, 21, 30, 5, DateTimeKind.Utc); var b = new DateTime(2024, 1, 15, 21, 30, 5, DateTimeKind.Local);
        var c = new DateTime(2023, 12, 31);
        return DateTime.Compare(a, c) + "|" + DateTime.Compare(c, a) + "|" + DateTime.Compare(a, b) + "|" + DateTime.Equals(a, b) + "|"
            + DateTime.Equals(a, c) + "|" + DateTime.UnixEpoch.ToString("o") + "|" + DateTime.UnixEpoch.Kind + "|" + a.Subtract(c);
        """,
        """
        var d = new DateOnly(2024, 2, 29); var t = new TimeOnly(774051234567);
        return d.ToDateTime(t).ToString("o") + "|" + d.ToDateTime(t, DateTimeKind.Utc).ToString("o") + "|" + d.ToDateTime(t).Kind;
        """,
        """
        var t = new TimeOnly(774051234567); var late = new TimeOnly(23, 0); var early = new TimeOnly(1, 0);
        return t.Microsecond + "|" + t.Nanosecond + "|" + t.Add(TimeSpan.FromHours(3)).ToString("o") + "|" + t.Add(TimeSpan.FromHours(-22)).ToString("o") + "|"
            + t.ToTimeSpan() + "|" + t.IsBetween(early, late) + "|" + t.IsBetween(late, early) + "|" + new TimeOnly(0, 30).IsBetween(late, early) + "|"
            + early.IsBetween(early, late) + "|" + late.IsBetween(early, late);
        """,
        """
        var a = new DateTimeOffset(2024, 3, 1, 23, 30, 0, TimeSpan.FromHours(-3)); var b = new DateTimeOffset(2024, 3, 2, 2, 30, 0, TimeSpan.Zero);
        return a.DayOfYear + "|" + a.TotalOffsetMinutes + "|" + a.ToUniversalTime().ToString("o") + "|" + a.EqualsExact(b) + "|" + a.Equals(b) + "|"
            + a.EqualsExact(a.ToOffset(TimeSpan.FromHours(-3))) + "|" + DateTimeOffset.Compare(a, b) + "|" + DateTimeOffset.Equals(a, b) + "|"
            + DateTimeOffset.UnixEpoch.ToString("o") + "|" + a.Subtract(b) + "|" + a.Subtract(TimeSpan.FromDays(1)).ToString("o");
        """,
        // Named arguments in another order than the parameters', each in its parameter's place and evaluated
        // as written (Copilot's review of #958).
        """
        var d = new DateOnly(2024, 2, 29); var t = new TimeOnly(10, 30); var early = new TimeOnly(1, 0); var late = new TimeOnly(23, 0);
        var earlier = new DateTime(2024, 1, 1); var later = new DateTime(2024, 6, 1);
        var o1 = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero); var o2 = o1.AddDays(1);
        var log = ""; DateTime At(int k) { log += k; return earlier.AddDays(k); }
        return d.ToDateTime(kind: DateTimeKind.Utc, time: t).ToString("o") + "|" + t.IsBetween(end: late, start: early) + "|"
            + DateTime.Compare(t2: later, t1: earlier) + "|" + DateTimeOffset.Compare(second: o2, first: o1) + "|"
            + DateTime.Compare(t2: At(2), t1: At(1)) + "|" + log + "|"
            + new DateOnly(day: 29, month: 2, year: 2024).ToString("o") + "|" + new TimeSpan(seconds: 3, minutes: 2, hours: 1);
        """,
        // An instant beside a date: C# converts the date to an instant and binds the instant's operator, both
        // orders (#961).
        """
        var a = new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero); var b = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return (a - b) + "|" + (b - a) + "|" + (a < b) + "|" + (a == b) + "|" + (a > b) + "|" + (a == new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc));
        """,
    ];

    public static IEnumerable<object?[]> CasesInEachCulture() =>
        Cases.SelectMany(statements => Cultures.Select(culture => new object?[] { statements, culture }));

    [SkippableTheory]
    [MemberData(nameof(CasesInEachCulture))]
    public void ADatesMember_AnswersAsDotNets(string statements, string? culture)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, culture: culture);
    }
}
