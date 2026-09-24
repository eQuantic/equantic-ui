using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using eQuantic.UI.Compiler.CodeGen;
using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// A local function's name through the emitter a plain class takes, where the conformance harness
/// cannot go: it emits records and structs, not classes. The runs themselves are in the
/// conformance suite (<c>LocalNameConformanceTests</c>).
/// </summary>
public class LocalFunctionNameTests
{
    [Theory]
    // A constructor with no parameters of its own takes `props` in JavaScript, and `Props`
    // camel-cased is that name: `const props` beside the parameter did not parse.
    [InlineData("Props")]
    // A name the casing leaves alone yields too: C# never saw the emitter's `props`, so nothing
    // kept the two apart.
    [InlineData("props")]
    public void InAConstructor_ItDoesNotRedeclareTheParameterTheEmitterAdds(string function)
    {
        var ts = TestHelper.ConvertClass(
            $"public int Value {{ get; set; }} public Setup() {{ int {function}() => 5; Value = {function}(); }}", "Setup");

        ts.Should().Contain("const props$ = ").And.Contain("this.value = props$()");
        ts.Should().NotContain("const props = ");
    }

    [Fact]
    public void ANameNothingHolds_KeepsItsCasing()
    {
        // The rename is for a collision only: a function whose cased name is free reads as authored.
        var ts = TestHelper.ConvertClass("public int Area(int side) { int Square(int n) => n * n; return Square(side); }");

        ts.Should().Contain("const square = ").And.Contain("return square(side)");
    }

    [Fact]
    public void ANameWrittenWithAnUnderscore_NeverGoesOutAsItIs()
    {
        // The lowerings spell their own bindings with an underscore: `list.Remove(x)` assigns `_idx`,
        // and a function `_idx` in scope took that assignment (Copilot's review of #399). Such a
        // name always takes a `$`, which none of theirs holds.
        var ts = TestHelper.ConvertClass("public int M() { int _idx() => 1; return _idx(); }");

        ts.Should().Contain("const _idx$ = ").And.Contain("return _idx$()");
    }

    [Fact]
    public void WithNoModel_TheReferencesNameWhatTheDeclarationNamed()
    {
        // The playground converts with no semantic model, so no reference has a symbol to ask. The
        // declaration still went through the owner (`d$`, `delete$`), and the call camel-cased by
        // hand (`d()`, `delete()`), a module that did not parse (Copilot's review of #399).
        var method = CSharpSyntaxTree.ParseText(
                "class C { int M() { int d = 1; int D() => 2; int Delete() => 3; System.Func<int> f = Delete; return d + D() + Delete() + f(); } }",
                ParseDefaults.Options)
            .GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single();

        var js = new CSharpToJsConverter().Convert(method.Body!);

        js.Should().Contain("const d$ = ").And.Contain("const delete$ = ");
        js.Should().Contain("d$()").And.Contain("delete$()").And.Contain("f = delete$");
        js.Should().NotContain(" d()").And.NotContain("delete()");
    }

    /// <summary>
    /// The lowercase globals of JavaScript and the browser, the universe the scan below picks from.
    /// A local function named like one the emitted code reads would shadow it.
    /// </summary>
    private static readonly string[] PlatformGlobals =
    [
        "globalThis", "undefined", "isNaN", "isFinite", "parseInt", "parseFloat", "encodeURI", "decodeURI",
        "encodeURIComponent", "decodeURIComponent", "escape", "unescape", "console", "window", "document",
        "navigator", "location", "history", "localStorage", "sessionStorage", "fetch", "setTimeout",
        "clearTimeout", "setInterval", "clearInterval", "queueMicrotask", "structuredClone",
        "requestAnimationFrame", "cancelAnimationFrame", "crypto", "performance", "atob", "btoa",
        "alert", "confirm", "prompt", "self",
    ];

    private static string RepoRoot([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", "..", ".."));

    /// <summary>
    /// Every global the compiler's own source emits: a string it writes that calls one, reads a member
    /// off one, or is one. Read as C#, by the parser eqc reads with, since what matters is the text a
    /// string holds and not how a line happens to look.
    /// </summary>
    private static IReadOnlyList<string> EmittedGlobals()
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);
        var codeGen = Path.Combine(RepoRoot(), "src", "eQuantic.UI.Compiler", "CodeGen");
        foreach (var file in Directory.EnumerateFiles(codeGen, "*.cs", SearchOption.AllDirectories))
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file), ParseDefaults.Options).GetRoot();
            foreach (var token in root.DescendantTokens())
            {
                if (!token.IsKind(SyntaxKind.StringLiteralToken) && !token.IsKind(SyntaxKind.InterpolatedStringTextToken)
                    && !token.IsKind(SyntaxKind.SingleLineRawStringLiteralToken)
                    && !token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken))
                    continue;
                foreach (var global in PlatformGlobals)
                    if (token.ValueText == global
                        || Regex.IsMatch(token.ValueText, $@"(?<![\w$.]){global}\s*(\(|\.[A-Za-z_$])"))
                        found.Add(global);
            }
        }

        return found.ToList();
    }

    public static TheoryData<string> GlobalsTheCompilerEmits()
    {
        var data = new TheoryData<string>();
        foreach (var global in EmittedGlobals()) data.Add(global);
        return data;
    }

    [Fact]
    public void TheScan_FindsTheGlobalsItIsKnownToEmit()
    {
        // The instrument first: a scan that read nothing would leave the theory below with no case.
        EmittedGlobals().Should().Contain(["console", "parseInt", "crypto", "undefined"]);
    }

    [Theory]
    [MemberData(nameof(GlobalsTheCompilerEmits))]
    public void AGlobalTheOutputReads_IsNeverALocalFunctionsName(string global)
    {
        // The name as written, which the casing leaves alone: a function that kept it would shadow
        // the global for every lowering beside it that reads it.
        var ts = TestHelper.ConvertClass($"public int M() {{ int {global}() => 1; return {global}(); }}");

        ts.Should().Contain($"const {global}$ = ").And.Contain($"return {global}$()");
    }
}
