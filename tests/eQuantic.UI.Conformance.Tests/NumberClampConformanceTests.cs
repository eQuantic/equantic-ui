using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// <c>Math.Clamp</c> and its siblings over every number the browser holds as a JavaScript number or a BigInt,
/// on both sides (#1824). A minimum above the maximum is refused with .NET's <c>ArgumentException</c>, the
/// two values written in the current culture as their type writes them; the browser answered the maximum.
/// The value is then compared in .NET's order, the minimum first, which `Math.min(Math.max(…))` is not: a NaN
/// bound left the answer NaN, and a negative zero lost its sign.
/// </summary>
public class NumberClampConformanceTests
{
    /// <summary>Each width's refusal, caught and written as its message.</summary>
    private const string Refusals =
        "string Try(Func<string> f) { try { return f(); } catch (ArgumentException e) { return e.Message; } } "
        + "return Try(() => Math.Clamp(2.0, 3.5, 1.25).ToString()) + \"|\" + Try(() => Math.Clamp(2.0f, 3.5f, 1.25f).ToString()) + \"|\" "
        + "+ Try(() => Math.Clamp(0f, 0.1f, 0.05f).ToString()) + \"|\" + Try(() => Math.Clamp(2, 3, 1).ToString()) + \"|\" "
        + "+ Try(() => Math.Clamp(2L, 3000000000L, 1L).ToString()) + \"|\" + Try(() => Math.Clamp((byte)2, (byte)3, (byte)1).ToString()) + \"|\" "
        + "+ Try(() => Math.Clamp(2u, 3u, 1u).ToString()) + \"|\" + Try(() => Math.Clamp(2UL, 3UL, 1UL).ToString()) + \"|\" "
        + "+ Try(() => Math.Clamp((short)2, (short)3, (short)-1).ToString()) + \"|\" + Try(() => Math.Clamp((nint)2, (nint)3, (nint)1).ToString()) + \"|\" "
        + "+ Try(() => int.Clamp(2, 3, 1).ToString()) + \"|\" + Try(() => double.Clamp(2.0, 3.0, 1.0).ToString()) + \"|\" "
        + "+ Try(() => double.ClampNative(2.0, 3.0, 1.0).ToString()) + \"|\" + Try(() => long.Clamp(5L, 9L, -9L).ToString());";

    [SkippableTheory]
    [InlineData(null)]
    [InlineData("en-US")]
    [InlineData("pt-BR")]
    public void AMinimumAboveTheMaximum_IsRefusedInTheCulture(string? culture)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(Refusals, culture: culture);
    }

    [SkippableTheory]
    // NaN: as a value it stays NaN; as a bound it is passed over, as .NET compares in order.
    [InlineData("return Math.Clamp(double.NaN, 1.0, 3.0) + \"|\" + Math.Clamp(2.0, double.NaN, 1.0) + \"|\" + Math.Clamp(2.0, 1.0, double.NaN) + \"|\" + Math.Clamp(float.NaN, 1f, 3f) + \"|\" + Math.Clamp(2f, float.NaN, 1f);")]
    // A negative zero keeps its sign, and is no minimum above a positive zero.
    [InlineData("return Math.Clamp(-0.0, 0.0, 1.0) + \"|\" + Math.Clamp(0.0, -0.0, 1.0) + \"|\" + Math.Clamp(1.0, 0.0, -0.0) + \"|\" + Math.Clamp(-0f, 0f, 1f);")]
    // The ordinary answers, and equal bounds, for every width.
    [InlineData("return Math.Clamp(5, 1, 3) + \"|\" + Math.Clamp(-5, 1, 3) + \"|\" + Math.Clamp(2, 2, 2) + \"|\" + Math.Clamp(5L, 1L, 3L) + \"|\" + Math.Clamp(9007199254740993L, 0L, long.MaxValue) + \"|\" + Math.Clamp(200u, 1u, 100u) + \"|\" + Math.Clamp(ulong.MaxValue, 1UL, 100UL) + \"|\" + Math.Clamp(2.5, 1.0, 3.0) + \"|\" + Math.Clamp(0.1f, 0f, 1f);")]
    // The arguments are evaluated once each, in order.
    [InlineData("var log = \"\"; int Next(int v) { log += v + \",\"; return v; } var c = Math.Clamp(Next(5), Next(1), Next(3)); return c + \"|\" + log;")]
    public void AValue_IsClampedInDotNetsOrder(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
