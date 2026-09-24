using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for .NET `long`/`ulong` (Int64) — exact 64-bit via the BigInt-backed `long` compat
/// helper. Naive JS numbers lose precision beyond 2^53; BigInt matches .NET. Arithmetic results are
/// compared via ToString() (a BigInt can't be JSON-serialized as a number); comparisons return bool.
/// </summary>
public class LongConformanceTests
{
    [SkippableTheory]
    [InlineData("(5L + 3L).ToString()")]                              // "8"
    [InlineData("(2L * 21L).ToString()")]                             // "42"
    [InlineData("(10L / 3L).ToString()")]                             // "3" (truncating division)
    [InlineData("(10L % 3L).ToString()")]                             // "1"
    [InlineData("(1L << 10).ToString()")]                             // "1024"
    [InlineData("(9007199254740993L + 1L).ToString()")]              // "9007199254740994" (> 2^53, exact)
    [InlineData("(long.MaxValue).ToString()")]                        // "9223372036854775807"
    [InlineData("(long.MaxValue - 1L).ToString()")]                   // "9223372036854775806"
    // Comparisons (bool)
    [InlineData("5L > 3L")]                                           // true
    [InlineData("5L == 5L")]                                          // true
    [InlineData("9007199254740993L > 9007199254740992L")]            // true — equal as doubles!
    public void Long_MatchesDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }

    /// <summary>A literal is a long by its TYPE, which it has with no suffix when int and uint
    /// cannot hold it; as a plain number it lost its low digits and threw at the first arithmetic
    /// with another long.</summary>
    [SkippableTheory]
    [InlineData("long l = 9007199254740993; return l.ToString();")]                                         // "9007199254740993"
    [InlineData("long l = 9007199254740993; return (l + 1L).ToString();")]                                  // "9007199254740994"
    [InlineData("var t = 637000000000000000; return (t / 10).ToString();")]                                 // a var is a long too
    [InlineData("ulong u = 18446744073709551615; return u.ToString();")]
    [InlineData("var h = 0x1_0000_0000; return (h + 1).ToString();")]                                       // "4294967297": hex, separators
    [InlineData("return (-9223372036854775808).ToString();")]                                               // long.MinValue, written out
    [InlineData("long? l = 9007199254740993; return l.ToString();")]                                        // into a nullable long
    [InlineData("long big = 3000000000; return (big * 2).ToString();")]                                     // "6000000000": a uint literal, widened (control)
    public void ALongLiteral_WithNoSuffix_IsALong(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
