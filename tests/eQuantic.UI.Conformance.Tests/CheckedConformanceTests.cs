using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for integer overflow. JS numbers are float64 and don't wrap, and a BigInt never does,
/// so without this <c>int.MaxValue + 1</c> silently produces the wrong value. The default context and
/// <c>unchecked</c> wrap to the type's width; <c>checked</c> throws <c>OverflowException</c>.
/// </summary>
public class CheckedConformanceTests
{
    [SkippableTheory]
    // unchecked: wrap to int32
    [InlineData("unchecked(int.MaxValue + 1)")]       // -2147483648
    [InlineData("unchecked(int.MinValue - 1)")]       // 2147483647
    [InlineData("unchecked(int.MaxValue * 2)")]       // -2
    [InlineData("unchecked(2000000000 + 2000000000)")] // -294967296
    // unchecked on a non-overflowing expression is a no-op value-wise
    [InlineData("unchecked(1000 + 1)")]               // 1001
    // checked on a non-overflowing expression returns the value
    [InlineData("checked(1000 + 1)")]                 // 1001
    [InlineData("checked(2147483646 + 1)")]           // 2147483647 (right at the edge, no overflow)
    public void Checked_MatchesDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }

    [SkippableTheory]
    // checked overflow throws — observed identically on both sides via a try/catch marker.
    // (Operands are variables: `checked(int.MaxValue + 1)` on constants is a C# compile-time error,
    // so a runtime overflow needs a non-constant operand.)
    [InlineData("int m = int.MaxValue; try { var x = checked(m + 1); return \"ok:\" + x; } catch (OverflowException) { return \"overflow\"; }")] // "overflow"
    [InlineData("int n = int.MinValue; try { var x = checked(n - 1); return \"ok:\" + x; } catch (OverflowException) { return \"overflow\"; }")] // "overflow"
    [InlineData("int a = 100; try { var x = checked(a + 1); return \"ok:\" + x; } catch (OverflowException) { return \"overflow\"; }")]           // "ok:101"
    public void CheckedOverflow_ThrowsLikeDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>
    /// C#'s DEFAULT context is unchecked, so an int and a long wrap there with no keyword written, and
    /// a rule ported from the server leans on it (a hash written without <c>unchecked</c>). The browser
    /// kept counting in a double, or in a BigInt, until #1807. Operands are variables: an overflowing
    /// constant is a compile error outside <c>unchecked</c>.
    /// </summary>
    [SkippableTheory]
    [InlineData("int m = int.MaxValue; return m + 1;")]                                     // -2147483648
    [InlineData("int n = int.MinValue; return n - 1;")]                                     // 2147483647
    [InlineData("int a = 65536; return a * a;")]                                            // 0
    [InlineData("int a = 2000000000; int b = 2000000003; return a * b;")]                   // low bits, past 2^53
    [InlineData("int m = int.MinValue; return -m;")]                                        // -2147483648
    [InlineData("int i = int.MaxValue; i++; return i;")]                                    // -2147483648
    [InlineData("int i = int.MinValue; --i; return i;")]                                    // 2147483647
    [InlineData("int i = int.MaxValue; var old = i++; return old + \"|\" + i;")]           // postfix answers the old value
    [InlineData("int c = int.MaxValue; Action step = () => c++; step(); return c;")]        // a void lambda's step
    [InlineData("int c = int.MaxValue; Func<int> step = () => c++; var r = step(); return r + \"|\" + c;")] // answers the old value
    [InlineData("int i = int.MaxValue; i += 2; return i;")]                                 // -2147483647
    [InlineData("int i = 3; i *= int.MaxValue; return i;")]                                 // 2147483645
    [InlineData("int h = 17; foreach (var c in \"eQuantic\") h = h * 31 + c; return h;")] // a hash with no unchecked
    [InlineData("int m = int.MaxValue; return m + 1 > m;")]                                 // false
    [InlineData("int? q = int.MaxValue; return q + 1;")]                                    // lifted: -2147483648
    [InlineData("long l = long.MaxValue; return (l + 1).ToString();")]                      // "-9223372036854775808"
    [InlineData("long l = long.MinValue; return (l - 1).ToString();")]                      // "9223372036854775807"
    [InlineData("long l = 3037000500L; return (l * l).ToString();")]                        // past 2^63
    [InlineData("long l = long.MinValue; return (-l).ToString();")]                         // "-9223372036854775808"
    [InlineData("long l = long.MaxValue; l++; return l.ToString();")]                       // "-9223372036854775808"
    [InlineData("long l = long.MaxValue; l += 2; return l.ToString();")]                    // "-9223372036854775807"
    [InlineData("ulong u = ulong.MaxValue; return (u + 1).ToString();")]                    // "0"
    [InlineData("ulong u = 0; u--; return u.ToString();")]                                  // "18446744073709551615"
    [InlineData("uint u = 5; return (-u).ToString();")]                                     // "-5": a uint negates as a long
    [InlineData("return -5;")]                                                              // a constant stays itself
    [InlineData("return (1u << 31).ToString();")]                                           // "2147483648": a uint's shift, folded
    [InlineData("const uint k = 3u; return (k << 30).ToString();")]                         // "3221225472"
    [InlineData("const int k = 1 << 30; return k - 1 + k;")]                                // 2147483647, folded in range
    public void DefaultContextOverflow_WrapsLikeDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
