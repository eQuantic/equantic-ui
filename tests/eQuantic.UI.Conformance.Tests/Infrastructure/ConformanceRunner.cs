using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace eQuantic.UI.Conformance.Tests.Infrastructure;

/// <summary>
/// The heart of the conformance harness: evaluates a C# expression two ways — by transpiling it
/// to JS and running it under embedded Bun (or Node fallback), and by evaluating it directly in
/// .NET — and asserts the JSON results are identical. Divergence means the transpiler miscompiled
/// the construct.
/// </summary>
public static class ConformanceRunner
{
    // Runtime helpers the transpiler may emit; imported from the REAL bundled runtime.js (not a
    // re-implementation) so format/enum/etc. behavior is validated against what actually ships.
    // All runtime helpers (incl. CSS) are now emitted under the `$eq` namespace (global in the browser;
    // imported once here for the standalone harness JS). No individual named helpers remain.
    private static readonly string[] RuntimeHelpers = System.Array.Empty<string>();

    public static void AssertSameAsDotNet(string csharpExpression) =>
        AssertSameAsDotNet(csharpExpression, prelude: "");

    /// <summary>
    /// As <see cref="AssertSameAsDotNet(string,string)"/> but for a block of C# <b>statements</b>
    /// (control flow). The block must <c>return</c> a value; the transpiled block is wrapped in an
    /// IIFE to capture it, and the .NET side runs the same block as a script (top-level return).
    /// </summary>
    public static void AssertStatementsSameAsDotNet(string csharpStatements, string prelude = "")
    {
        var jsBlock = Transpiler.TranspileStatements(csharpStatements, prelude);
        var types = Transpiler.EmitDeclaredRecordTypes(prelude);
        var program = $"{BuildHelperImport(jsBlock + types)}{types}console.log(JSON.stringify((() => {jsBlock})()))";

        var actual = JsExecutor.Run(program);
        var expected = DotNetEvaluator.EvaluateToJson(csharpStatements, prelude);

        actual.Should().Be(
            expected,
            $"C# block `{csharpStatements}` (transpiled to JS `{jsBlock}`) must behave identically to .NET");
    }

    /// <summary>
    /// As above, but with a C# <paramref name="prelude"/> of type declarations (e.g. an enum) made
    /// available to both the transpiler's semantic model and the .NET evaluator.
    /// </summary>
    public static void AssertSameAsDotNet(string csharpExpression, string prelude)
    {
        var js = Transpiler.TranspileExpression(csharpExpression, prelude);
        var types = Transpiler.EmitDeclaredRecordTypes(prelude);
        var program = $"{BuildHelperImport(js + types)}{types}console.log(JSON.stringify({js}))";

        var actual = JsExecutor.Run(program);
        var expected = DotNetEvaluator.EvaluateToJson(csharpExpression, prelude);

        actual.Should().Be(
            expected,
            $"C# `{csharpExpression}` (transpiled to JS `{js}`) must behave identically to .NET");
    }

    /// <summary>
    /// If the emitted JS references runtime helpers (e.g. `format`), import exactly those from the
    /// real bundled runtime.js. Helper-free output (the common case) gets no import at all.
    /// </summary>
    private static string BuildHelperImport(string js)
    {
        var used = RuntimeHelpers.Where(h => Regex.IsMatch(js, $@"\b{h}[(.]")).ToList();
        // The `$eq` namespace is a browser global; the standalone harness JS imports it explicitly.
        if (js.Contains("$eq.")) used.Insert(0, "$eq");
        if (used.Count == 0) return string.Empty;

        var runtimeUrl = RuntimeJsUrl()
            ?? throw new InvalidOperationException("Could not locate the bundled runtime.js for helper import.");
        return $"import {{ {string.Join(", ", used)} }} from '{runtimeUrl}';\n";
    }

    private static string? RuntimeJsUrl()
    {
        var root = RepoRoot.Find();
        if (root == null) return null;
        var path = Path.Combine(root, "src", "eQuantic.UI.Server", "wwwroot", "runtime.js");
        return File.Exists(path) ? new Uri(path).AbsoluteUri : null;
    }
}
