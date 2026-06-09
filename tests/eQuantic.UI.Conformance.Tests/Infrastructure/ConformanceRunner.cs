using FluentAssertions;

namespace eQuantic.UI.Conformance.Tests.Infrastructure;

/// <summary>
/// The heart of the conformance harness: evaluates a C# expression two ways — by transpiling it
/// to JS and running it under embedded Bun, and by evaluating it directly in .NET — and asserts
/// the JSON results are identical. Divergence means the transpiler miscompiled the construct.
/// </summary>
public static class ConformanceRunner
{
    public static void AssertSameAsDotNet(string csharpExpression)
    {
        var js = Transpiler.TranspileExpression(csharpExpression);
        var program = $"console.log(JSON.stringify({js}))";

        var actual = JsExecutor.Run(program);
        var expected = DotNetEvaluator.EvaluateToJson(csharpExpression);

        actual.Should().Be(
            expected,
            $"C# `{csharpExpression}` (transpiled to JS `{js}`) must behave identically to .NET");
    }
}
