using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A constant reaches JavaScript as its value in its C# type (#447): a decimal as the runtime's exact
/// Decimal, a long as a BigInt. <c>decimal.MaxValue</c> emitted <c>decimal.maxValue</c>, a
/// ReferenceError, because nothing wrote a decimal const field, and a long constant in a number's
/// range was written as a number, which the first long it met threw on. A decimal literal was
/// written from its text, and a parameter's default filled in for a skipped argument by a second,
/// simpler writer. Results are compared as text, which keeps a decimal's scale.
/// </summary>
public class ConstantValueConformanceTests
{
    private const string Prelude =
        "public record Money(decimal Amount) { public const decimal Rate = 1.50m; public const long Small = 5; }";

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
    public void DecimalRound_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
