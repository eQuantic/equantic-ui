using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// An implicit conversion is applied whole: a user-defined one with the standard one beside it, which
/// the bound tree holds as two conversions on one node (#970), and a tuple into a tuple of other element
/// types, whose every element converts as C# converts it (#911).
/// </summary>
public class ConversionChainConformanceTests
{
    private const string Types = """
        public struct Count { public int V; public Count(int v) { V = v; } public static implicit operator int(Count c) => c.V; }
        public struct Ticks { public long V; public Ticks(long v) { V = v; } public static implicit operator Ticks(long value) => new Ticks(value); }
        public struct Money { public decimal V; public Money(decimal v) { V = v; } public static implicit operator Money(decimal value) => new Money(value); }
        public struct Whole { public double V; public Whole(double v) { V = v; } public static explicit operator Whole(double value) => new Whole(value); public static explicit operator double(Whole w) => w.V; }
        """;

    [SkippableTheory]
    // The rows of #970: the operator and then the widening, the widening and then the operator.
    [InlineData("long l = new Count(5); return (l * 2).ToString();")]                                  // 10
    [InlineData("Ticks t = 5; return (t.V * 2).ToString();")]                                          // 10
    // Into every representation an int widens to, and from every one a parameter takes.
    [InlineData("decimal m = new Count(5); return (m / 2).ToString();")]                               // 2.5
    [InlineData("double d = new Count(5); return (d / 2).ToString();")]                                // 2.5
    [InlineData("Money m = 5; return (m.V / 2).ToString();")]                                          // 2.5
    [InlineData("Ticks t = 'A'; return t.V.ToString();")]                                              // 65
    [InlineData("int n = 7; Ticks t = n; return (t.V + 1L).ToString();")]                              // 8
    // At an argument, a return and a branch, the sites C# applies them at.
    [InlineData("long Twice(long x) => x * 2; return Twice(new Count(4)).ToString();")]                // 8
    [InlineData("long Of(Count c) { return c; } return (Of(new Count(4)) + 1L).ToString();")]          // 5
    [InlineData("var c = new Count(4); bool yes = true; long l = yes ? c : 0L; return l.ToString();")] // 4
    [InlineData("long l = 2L + new Count(4); return l.ToString();")]                                   // 6
    // Into a nullable.
    [InlineData("long? l = new Count(5); return (l + 1L).ToString();")]                                // 6
    // An explicit cast, whose operand's conversion is implicit and whose own is not.
    [InlineData("var l = (long)new Count(5); return (l * 2).ToString();")]                             // 10
    [InlineData("int n = 3; var w = (Whole)n; return (w.V / 2).ToString();")]                          // 1.5
    [InlineData("var w = new Whole(2.5); var i = (int)(double)w; return i.ToString();")]               // 2
    [InlineData("var w = new Whole(2.5); var i = (int)w; return (i + 1).ToString();")]                 // 3
    [InlineData("var w = new Whole(2.5); var b = (byte)w; var l = (long)w; return (b + 1) + \"|\" + (l + 1L);")] // 3|3
    public void AUserDefinedConversionWithAStandardOne_AppliesBoth(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Types);
    }

    [SkippableTheory]
    // The rows of #911.
    [InlineData("(int, int) u = (1, 2); (long, long) t2 = u; return (t2.Item1 + 1L).ToString();")]     // 2
    [InlineData("(int, int) u = (1, 2); (long, long) t = u; return t.Equals((object)(1L, 2L)).ToString();")] // True
    // Each element by its own conversion.
    [InlineData("(int, int) u = (1, 2); (double, decimal) w = u; return (w.Item1 / 2) + \"|\" + (w.Item2 / 4);")] // 0.5|0.5
    [InlineData("(char, char) c = ('a', 'b'); (int, long) i = c; return (i.Item1 + 1) + \"|\" + (i.Item2 + 1L);")] // 98|99
    [InlineData("(int, string) p = (1, \"a\"); (long, string) q = p; return (q.Item1 + 1L) + q.Item2;")] // 2a
    [InlineData("(int, int?) p = (1, null); (long, long?) q = p; return (q.Item1 + 1L) + \"|\" + (q.Item2 is null);")] // 2|True
    // Nested, and with a user-defined element.
    [InlineData("((int, int), int) n = ((1, 2), 3); ((long, long), long) m = n; return (m.Item1.Item2 + 1L) + \"|\" + (m.Item2 + 1L);")] // 3|4
    [InlineData("(Count, int) p = (new Count(4), 1); (long, long) q = p; return (q.Item1 + 1L) + \"|\" + (q.Item2 + 1L);")] // 5|2
    // At an argument and a return, its source read once.
    [InlineData("long Sum((long, long) t) => t.Item1 + t.Item2; (int, int) u = (1, 2); return Sum(u).ToString();")] // 3
    [InlineData("(long, long) Up((int, int) t) => t; return (Up((3, 4)).Item2 + 1L).ToString();")]     // 5
    [InlineData("var calls = 0; (int, int) Make() { calls++; return (1, 2); } (long, long) t = Make(); return calls + \"|\" + (t.Item2 + 1L);")] // 1|3
    // Into a nullable tuple, a null staying null.
    [InlineData("(int, int) u = (1, 2); (long, long)? t = u; return (t.Value.Item1 + 1L).ToString();")] // 2
    [InlineData("(int, int)? u = null; (long, long)? t = u; return (t is null).ToString();")]          // True
    // A literal converts its own elements, and a conversion of nothing changes nothing.
    [InlineData("(long, long) t = (1, 2); return (t.Item1 + 1L).ToString();")]                         // 2
    [InlineData("(int a, int b) u = (1, 2); (int, int) v = u; v.Item1 = 9; return u.a + \"|\" + v.Item1;")] // 1|9
    // A conversion makes a new tuple, as .NET's copy is one.
    [InlineData("(int, int) u = (1, 2); (long, long) t = u; t.Item1 = 9L; return u.Item1 + \"|\" + t.Item1;")] // 1|9
    // A checked context reaches the standard conversion beside an element's operator.
    [InlineData("(Count, int) t = (new Count(300), 1); try { checked { (byte, int) w = ((byte, int))t; return w.Item1.ToString(); } } catch (OverflowException e) { return e.Message; }")]
    public void ATupleConvertedToOtherElementTypes_ConvertsEachElement(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Types);
    }
}
