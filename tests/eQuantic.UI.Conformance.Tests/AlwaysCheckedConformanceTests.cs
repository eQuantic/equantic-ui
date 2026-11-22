using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The .NET members that check overflow whatever the context they are called in, run on both sides (#1808).
/// <c>Enumerable.Sum</c> over <c>int</c> and <c>long</c> adds in a checked context, and <c>Math.Abs</c> and
/// <c>CopySign</c> of a signed integer's <c>MinValue</c> throw, since its magnitude is one past the type's largest
/// value. They never wrap, inside <c>unchecked</c> or out of it. The browser kept counting: a sum answered
/// 2147483648 for an int, and an absolute value 9223372036854775808 for a long, each a value outside its type.
/// </summary>
public class AlwaysCheckedConformanceTests
{
    /// <summary>The expression's value, or the overflow it threw with its message, on either side.</summary>
    private static string Outcome(string expression, string setup = "") =>
        setup + " try { return \"ok:\" + (" + expression + "); } catch (OverflowException e) { return \"overflow:\" + e.Message; }";

    [SkippableTheory]
    [InlineData("int[] a = { int.MaxValue, 1 };", "a.Sum()")]                          // overflow
    [InlineData("int[] a = { int.MaxValue, 1, -1 };", "a.Sum()")]                      // overflow: the running total, not the last
    [InlineData("int[] a = { int.MinValue, -1 };", "a.Sum()")]                         // overflow, below
    [InlineData("int[] a = { int.MaxValue - 1, 1 };", "a.Sum()")]                      // ok:2147483647, at the edge
    [InlineData("int[] a = { 1, 2, 3 };", "a.Sum()")]                                  // ok:6
    [InlineData("int[] a = { };", "a.Sum()")]                                          // ok:0
    [InlineData("var a = new List<int> { int.MaxValue, 1 };", "a.Sum()")]              // overflow
    [InlineData("int[] a = { int.MaxValue, 1 };", "a.Select(x => x).Sum()")]           // overflow
    [InlineData("int[] a = { int.MaxValue, 1 };", "unchecked(a.Sum())")]               // overflow: unchecked is the caller's, not Sum's
    public void SumOfInts_ChecksEachAddition(string setup, string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(Outcome(expression, setup));
    }

    [SkippableTheory]
    [InlineData("long[] a = { long.MaxValue, 1L };", "a.Sum()")]                       // overflow
    [InlineData("long[] a = { long.MinValue, -1L };", "a.Sum()")]                      // overflow, below
    [InlineData("long[] a = { long.MaxValue, 1L, -1L };", "a.Sum()")]                  // overflow
    [InlineData("long[] a = { long.MaxValue - 1, 1L };", "a.Sum()")]                   // ok:9223372036854775807
    [InlineData("long[] a = { };", "a.Sum()")]                                         // ok:0
    [InlineData("var a = new List<long> { long.MaxValue, 1L };", "a.Sum()")]           // overflow
    public void SumOfLongs_ChecksEachAddition(string setup, string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(Outcome(expression, setup));
    }

    /// <summary>A selector adds what it answers, in its own type: a lambda, a method group, a delegate, and an int
    /// widened to a long, whose total of three int.MaxValues fits.</summary>
    [SkippableTheory]
    [InlineData("int[] a = { int.MaxValue, 1 };", "a.Sum(x => x)")]                                         // overflow
    [InlineData("int[] a = { 1, 2, 3 };", "a.Sum(x => x * 2)")]                                             // ok:12
    [InlineData("int[] a = { int.MaxValue, 1 }; Func<int, int> f = x => x;", "a.Sum(f)")]                   // overflow
    [InlineData("int[] a = { int.MaxValue, 1 };", "a.Sum(x => { return x; })")]                             // overflow
    [InlineData("int[] a = { int.MaxValue, int.MaxValue, int.MaxValue };", "a.Sum(x => (long)x)")]          // ok:6442450941
    [InlineData("long[] a = { long.MaxValue, 1L };", "a.Sum(x => x)")]                                      // overflow
    [InlineData("string[] a = { \"aa\", \"b\" };", "a.Sum(s => s.Length)")]                                 // ok:3
    [InlineData("string[] a = { \"aa\", \"b\" }; Func<string, long> f = s => long.MaxValue;", "a.Sum(f)")]  // overflow
    public void SumWithASelector_ChecksEachAddition(string setup, string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(Outcome(expression, setup));
    }

    /// <summary>A nullable sum passes a null over and checks what it adds; one with no value is 0, never null.</summary>
    [SkippableTheory]
    [InlineData("int?[] a = { int.MaxValue, null, 1 };", "a.Sum()")]                   // overflow
    [InlineData("int?[] a = { 1, null, 2 };", "a.Sum()")]                              // ok:3
    [InlineData("int?[] a = { null };", "a.Sum()")]                                    // ok:0
    [InlineData("long?[] a = { 1L, null, 2L };", "a.Sum()")]                           // ok:3
    [InlineData("long?[] a = { long.MaxValue, null, 1L };", "a.Sum()")]                // overflow
    [InlineData("long?[] a = { null, null };", "a.Sum()")]                             // ok:0
    [InlineData("int[] a = { int.MaxValue, 1 };", "a.Sum(x => (int?)x)")]              // overflow
    [InlineData("int[] a = { 1, 2, 3 };", "a.Sum(x => x == 2 ? null : (long?)x)")]     // ok:4
    [InlineData("decimal?[] a = { 1.5m, null, 2m };", "a.Sum()")]                      // ok:3.5
    [InlineData("double?[] a = { 1.5, null, 2 };", "a.Sum()")]                         // ok:3.5
    [InlineData("float?[] a = { 1.5f, null, 2f };", "a.Sum()")]                        // ok:3.5
    public void SumOfNullables_PassesANullOver(string setup, string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(Outcome(expression, setup));
    }

    /// <summary>A sum of reals has nothing to check: a double past its range is infinity, and a decimal throws on
    /// its own.</summary>
    [SkippableTheory]
    [InlineData("double[] a = { double.MaxValue, double.MaxValue };", "a.Sum()")]      // ok:∞
    [InlineData("double[] a = { 0.1, 0.2 };", "a.Sum()")]                              // ok:0.30000000000000004
    [InlineData("float[] a = { 0.1f, 0.2f };", "a.Sum()")]                             // ok:0.3
    [InlineData("decimal[] a = { 0.1m, 0.2m };", "a.Sum()")]                           // ok:0.3
    [InlineData("decimal[] a = { decimal.MaxValue, 1m };", "a.Sum()")]                 // overflow
    public void SumOfReals_IsUnchanged(string setup, string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(Outcome(expression, setup));
    }

    /// <summary>A long's mean adds its values in a checked long; an int's in a long, where two int.MaxValues fit.</summary>
    [SkippableTheory]
    [InlineData("long[] a = { long.MaxValue, 1L };", "a.Average()")]                   // overflow
    [InlineData("long?[] a = { long.MaxValue, 1L };", "a.Average()")]                  // overflow
    [InlineData("long[] a = { long.MaxValue, long.MaxValue };", "a.Average(x => x)")]  // overflow
    [InlineData("int[] a = { int.MaxValue, int.MaxValue };", "a.Average()")]           // ok:2147483647
    public void AverageOfLongs_ChecksItsTotal(string setup, string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(Outcome(expression, setup));
    }

    /// <summary>The magnitude of a signed integer's MinValue is no value of its type, so .NET throws, through
    /// <c>Math</c> and through the type alike, and in an unchecked context too.</summary>
    [SkippableTheory]
    [InlineData("int n = int.MinValue;", "Math.Abs(n)")]                               // overflow
    [InlineData("int n = int.MinValue;", "int.Abs(n)")]                                // overflow
    [InlineData("int n = int.MinValue;", "unchecked(Math.Abs(n))")]                    // overflow
    [InlineData("int n = int.MinValue + 1;", "Math.Abs(n)")]                           // ok:2147483647
    [InlineData("int n = -5;", "Math.Abs(n)")]                                         // ok:5
    [InlineData("long n = long.MinValue;", "Math.Abs(n)")]                             // overflow
    [InlineData("long n = long.MinValue;", "long.Abs(n)")]                             // overflow
    [InlineData("long n = long.MinValue + 1;", "Math.Abs(n)")]                         // ok:9223372036854775807
    [InlineData("long n = -5L;", "long.Abs(n)")]                                       // ok:5
    [InlineData("short n = short.MinValue;", "Math.Abs(n)")]                           // overflow
    [InlineData("short n = short.MinValue;", "short.Abs(n)")]                          // overflow
    [InlineData("short n = -5;", "short.Abs(n)")]                                      // ok:5
    [InlineData("sbyte n = sbyte.MinValue;", "Math.Abs(n)")]                           // overflow
    [InlineData("sbyte n = sbyte.MinValue;", "sbyte.Abs(n)")]                          // overflow
    [InlineData("nint n = nint.MinValue;", "Math.Abs(n)")]                             // overflow
    [InlineData("nint n = nint.MinValue;", "nint.Abs(n)")]                             // overflow
    [InlineData("nint n = -5;", "nint.Abs(n)")]                                        // ok:5
    [InlineData("double d = -0.0;", "Math.Abs(d)")]                                    // ok:0
    public void AbsOfMinValue_Throws(string setup, string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(Outcome(expression, setup));
    }

    /// <summary><c>CopySign</c> takes the magnitude first, so MinValue throws wherever the sign is not negative.</summary>
    [SkippableTheory]
    [InlineData("int n = int.MinValue;", "int.CopySign(n, 1)")]                        // overflow
    [InlineData("int n = int.MinValue;", "int.CopySign(n, 0)")]                        // overflow
    [InlineData("int n = int.MinValue;", "int.CopySign(n, -1)")]                       // ok:-2147483648
    [InlineData("int n = -5;", "int.CopySign(n, 1)")]                                  // ok:5
    [InlineData("int n = 5;", "int.CopySign(n, -1)")]                                  // ok:-5
    [InlineData("int n = 0;", "int.CopySign(n, -1) + \"|\" + (1.0 / int.CopySign(n, -1))")] // ok:0|∞, an integer's one zero
    [InlineData("short n = short.MinValue;", "short.CopySign(n, (short)1)")]           // overflow
    [InlineData("sbyte n = sbyte.MinValue;", "sbyte.CopySign(n, (sbyte)1)")]           // overflow
    [InlineData("long n = long.MinValue;", "long.CopySign(n, 1L)")]                    // overflow
    [InlineData("long n = long.MinValue;", "long.CopySign(n, 0L)")]                    // overflow
    [InlineData("long n = long.MinValue;", "long.CopySign(n, -1L)")]                   // ok:-9223372036854775808
    [InlineData("long n = -5L;", "long.CopySign(n, 1L)")]                              // ok:5
    [InlineData("nint n = nint.MinValue;", "nint.CopySign(n, 1)")]                     // overflow
    public void CopySignOfMinValue_ThrowsForASignThatIsNotNegative(string setup, string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(Outcome(expression, setup));
    }

    /// <summary>The magnitude comparisons compare MinValue's magnitude without writing it, so they never throw.</summary>
    [SkippableTheory]
    [InlineData("int n = int.MinValue;", "int.MaxMagnitude(n, 0)")]                    // ok:-2147483648
    [InlineData("int n = int.MinValue;", "int.MinMagnitude(n, int.MaxValue)")]         // ok:2147483647
    [InlineData("short n = short.MinValue;", "short.MaxMagnitude(n, short.MaxValue)")] // ok:-32768
    [InlineData("long n = long.MinValue;", "long.MaxMagnitude(n, 0L)")]                // ok:-9223372036854775808
    [InlineData("long n = long.MinValue;", "long.MinMagnitude(n, long.MaxValue)")]     // ok:9223372036854775807
    public void MagnitudeOfMinValue_NeverThrows(string setup, string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(Outcome(expression, setup));
    }
}
