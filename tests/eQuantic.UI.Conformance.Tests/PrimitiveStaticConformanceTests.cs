using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The .NET 7+ static surface of the primitives themselves (<c>double.IsNaN</c>,
/// <c>int.Clamp</c>, <c>char.IsAsciiLetter</c>…), lowered symbol-first by
/// PrimitiveStaticStrategy. These execute on both sides: the table only admits faithful
/// single-evaluation translations, and this is the proof.
/// </summary>
public class PrimitiveStaticConformanceTests
{
    [SkippableTheory]
    // double / float classification and math
    [InlineData("double.IsNaN(Math.Sqrt(-1.0))")]                    // true
    [InlineData("double.IsNaN(1.5)")]                                // false
    [InlineData("double.IsInfinity(1.0 / 0.0)")]                     // true
    [InlineData("double.IsInfinity(-1.0 / 0.0)")]                    // true
    [InlineData("double.IsPositiveInfinity(-1.0 / 0.0)")]            // false
    [InlineData("double.IsNegativeInfinity(-1.0 / 0.0)")]            // true
    [InlineData("double.IsFinite(2.5)")]                             // true
    [InlineData("double.IsInteger(4.0)")]                            // true
    [InlineData("double.IsInteger(4.5)")]                            // false
    [InlineData("double.Abs(-2.5)")]                                 // 2.5
    [InlineData("double.Clamp(7.5, 0.0, 5.0)")]                      // 5
    [InlineData("double.Max(2.0, 3.5)")]                             // 3.5
    [InlineData("double.Min(2.0, 3.5)")]                             // 2
    [InlineData("double.Sqrt(9.0)")]                                 // 3
    [InlineData("double.Floor(2.7)")]                                // 2
    [InlineData("double.Ceiling(2.1)")]                              // 3
    [InlineData("double.Truncate(-2.7)")]                            // -2
    [InlineData("double.Round(2.5)")]                                // 2 — banker's, NOT Math.round's 3
    [InlineData("double.Round(3.5)")]                                // 4
    [InlineData("double.Log2(8.0)")]                                 // 3
    [InlineData("double.Pow(2.0, 10.0)")]                            // 1024
    [InlineData("double.DegreesToRadians(180.0)")]                   // π
    [InlineData("double.RadiansToDegrees(Math.PI)")]                 // 180
    [InlineData("double.Sign(-3.5)")]                                // -1
    // int family
    [InlineData("int.Abs(-7)")]                                      // 7
    [InlineData("int.Clamp(15, 0, 10)")]                             // 10
    [InlineData("int.Max(3, 9)")]                                    // 9
    [InlineData("int.Min(3, 9)")]                                    // 3
    [InlineData("int.Sign(-4)")]                                     // -1
    [InlineData("int.IsPositive(0)")]                                // true — int has no -0
    [InlineData("int.IsNegative(-1)")]                                // true
    [InlineData("int.IsEvenInteger(4)")]                             // true
    [InlineData("int.IsOddInteger(-3)")]                             // true — negative odds count
    [InlineData("int.MaxValue")]                                     // 2147483647
    [InlineData("int.MinValue")]                                     // -2147483648
    [InlineData("short.MaxValue")]                                   // 32767
    [InlineData("byte.MaxValue")]                                    // 255
    // char ASCII family and surrogates
    [InlineData("char.IsAsciiLetter('x')")]                          // true
    [InlineData("char.IsAsciiLetter('1')")]                          // false
    [InlineData("char.IsAsciiDigit('7')")]                           // true
    [InlineData("char.IsAsciiLetterOrDigit('_')")]                   // false
    [InlineData("char.IsAsciiHexDigit('F')")]                        // true
    [InlineData("char.IsAsciiHexDigitLower('F')")]                   // false
    [InlineData("char.IsAsciiLetterUpper('F')")]                     // true
    [InlineData("char.ToString('a')")]                               // "a"
    // bool
    [InlineData("bool.TrueString")]                                  // "True"
    [InlineData("bool.FalseString")]                                 // "False"
    public void PrimitiveStatics_MatchDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }
}
