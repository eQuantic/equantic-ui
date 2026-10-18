using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A method group inside an expression a strategy rebuilds, a <c>?.</c> chain or a query, is the delegate
/// its call is, as one written anywhere else (Copilot's second review of #958). The call it stands for was
/// bound where the REBUILT node stands, which no model knows, so the group was refused (EQ2004).
/// </summary>
public class MethodGroupInARebuiltExpressionConformanceTests
{
    [SkippableTheory]
    [InlineData("int[] values = null; var a = values?.Select(Math.Abs).Sum(); values = new[] { -1, 2, -3 }; "
        + "var b = values?.Select(Math.Abs).Sum(); return (a is null) + \"|\" + b;")]
    [InlineData("var strings = new[] { \"a\", \"b\" }; var made = (from s in strings select new Func<string>(s.ToString)).ToList(); "
        + "return string.Join(\"|\", made.Select(f => f()));")]
    [InlineData("var prefix = \"ab\"; string[] items = null; var a = items?.Select(prefix.StartsWith).ToList(); "
        + "items = new[] { \"a\", \"abc\", \"x\" }; var b = items?.Select(prefix.StartsWith).ToList(); "
        + "return (a is null) + \"|\" + string.Join(\",\", b);")]
    public void AGroupInsideARebuiltExpression_IsTheDelegateItsCallIs(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
