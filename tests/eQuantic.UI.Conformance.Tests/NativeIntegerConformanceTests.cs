using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A native integer is 64 bits wide on every target this SDK ships to, the server that renders the page
/// included, so <c>nint</c> and <c>nuint</c> are held as <c>long</c> and <c>ulong</c> are: a BigInt, whatever
/// the value came from (#649). A value from an <c>int</c> was a number and one from a <c>long</c> a BigInt,
/// and JavaScript refuses to mix the two.
/// </summary>
public class NativeIntegerConformanceTests
{
    [SkippableTheory]
    // The rows of #649: an int, a long and a constant meeting in one expression.
    [InlineData("nint a = (nint)9007199254740993; int i = 5; nint b = i; nint c = a + b; return c.ToString();")]    // 9007199254740998
    [InlineData("long big = 9007199254740993L; nint n = (nint)big; return (n + 2).ToString();")]             // 9007199254740995
    [InlineData("nint n = 5; nuint u = 7; return (n * 2).ToString() + \"|\" + (u + 1).ToString();")]         // 10|8
    [InlineData("uint v = 3000000000; nuint u = v; return (u * 2).ToString();")]                             // 6000000000
    // Every operator, at a width of 64 bits.
    [InlineData("nint n = -7; return (n / 2) + \"|\" + (n % 2) + \"|\" + (n >> 1) + \"|\" + (n << 1) + \"|\" + (n >>> 60);")]
    [InlineData("nuint u = 1; return (u << 63).ToString() + \"|\" + (u << 63 >> 62).ToString();")]           // 9223372036854775808|2
    [InlineData("nint n = 6; return (n & 3) + \"|\" + (n | 1) + \"|\" + (n ^ 2) + \"|\" + (~n);")]           // 2|7|4|-7
    [InlineData("nint n = 5; return (n > 3) + \"|\" + (n == 5) + \"|\" + (n != 5) + \"|\" + (-n);")]         // True|True|False|-5
    [InlineData("nint n = 5; n++; n += 2; n *= 3; n--; return n.ToString();")]                               // 23
    [InlineData("nint n = unchecked((nint)long.MaxValue); unchecked { n = n + 1; } return n.ToString();")]   // -9223372036854775808
    [InlineData("nint n = (nint)long.MaxValue; try { checked { n = n + 1; } return n.ToString(); } catch (OverflowException e) { return e.Message; }")]
    // Conversions out of it, each to the type's own representation.
    [InlineData("nint n = 5; int i = (int)n; return (i + 1).ToString();")]                                   // 6
    [InlineData("nint n = 5; long l = n; return (l + 1L).ToString();")]                                      // 6
    [InlineData("nint n = 5; double d = n; float f = n; decimal m = n; return (d / 2) + \"|\" + (f / 2) + \"|\" + (m / 2);")] // 2.5|2.5|2.5
    [InlineData("nint n = 300; return ((byte)n) + \"|\" + ((short)n) + \"|\" + ((nuint)n).ToString();")]     // 44|300|300
    [InlineData("nint n = -1; return unchecked((nuint)n).ToString() + \"|\" + unchecked((ulong)n).ToString();")] // 18446744073709551615 twice
    [InlineData("double d = 2.9; nint n = (nint)d; nuint u = (nuint)d; return n.ToString() + \"|\" + u.ToString();")] // 2|2
    [InlineData("char c = 'A'; nint n = c; return (n + 1).ToString();")]                                     // 66
    // Printed, in every way a number is.
    [InlineData("nint n = 255; return n.ToString(\"X\") + \"|\" + $\"{n}\" + \"|\" + (\"v=\" + n) + \"|\" + string.Join(\",\", new nint[] { 1, n });")]
    [InlineData("nint n = -1; return n.ToString(\"X\");")]                                                   // FFFFFFFFFFFFFFFF
    // A zero, in a default, an array, a nullable and a member.
    [InlineData("nint n = default; var a = new nint[2]; return (n + 1).ToString() + \"|\" + (a[0] + 1).ToString();")] // 1|1
    [InlineData("nint? n = null; nint? m = 5; return (n ?? 7).ToString() + \"|\" + (m + 1).ToString() + \"|\" + (n is null);")] // 7|6|True
    [InlineData("return (new Holder().N + 1).ToString() + \"|\" + new Holder(4).N.ToString();")]             // 1|4
    // A pattern, a switch, a branch and a collection.
    [InlineData("nint n = 5; return (n is 5) + \"|\" + (n is > 3 and < 9) + \"|\" + (n switch { 5 => \"five\", _ => \"other\" });")] // True|True|five
    [InlineData("bool yes = true; nint n = yes ? 1 : 2; return (n + 1).ToString();")]                        // 2
    [InlineData("var l = new List<nint> { 1, 2 }; l.Add(3); return l.Contains(2) + \"|\" + l.Count + \"|\" + l.Max().ToString();")] // True|3|3
    [InlineData("var d = new Dictionary<nint, string> { [1] = \"a\" }; return d[1] + \"|\" + d.ContainsKey(2);")] // a|False
    // Equal to itself as a member of a record, and read from text.
    [InlineData("return (new Pair(1, 2) == new Pair(1, 2)) + \"|\" + new Pair(1, 2).Equals(new Pair(1, 3));")] // True|False
    [InlineData("nint n = nint.Parse(\"12\"); return (n + 1).ToString() + \"|\" + nint.TryParse(\"x\", out var m);")] // 13|False
    public void ANativeInteger_IsHeldAs64Bits(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, """
            public struct Holder { public nint N; public Holder(nint n) { N = n; } }
            public record Pair(nint A, nint B);
            """);
    }
}
