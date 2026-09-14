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
        // Top-level undefined canonicalizes to null: the transpiled world treats them as ONE
        // (the `== null` doctrine), and C#'s side of a guarded chain answers null.
        var program = $"{BuildHelperImport(jsBlock + types)}{types}{Log(jsBlock)}";

        var actual = JsExecutor.Run(program);
        var expected = DotNetEvaluator.EvaluateToJson(csharpStatements, prelude);

        actual.Should().Be(
            expected,
            $"C# block `{csharpStatements}` (transpiled to JS `{jsBlock}`) must behave identically to .NET");
    }

    /// <summary>
    /// For the handful of values .NET itself does not compute identically on every platform.
    ///
    /// <para>
    /// The contract of this suite is "the twin behaves identically to .NET", and that sentence has
    /// a hidden assumption: that ".NET" is one answer. For most of the surface it is. For a few
    /// numeric functions it is the platform's libm wearing a .NET name, and the answers differ in
    /// the last bit — <c>double.RootN(27.0, 3)</c> is exactly <c>3</c> on macOS and
    /// <c>3.0000000000000004</c> on Linux, measured on both.
    /// </para>
    ///
    /// <para>
    /// The twin is not the thing that is wrong there. 27's cube root IS 3, and 3 is representable,
    /// so the JS answer is exact and one platform's .NET is a bit off it. Rewriting the twin to
    /// reproduce a libm's error on one OS and be wrong on the other is not a contract worth having.
    /// </para>
    ///
    /// <para>
    /// So these compare within ONE ULP and each says which platform difference it is fencing.
    /// A ULP, not an epsilon: a tolerance in decimals would quietly admit a real translation bug of
    /// the kind this suite exists to catch, where "the adjacent double" cannot.
    /// </para>
    /// </summary>
    /// <param name="why">The measured difference, named. It goes into the failure message, because
    /// the next person here needs to know whether they are looking at a new platform gap or at the
    /// one already known.</param>
    public static void AssertStatementsWithinAnUlpOfDotNet(
        string csharpStatements, string why, string prelude = "")
    {
        var jsBlock = Transpiler.TranspileStatements(csharpStatements, prelude);
        var types = Transpiler.EmitDeclaredRecordTypes(prelude);
        var program = $"{BuildHelperImport(jsBlock + types)}{types}{Log(jsBlock)}";

        var actual = JsExecutor.Run(program);
        var expected = DotNetEvaluator.EvaluateToJson(csharpStatements, prelude);
        if (actual == expected) return;

        double.TryParse(actual, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var actualValue)
            .Should().BeTrue($"`{csharpStatements}` answered `{actual}`, which is not a number — "
                + "this overload is for a numeric difference of one bit, and that is not one");
        double.TryParse(expected, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var expectedValue)
            .Should().BeTrue($"`{csharpStatements}` answered `{expected}` on .NET, which is not a number");

        UlpsBetween(actualValue, expectedValue).Should().BeLessThanOrEqualTo(1,
            $"C# block `{csharpStatements}` (transpiled to JS `{jsBlock}`) answered {actual} where "
            + $".NET answered {expected}. One bit is the known platform difference: {why}. More than "
            + "one is a translation defect, and this overload is not the place to make it pass");
    }

    /// <summary>
    /// How many representable doubles lie between two values — 0 is identical, 1 is adjacent.
    /// Sign-magnitude bits ordered so that the comparison means what it says across zero.
    /// <para>
    /// PUBLIC so it can be tested directly, and it has to be: the caller above returns early when
    /// the two sides agree, which they do on macOS for the one case that uses it. On this machine
    /// the tolerance path never runs, so a suite that only ran the conformance case would be
    /// claiming a comparison it had never exercised.
    /// </para>
    /// </summary>
    public static long UlpsBetween(double a, double b)
    {
        if (double.IsNaN(a) || double.IsNaN(b)) return a.Equals(b) ? 0 : long.MaxValue;
        var left = Ordered(a);
        var right = Ordered(b);
        // UNSIGNED, then saturated. The obvious `Math.Abs(left - right)` reads fine and is wrong at
        // both ends of the domain: it THREW on (-2.0, 2.0), where the difference is exactly
        // long.MinValue and has no positive counterpart, and it wrapped (-double.MaxValue,
        // double.MaxValue) to about nine quadrillion — a small number for the widest pair there is,
        // which is the failure that would have quietly passed a tolerance. Measured both. Found in
        // review.
        var distance = left > right
            ? (ulong)left - (ulong)right
            : (ulong)right - (ulong)left;
        return distance > long.MaxValue ? long.MaxValue : (long)distance;

        static long Ordered(double value)
        {
            var bits = BitConverter.DoubleToInt64Bits(value);
            return bits < 0 ? long.MinValue - bits : bits;
        }
    }

    /// <summary>
    /// As above, but with a C# <paramref name="prelude"/> of type declarations (e.g. an enum) made
    /// available to both the transpiler's semantic model and the .NET evaluator.
    /// </summary>
    public static void AssertSameAsDotNet(string csharpExpression, string prelude)
    {
        var js = Transpiler.TranspileExpression(csharpExpression, prelude);
        var types = Transpiler.EmitDeclaredRecordTypes(prelude);
        var program = $"{BuildHelperImport(js + types)}{types}console.log(JSON.stringify(((v) => v === undefined ? null : v)({js})))";

        var actual = JsExecutor.Run(program);
        var expected = DotNetEvaluator.EvaluateToJson(csharpExpression, prelude);

        actual.Should().Be(
            expected,
            $"C# `{csharpExpression}` (transpiled to JS `{js}`) must behave identically to .NET");
    }

    /// <summary>
    /// The line that runs the block and prints its result.
    /// <para>
    /// A block that AWAITS cannot run inside a plain arrow — `await` there is a SyntaxError and bun
    /// exits before printing anything, which reads as a translation failure and is not one. So the
    /// awaiting shape gets an async IIFE and prints from inside it, rather than a top-level await:
    /// the harness writes a bare script, and top-level await needs a module.
    /// </para>
    /// <para>
    /// Conditional ON PURPOSE. Every non-awaiting case keeps the exact program it had, byte for
    /// byte, so a thousand green conformance cases are not quietly re-run through a new shape.
    /// </para>
    /// </summary>
    private static string Log(string jsBlock)
    {
        const string canonical = "((v) => v === undefined ? null : v)";
        return Regex.IsMatch(jsBlock, @"\bawait\b")
            ? $"(async () => {{ const $r = await (async () => {jsBlock})(); "
              + $"console.log(JSON.stringify({canonical}($r))); }})()"
            : $"console.log(JSON.stringify({canonical}((() => {jsBlock})())))";
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
