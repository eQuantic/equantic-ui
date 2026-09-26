using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A constant reaches JavaScript as its value in its C# type (#444): a decimal as the runtime's exact
/// Decimal, a long as a BigInt. <c>decimal.MaxValue</c> emitted <c>decimal.maxValue</c>, a
/// ReferenceError, because nothing wrote a decimal const field, and a long constant in a number's
/// range was written as a number, which the first long it met threw on. A decimal literal was
/// written from its text, and a parameter's default filled in for a skipped argument by a second,
/// simpler writer. Results are compared as text, which keeps a decimal's scale.
/// </summary>
public class ConstantValueConformanceTests
{
    private const string Prelude =
        "public record Money(decimal Amount) { public const decimal Rate = 1.50m; public const long Small = 5; } " +
        "public static class Cal { public const DayOfWeek First = DayOfWeek.Monday; } " +
        "[Flags] public enum Perm { None = 0, Read = 1, Write = 2 } " +
        "public static class Access { public const Perm Both = Perm.Read | Perm.Write; } " +
        "public static class Texts { public const string Odd = \"\\uD800\\e\\u0001\"; public const string Pair = \"\\uD83D\\uDE00\"; }";

    [SkippableTheory]
    // decimal's five constants, each reached through its type.
    [InlineData("return decimal.MaxValue.ToString();")]
    [InlineData("return decimal.MinValue.ToString();")]
    [InlineData("return decimal.Zero.ToString();")]
    [InlineData("return decimal.One.ToString();")]
    [InlineData("return decimal.MinusOne.ToString();")]
    [InlineData("return (decimal.Zero + decimal.One + decimal.MinusOne).ToString();")]
    [InlineData("return Math.Round(decimal.MaxValue, 2).ToString();")]
    [InlineData("return (decimal.MaxValue - 1).ToString();")]
    [InlineData("return (decimal.MinValue < decimal.MaxValue) + \"|\" + (decimal.One == 1m);")]
    // The rows of #444, the type spelled either way.
    [InlineData("return decimal.One + \"|\" + decimal.Zero + \"|\" + decimal.MinusOne;")]
    [InlineData("return Decimal.MaxValue.ToString();")]
    // A decimal const of the app's own: a record's, a local one, and one as a default.
    [InlineData("return Money.Rate.ToString();")]
    [InlineData("return (Money.Rate * 2).ToString();")]
    [InlineData("const decimal local = 2.50m; return (local * 2).ToString();")]
    // A decimal literal is its value: a digit separator is not a decimal's grammar.
    [InlineData("return (1_000.5m).ToString();")]
    [InlineData("return (1_000_000m + 0.25m).ToString();")]
    public void ADecimalConstant_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }

    [SkippableTheory]
    // A long constant is a BigInt whatever its size, so it meets another long.
    [InlineData("long t = 20000000; return (t / TimeSpan.TicksPerSecond).ToString();")]
    [InlineData("return (TimeSpan.TicksPerDay * 2).ToString();")]
    [InlineData("return (TimeSpan.TicksPerMillisecond + TimeSpan.TicksPerMinute).ToString();")]
    [InlineData("return (Money.Small + 1L).ToString();")]
    [InlineData("return Money.Small.ToString();")]
    [InlineData("return (long.MaxValue - 1).ToString();")]
    [InlineData("return ulong.MaxValue.ToString();")]
    public void ALongConstant_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }

    [SkippableTheory]
    // A parameter's default filled in for an argument a named one skips, in the parameter's type.
    [InlineData("decimal F(decimal a = 1.5m, int b = 0) => a * b; return F(b: 2).ToString();")]
    [InlineData("decimal M(decimal a = decimal.MaxValue, int b = 0) => a - b; return M(b: 1).ToString();")]
    [InlineData("long L(long a = 5, int b = 1) => a + b; return L(b: 2).ToString();")]
    [InlineData("string G(char c = 'x', int n = 1) => new string(c, n); return G(n: 3);")]
    [InlineData("string H(string s = \"it's\", int n = 1) => s + n; return H(n: 2);")]
    [InlineData("double D(float f = 0.1f, int b = 1) => f; return D(b: 2).ToString();")]
    public void ASkippedParametersDefault_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    [SkippableTheory]
    // decimal's own Round, which the BCL audit grades now that it grades decimal's static surface.
    [InlineData("return decimal.Round(2.5m).ToString();")]
    [InlineData("return decimal.Round(3.5m).ToString();")]
    [InlineData("return decimal.Round(-2.5m).ToString();")]
    [InlineData("return decimal.Round(3.14159m, 2).ToString();")]
    [InlineData("return decimal.Round(1.005m, 2).ToString();")]
    [InlineData("return decimal.Round(1.5m, 5).ToString();")]
    [InlineData("return decimal.Round(2.345m, 2, MidpointRounding.AwayFromZero).ToString();")]
    [InlineData("return decimal.Round(-2.345m, 2, MidpointRounding.ToZero).ToString();")]
    [InlineData("return decimal.Round(2.5m, 0, MidpointRounding.ToEven).ToString();")]
    [InlineData("try { return decimal.Round(1m, 29).ToString(); } catch { return \"threw\"; }")]
    // The audit grades Round by name and arity: its two-argument line claims this overload too.
    [InlineData("return decimal.Round(2.5m, MidpointRounding.AwayFromZero).ToString();")]
    [InlineData("return decimal.Round(-2.5m, MidpointRounding.ToZero).ToString();")]
    [InlineData("return decimal.Round(2.5m, MidpointRounding.ToEven).ToString();")]
    public void DecimalRound_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>
    /// A decimal constant in a pattern or a case label matches by value: the runtime's Decimal is an
    /// object, which <c>===</c> and a native switch compared by identity, so <c>d is 1m</c> never matched,
    /// and <c>d is decimal.One</c> went from a ReferenceError to the same silent false (found in review).
    /// </summary>
    [SkippableTheory]
    // An `is` pattern, the constant spelled every way C# converts to a decimal.
    [InlineData("decimal d = decimal.One; return (d is decimal.One).ToString();")]
    [InlineData("decimal d = 1.0m; return (d is 1m).ToString();")]
    [InlineData("decimal d = 2m; return (d is 1m).ToString();")]
    [InlineData("decimal d = 1m; return (d is 1).ToString();")]
    [InlineData("decimal? n = null; return (n is 1m).ToString();")]
    [InlineData("object o = 1m; return (o is 1m).ToString();")]
    [InlineData("object o = 1; return (o is 1m).ToString();")]
    [InlineData("decimal d = 3m; return (d is not decimal.One).ToString();")]
    [InlineData("decimal d = 2m; return (d is 1m or 2m).ToString();")]
    // A switch statement, which a decimal label turns into the chain, and a switch expression.
    [InlineData("decimal d = 1m; switch (d) { case decimal.One: return \"one\"; default: return \"other\"; }")]
    [InlineData("decimal d = 2m; switch (d) { case 1: return \"one\"; case 2: return \"two\"; default: return \"other\"; }")]
    [InlineData("decimal d = 2.00m; var r = \"none\"; switch (d) { case 1m: r = \"one\"; break; case 2m: r = \"two\"; break; } return r;")]
    [InlineData("decimal d = Money.Rate; switch (d) { case Money.Rate: return \"rate\"; default: return \"other\"; }")]
    [InlineData("decimal d = 0m; return d switch { 1m => \"one\", decimal.Zero => \"zero\", _ => \"other\" };")]
    [InlineData("decimal d = 1.50m; return d switch { Money.Rate => \"rate\", _ => \"other\" };")]
    [InlineData("object o = 1.5m; return o switch { 1.5m => \"match\", _ => \"other\" };")]
    public void ADecimalConstant_MatchesByValue_InAPatternOrACaseLabel(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }

    /// <summary>
    /// A constant whose TYPE is an enum is that enum's representation: a member's camelCase name, or a
    /// [Flags] enum's number. Its value arrives as the underlying integer, which written as a number read
    /// the name table at <c>[1]</c>, and a flags default reached its parameter as a name (found in review).
    /// </summary>
    [SkippableTheory]
    // A const whose type is an enum, reached through its class.
    [InlineData("return Cal.First.ToString();")]
    [InlineData("return (Cal.First == DayOfWeek.Monday).ToString();")]
    [InlineData("return ((int)Cal.First).ToString();")]
    [InlineData("return ((int)Access.Both).ToString();")]
    [InlineData("return Access.Both.HasFlag(Perm.Write).ToString();")]
    // A skipped default of a [Flags] enum is its number, as the function's own default is.
    [InlineData("int F(Perm p = Perm.Read, int n = 0) => (int)p; return F(n: 1).ToString();")]
    [InlineData("bool F(Perm p = Perm.Write, int n = 0) => p.HasFlag(Perm.Write); return F(n: 1).ToString();")]
    [InlineData("int F(Perm p = Perm.Read, int n = 0) => (int)(p | Perm.Write); return F(n: 1).ToString();")]
    [InlineData("string F(DayOfWeek? d = DayOfWeek.Friday, int n = 0) => d.Value.ToString(); return F(n: 1);")]
    [InlineData("string F(DayOfWeek d = DayOfWeek.Friday, int n = 0) => d.ToString(); return F(n: 1);")]
    public void AnEnumTypedConstant_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }

    /// <summary>
    /// A constant's text is escaped where it cannot stand for itself in the module: a LONE surrogate has
    /// no UTF-8 bytes, so a module holding one could not be written at all, and a NUL or a line separator
    /// is invisible there. A surrogate pair is one character, written as it is (found in review).
    /// </summary>
    [SkippableTheory]
    // A lone surrogate, a NUL and the line separators, in a default, a const and a literal.
    [InlineData("int G(char c = (char)0xD800, int n = 0) => c; return G(n: 1).ToString();")]
    [InlineData("int G(char c = (char)0, int n = 0) => c; return G(n: 1).ToString();")]
    [InlineData("string S(string s = \"a\\u2028b\\0c\", int n = 0) => s; return ((int)S(n: 1)[1]).ToString() + \"|\" + ((int)S(n: 1)[3]).ToString();")]
    [InlineData("return ((int)Texts.Odd[0]).ToString() + \"|\" + ((int)Texts.Odd[1]).ToString() + \"|\" + Texts.Odd.Length;")]
    [InlineData("return Texts.Pair.Length + \"|\" + char.IsSurrogatePair(Texts.Pair, 0);")]
    [InlineData("return ((int)\"\\uDC00x\"[0]).ToString();")]
    [InlineData("const string local = \"\\u2029\"; return ((int)local[0]).ToString();")]
    // A tab stands for itself, and stays one.
    [InlineData("return (\"a\\tb\").Length + \"|\" + ((int)\"a\\tb\"[1]).ToString();")]
    public void AConstantsText_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }
}
