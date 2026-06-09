using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for <c>checked</c>/<c>unchecked</c> integer overflow. JS numbers are float64 and don't
/// wrap, so without this <c>int.MaxValue + 1</c> silently produces the wrong value. <c>unchecked</c>
/// wraps to 32 bits; <c>checked</c> throws <c>OverflowException</c>.
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
}
