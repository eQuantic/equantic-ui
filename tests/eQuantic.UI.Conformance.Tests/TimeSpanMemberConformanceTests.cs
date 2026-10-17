using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Every member of a <c>TimeSpan</c> the browser is asked for answers as .NET's does, in the invariant
/// culture, pt-BR and sv-SE (#839). The strategy wrote any member camel-cased on the twin, which carried
/// some of them: <c>Subtract</c>, <c>Multiply</c>, <c>Divide</c>, <c>Microseconds</c>, <c>Nanoseconds</c>
/// and the totals in those units were members the twin did not have, a TypeError or undefined on the
/// page, and so were the statics <c>Compare</c> and <c>Equals</c>; <c>*</c>, <c>/</c> and a sign in front
/// of one went to JavaScript's own operators, and answered NaN. Measured on .NET 10: a product and a
/// quotient are rounded to the tick, a half to the even one, a factor or a divisor that is NaN is an
/// ArgumentException naming it, and a result past the span's range an OverflowException.
/// </summary>
public class TimeSpanMemberConformanceTests
{
    private static readonly string?[] Cultures = [null, "pt-BR", "sv-SE"];

    private static readonly string[] Cases =
    [
        // The parts below a millisecond, and the totals in them, of a span either side of zero.
        """
        var a = new TimeSpan(12345678); var b = a.Negate();
        return a.Microseconds + "|" + a.Nanoseconds + "|" + a.TotalMicroseconds + "|" + a.TotalNanoseconds + "|"
            + b.Microseconds + "|" + b.Nanoseconds + "|" + b.TotalMicroseconds + "|" + b.TotalNanoseconds;
        """,
        // A product and a quotient, by a number and by another span, rounded to the tick as .NET rounds it.
        """
        var a = new TimeSpan(12345678);
        return a.Multiply(2.5) + "|" + a.Divide(4) + "|" + a.Divide(new TimeSpan(4)) + "|" + new TimeSpan(5).Divide(2) + "|"
            + new TimeSpan(15).Divide(2) + "|" + new TimeSpan(25).Divide(10) + "|" + new TimeSpan(35).Divide(10) + "|"
            + TimeSpan.Zero.Divide(TimeSpan.Zero) + "|" + TimeSpan.FromSeconds(1).Multiply(-0.5);
        """,
        // The same through the operators, and a sign in front of a span.
        """
        var a = TimeSpan.FromSeconds(1);
        return (a * 2.5) + "|" + (2.0 * a) + "|" + (a / 3) + "|" + (a / TimeSpan.FromSeconds(4)) + "|" + (-a) + "|" + (+a);
        """,
        // What a product and a quotient refuse, in .NET's words.
        """
        var r = "";
        try { r += TimeSpan.FromSeconds(1).Multiply(double.NaN); } catch (ArgumentException e) { r += "AE:" + e.Message; }
        try { r += "|" + TimeSpan.FromSeconds(1).Divide(double.NaN); } catch (ArgumentException e) { r += "|AE:" + e.Message; }
        try { r += "|" + TimeSpan.FromSeconds(1).Divide(0); } catch (OverflowException e) { r += "|OE:" + e.Message; }
        try { r += "|" + TimeSpan.MaxValue.Multiply(2); } catch (OverflowException e) { r += "|OE:" + e.Message; }
        try { r += "|" + (TimeSpan.MaxValue * 1.0); } catch (OverflowException e) { r += "|OE:" + e.Message; }
        return r;
        """,
        // Subtract, and the statics that compare two spans.
        """
        var a = TimeSpan.FromMinutes(90); var b = TimeSpan.FromHours(1);
        return a.Subtract(b) + "|" + TimeSpan.Compare(a, b) + "|" + TimeSpan.Compare(b, a) + "|" + TimeSpan.Compare(a, a) + "|"
            + TimeSpan.Equals(a, b) + "|" + TimeSpan.Equals(a, TimeSpan.FromHours(1.5));
        """,
        // A span scaled by a long or a ulong, on either side, and divided by one (Copilot's review of #958).
        """
        var ts = TimeSpan.FromTicks(3); long lf = 2; ulong uf = 2;
        return (ts * lf).Ticks + "|" + (lf * ts).Ticks + "|" + (ts / lf).Ticks + "|" + (ts * uf).Ticks + "|" + (uf * ts).Ticks + "|" + (ts / uf).Ticks;
        """,
        // A span scaled and divided in place, a nullable one lifted, and an element target read once
        // (Copilot's review of #958).
        """
        var t = TimeSpan.FromTicks(10); t *= 2.5; var t2 = t; t2 /= 4;
        TimeSpan? tn = TimeSpan.FromTicks(6); tn *= 2; TimeSpan? tnull = null; tnull /= 2;
        var spans = new[] { TimeSpan.FromTicks(4) }; var i = 0; spans[i++] *= 3;
        return t.Ticks + "|" + t2.Ticks + "|" + tn?.Ticks + "|" + (tnull is null) + "|" + spans[0].Ticks + "|" + i;
        """,
        // A span scaled by a nullable number, present and null, on either side and in a quotient, and by a
        // char, which C# converts to its code (Copilot's review of #958).
        """
        double? f = null; double? g = 1.5; char c = 'a'; var two = TimeSpan.FromTicks(2);
        return ((two * f) is null) + "|" + ((f * two) is null) + "|" + ((two / f) is null) + "|" + (two * g)?.Ticks + "|" + (g * two)?.Ticks + "|"
            + (two / g)?.Ticks + "|" + (two * c).Ticks + "|" + (c * two).Ticks + "|" + (TimeSpan.FromTicks(970) / c).Ticks;
        """,
    ];

    public static IEnumerable<object?[]> CasesInEachCulture() =>
        Cases.SelectMany(statements => Cultures.Select(culture => new object?[] { statements, culture }));

    [SkippableTheory]
    [MemberData(nameof(CasesInEachCulture))]
    public void ATimeSpansMember_AnswersAsDotNets(string statements, string? culture)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, culture: culture);
    }
}
