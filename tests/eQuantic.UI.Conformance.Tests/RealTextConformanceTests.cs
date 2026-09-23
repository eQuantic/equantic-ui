using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A floating-point number's text, on both sides (#336). .NET writes the shortest digits that read
/// back as the value, in fixed notation from a decimal exponent of -4 up to 16 for a double and 8
/// for a float, and scientific outside, as <c>1E+17</c>; JavaScript's <c>String()</c> keeps fixed
/// notation up to 1e21, spells <c>1e+21</c>, drops the sign of <c>-0</c>, and gives a float the
/// digits of the double underneath. The same text reaches a string three ways, and each is here:
/// <c>ToString()</c>, concatenation and interpolation.
/// </summary>
public class RealTextConformanceTests
{
    [SkippableTheory]
    // ---- a double, at each boundary of its notation ----
    [InlineData("double d = 1e16; return d.ToString();")]                  // "10000000000000000"
    [InlineData("double d = 9.9e16; return d.ToString();")]                // "99000000000000000"
    [InlineData("double d = 1e17; return d.ToString();")]                  // "1E+17"
    [InlineData("double d = -1e17; return d.ToString();")]                 // "-1E+17"
    [InlineData("double d = 4611686018427387904.0; return d.ToString();")] // "4.611686018427388E+18"
    [InlineData("double d = 1e21; return d.ToString();")]                  // "1E+21"
    [InlineData("double d = 1.5e300; return d.ToString();")]               // "1.5E+300"
    [InlineData("double d = 0.0001; return d.ToString();")]                // "0.0001"
    [InlineData("double d = 1e-5; return d.ToString();")]                  // "1E-05"
    [InlineData("double d = 0.000012345; return d.ToString();")]           // "1.2345E-05"
    [InlineData("return double.MaxValue.ToString();")]                     // "1.7976931348623157E+308"
    [InlineData("return double.Epsilon.ToString();")]                      // "5E-324"
    [InlineData("double d = -0.0; return d.ToString();")]                  // "-0"
    [InlineData("return double.NaN.ToString();")]                          // "NaN"
    [InlineData("return double.PositiveInfinity.ToString();")]             // "Infinity"
    [InlineData("return double.NegativeInfinity.ToString();")]             // "-Infinity"
    // ---- the same text on its way into a string ----
    [InlineData("double d = 1e17; return \"v=\" + d;")]                    // "v=1E+17"
    [InlineData("double d = 1e-5; return $\"v={d}\";")]                    // "v=1E-05"
    [InlineData("double d = -0.0; return $\"v={d}\";")]                    // "v=-0"
    [InlineData("double? d = 1e17; return $\"v={d}\";")]                   // "v=1E+17"
    [InlineData("double? d = null; return $\"v={d}\";")]                   // "v="
    // ---- a float: its own digits, and its own boundary ----
    [InlineData("float f = 0.1f; return f.ToString();")]                   // "0.1"
    [InlineData("float f = 0.1f; return \"v=\" + f;")]                     // "v=0.1"
    [InlineData("float f = 0.1f; return $\"v={f}\";")]                     // "v=0.1"
    [InlineData("float? f = 0.1f; return $\"v={f}\";")]                    // "v=0.1"
    [InlineData("float f = 9.9e8f; return f.ToString();")]                 // "990000000"
    [InlineData("float f = 1e9f; return f.ToString();")]                   // "1E+09"
    [InlineData("float f = 1e-5f; return $\"{f}\";")]                      // "1E-05"
    [InlineData("return float.MaxValue.ToString();")]                      // "3.4028235E+38"
    [InlineData("return float.Epsilon.ToString();")]                       // "1E-45"
    [InlineData("float f = -0f; return f.ToString();")]                    // "-0"
    [InlineData("return float.PositiveInfinity.ToString();")]              // "Infinity"
    public void AFractionalNumber_ReadsAsDotNetWritesIt(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
