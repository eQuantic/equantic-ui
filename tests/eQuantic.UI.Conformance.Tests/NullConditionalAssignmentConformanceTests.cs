using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// C# 14 null-conditional assignment (<c>a?[i] = v</c>, <c>a?.B = v</c>): assign only when the
/// receiver is non-null, and evaluate the RIGHT side only behind the guard. JavaScript rejects
/// <c>?.</c> on an assignment target outright, so the compiler lowers to a single-evaluation
/// guarded arrow — these cases execute both sides to prove the semantics survived the lowering.
/// </summary>
public class NullConditionalAssignmentConformanceTests
{
    [SkippableTheory]
    // Non-null receiver: the assignment lands.
    [InlineData("int[]? a = new int[3]; a?[1] = 7; return a![1];")]                       // 7
    // Null receiver: nothing happens, nothing throws.
    [InlineData("int[]? a = null; a?[1] = 7; return a is null ? -1 : a[1];")]             // -1
    // The RIGHT side must not run for a null receiver…
    [InlineData("var calls = 0; int Next() { calls++; return 9; } int[]? a = null; a?[0] = Next(); return calls;")]   // 0
    // …and must run exactly once for a non-null one.
    [InlineData("var calls = 0; int Next() { calls++; return 9; } int[]? a = new int[1]; a?[0] = Next(); return calls + a![0];")] // 10
    // Compound form.
    [InlineData("int[]? a = new[] { 5 }; a?[0] += 2; return a![0];")]                     // 7
    public void NullConditionalAssignment_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
