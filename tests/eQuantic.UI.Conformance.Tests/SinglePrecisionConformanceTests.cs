using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The SINGLE-precision contract (#146): a C# <c>float</c> is a single wherever it is PRODUCED —
/// every arithmetic operation, increment, conversion, float answer of the BCL and float constant —
/// because RyuJIT rounds every float operation and JavaScript rounds none. The rule this replaced
/// rounded only at a store, and a float-returning method handed its caller an unrounded double.
/// <para>
/// Every case returns the float WIDENED to a double, so the comparison is exact: a float returned
/// as itself prints as the shortest single on .NET and as the shortest double here — two texts for
/// one value. The expected values are measured, never restated: each line runs on both sides.
/// </para>
/// </summary>
public class SinglePrecisionConformanceTests
{
    [SkippableTheory]
    // ---- every operation rounds, not only the store ----
    // The bar chart's edge (#146): three roundings in C#, one under the store-only rule.
    [InlineData("float across = 199f, hi = 17f / 30f, lo = 5f / 30f; return (double)(hi * across - lo * across);")]
    [InlineData("float x = 1f / 3f; return (double)(x * 3f - 1f);")]                          // 0, not 2.98e-8
    // A Decimal reaches a float THROUGH a double in .NET as well (`(float)d == (float)(double)d`,
    // measured): 2^62 + 2^38 + 1 is just above a midpoint between two singles, the double lands ON
    // it, and both sides take the even one. The answer names the neighbour, because a double this
    // large prints differently in the two JSON writers.
    [InlineData("decimal d = 4611686293305294849m; float f = (float)d; return f == 4611686018427387904f ? \"even\" : \"other\";")] // "even"
    [InlineData("float a = 10f, b = 3f; return (double)(a / b / b);")]
    [InlineData("float a = 0.1f; bool pick = true; return (double)(pick ? a * 3f : a);")]    // a branch is a producer too
    [InlineData("float a = 0.1f, b = 0.2f; return a + b == 0.3f;")]                          // true — singles compare
    [InlineData("return float.Epsilon / 2 == 0;")]                                            // true — the half underflows
    // ---- the return seam, in both shapes of body: the arrow form reached the emitter by a
    // different path, which is how the same defect came back twice before (ef67aec, #98) ----
    [InlineData("float Offset(double v, float across) => (float)(v / 30.0) * across; return (double)(Offset(17, 199f) - Offset(5, 199f));")]
    [InlineData("float Offset(double v, float across) { return (float)(v / 30.0) * across; } return (double)(Offset(17, 199f) - Offset(5, 199f));")]
    // ---- an integer too wide for 24 bits of significand rounds on its way into a float ----
    [InlineData("int n = 16777217; float t = 1.5f; return (double)(n * t);")]                 // 25165824, not 25165826
    [InlineData("int n = 16777217; float f = n; return (double)f;")]                          // 16777216
    [InlineData("uint u = 4294967295; float f = u; return (double)f;")]                       // 4294967296
    [InlineData("long l = 16777217; float f = l; return (double)f;")]                         // 16777216
    // A long just above a midpoint between two singles: rounding through the double lands ON the
    // midpoint and then on the even single; .NET rounds once and goes up.
    [InlineData("long l = 4611686293305294849L; float f = l; return ((long)(double)f).ToString();")]
    [InlineData("long l = 4611686293305294849L; return ((long)(double)(float)l).ToString();")]
    [InlineData("ulong u = 18446744073709551615UL; float f = u; return (double)f == 18446744073709551616.0;")]
    [InlineData("float f = 16777217; return (double)f;")]                                     // a constant settles at compile time
    [InlineData("short s = 12345; float t = 0.1f; return (double)(s * t);")]                 // a narrow width holds exactly
    // ---- increments ----
    [InlineData("float t = 0.1f; t++; return (double)t;")]                                    // 1.100000023841858
    [InlineData("float t = 0.3f; --t; return (double)t;")]
    [InlineData("float t = 0.1f; float old = t++; return (double)old * 1000 + (double)t;")]  // postfix answers the value before
    [InlineData("float s = 0; for (int i = 0; i < 10; i++) s += 0.1f; return (double)s;")]
    // ---- LINQ: .NET accumulates a float sequence in a DOUBLE and rounds once at the end ----
    [InlineData("var xs = new[] { 0.1f, 0.2f, 0.3f, 0.4f }; return (double)xs.Sum();")]      // 1
    [InlineData("var xs = new[] { 0.1f, 0.2f, 0.7f }; return (double)xs.Average();")]
    [InlineData("var xs = new[] { 1, 2, 3 }; return (double)xs.Sum(x => x * 0.1f);")]
    // ---- a float's constants are singles ----
    [InlineData("return (double)float.Pi;")]                                                  // 3.1415927410125732
    [InlineData("return (double)MathF.PI;")]
    [InlineData("return (double)float.E;")]
    [InlineData("return (double)MathF.E;")]
    [InlineData("return (double)float.Tau;")]
    [InlineData("return float.Epsilon > 0 && float.Epsilon / 2 == 0;")]
    public void AFloatIsASingleWhereverItIsProduced(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
