using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>Conformance: expression-level control flow (ternary, null-coalescing, switch, patterns).</summary>
public class ControlFlowConformanceTests
{
    [SkippableTheory]
    [InlineData("5 > 3 ? \"a\" : \"b\"")]                          // ternary
    [InlineData("(5 > 3 ? 1 : 2) + 10")]                          // ternary precedence
    [InlineData("((int?)null) ?? 7")]                             // null-coalescing
    [InlineData("((string?)null) ?? \"fallback\"")]
    [InlineData("3 switch { 1 => \"a\", 3 => \"c\", _ => \"z\" }")] // switch expression
    [InlineData("9 switch { 1 => \"a\", _ => \"z\" }")]            // switch default arm
    [InlineData("5 switch { var v => v + 1 }")]                    // regression #14: var-pattern binds
    [InlineData("5 switch { var v when v > 3 => \"big\", _ => \"small\" }")] // when-clause sees binding
    [InlineData("2 switch { var v when v > 3 => \"big\", _ => \"small\" }")]
    public void ControlFlow_MatchesDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }
}
