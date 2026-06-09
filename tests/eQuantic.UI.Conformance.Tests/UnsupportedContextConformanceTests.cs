using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for context-only statements that JS doesn't model but that are faithful no-ops once the
/// body runs: <c>lock</c> (JS is single-threaded) and <c>unsafe</c> (a context marker). The body must
/// execute and produce the same value as .NET. (<c>goto</c> is a build error, covered by compiler tests.)
/// </summary>
public class UnsupportedContextConformanceTests
{
    [SkippableTheory]
    // lock unwraps to its body — the lock object is ignored (single-threaded).
    [InlineData("var s = 0; lock (\"gate\") { s = 1 + 2; } return s;")]   // 3
    // unsafe is a context marker — non-pointer body runs normally.
    [InlineData("int n = 5; unsafe { n = n * 2; } return n;")]            // 10
    // labeled statement (no goto) just runs the inner statement.
    [InlineData("int x = 0; done: x = 7; return x;")]                     // 7
    public void ContextStatements_MatchDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
