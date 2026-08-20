using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for <c>x as T</c>: the value when it IS a <c>T</c>, <c>null</c> when it is not.
/// This was a PASSTHROUGH (<c>x as T</c> emitted plain <c>x</c>), so
/// <c>if (x as Foo != null)</c> took the branch for any non-null <c>x</c> — the compiler now
/// emits the same type test patterns use, and these cases execute both sides to prove it.
/// </summary>
public class AsOperatorConformanceTests
{
    [SkippableTheory]
    // Match: the value survives.
    [InlineData("(((object)\"hello\") as string)")]              // "hello"
    [InlineData("((((object)\"x\") as string) ?? \"fb\")")]      // "x"
    // Mismatch: null — and ?? takes over, exactly the C# idiom the passthrough broke.
    [InlineData("((((object)5) as string) ?? \"fb\")")]          // "fb"
    [InlineData("((((object)5) as string) == null)")]            // true
    [InlineData("((((object)null) as string) == null)")]         // true
    // Nullable value targets test the underlying type.
    [InlineData("(((object)3) as int?) ?? -1")]                  // 3
    [InlineData("((((object)\"x\") as int?) ?? -1)")]            // -1
    public void AsOperator_MatchesDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }

    [SkippableTheory]
    // The guard idiom that motivated the fix: mismatch must NOT take the branch.
    [InlineData("object o = 5; var s = o as string; return s != null ? s.Length : -1;")]   // -1
    [InlineData("object o = \"abc\"; var s = o as string; return s != null ? s.Length : -1;")] // 3
    public void AsOperatorStatements_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    [SkippableTheory]
    // TryParse with `out var`: the declaration lives INSIDE the call. Executed on both sides to
    // pin the emitted assignment shape (a bare assignment in a strict-mode module would throw).
    [InlineData("var ok = int.TryParse(\"42\", out var n); return n + (ok ? 100 : 0);")]  // 142
    [InlineData("var ok = int.TryParse(\"abc\", out var n); return ok ? n : -1;")]        // -1
    public void TryParseOutVar_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
