using eQuantic.UI.Compiler.CodeGen;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// A null-conditional call whose translation is a helper (#536). A receiver read again (a local, a
/// parameter, <c>this</c>) is guarded by a conditional with no function around the tail, so an
/// argument that awaits is awaited in the method it is written in, only when the receiver is not
/// null, and the call's own answer is never awaited: an async arrow suspended where C# does not,
/// and handed back the result of a task the call returned. Any other receiver is assigned to a
/// temporary the statement around it declares (#539), so the same holds behind it: no function
/// around the tail, wherever the receiver comes from.
/// </summary>
public class NullConditionalTailTests
{
    private const string Needle = "async Task<string> Needle() { await Task.Yield(); return \"a\"; } ";

    private const string Holder = "public class Holder { public string Name { get; set; } = \"abc\"; "
        + "public System.Collections.Generic.ICollection<string> Items { get; set; } }";

    [Fact]
    public void ALocalReceiver_IsGuardedByAConditional_WithTheAwaitInsideIt()
    {
        var (js, diagnostics) = Convert(
            "string s = \"abc\"; " + Needle + "return s?.StartsWith(await Needle(), StringComparison.Ordinal);");
        js.Should().Contain("return (s == null ? null : $eq.text.startsWith(s, await needle(), 'ordinal'));");
        js.Should().NotMatchRegex(@"async \(\$\w+\) => \$\w+ == null", "no async arrow guards the tail");
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void ALocalNamedInTheTail_LeavesTheReceiverWhereItWas() =>
        Convert("string s = \"abc\"; return s?.Replace(s, \"x\");").Js
            .Should().Contain("(s == null ? null : $eq.text.replace(s, s, 'x', 'ordinal'))");

    [Fact]
    public void ACallReceiver_WithATailThatAwaits_IsBoundToATemporary_AndTheAwaitStaysInTheMethod()
    {
        var (js, diagnostics) = Convert(
            "string Get() => \"abc\"; " + Needle + "return Get()?.StartsWith(await Needle(), StringComparison.Ordinal);");
        js.Should().Contain("let $n0;");
        js.Should().Contain("return (($n0 = get()) == null ? null : $eq.text.startsWith($n0, await needle(), 'ordinal'));");
        js.Should().NotContain("=> $n0");
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void ACallReceiver_WithATailThatDoesNotAwait_IsStillBoundOnce()
    {
        var js = Convert("string Get() => \" abc \"; return Get()?.Trim();").Js;
        js.Should().Contain("let $n0;");
        js.Should().Contain("return (($n0 = get()) == null ? null : $eq.text.trim($n0));");
    }

    [Fact]
    public void ANullConditionalInTheTailOfAnother_BindsATemporaryOfItsOwn()
    {
        var js = Convert(
            "string Get() => \"abc\"; string Other() => \" b \"; return Get()?.Replace(Other()?.Trim() ?? \"b\", \"x\");").Js;
        js.Should().Contain("let $n0;").And.Contain("let $n1;");
        js.Should().Contain("(($n0 = get()) == null ? null : $eq.text.replace($n0, (($n1 = other()) == null ? null : $eq.text.trim($n1)) ?? 'b', 'x', 'ordinal'))");
    }

    [Fact]
    public void TwoStatements_EachDeclareTheirOwn()
    {
        var js = Convert("string Get() => \" abc \"; var a = Get()?.Trim(); var b = Get()?.Trim(); return a + b;").Js;
        Count(js, "let $n0;").Should().Be(1);
        Count(js, "let $n1;").Should().Be(1);
        js.IndexOf("let $n0;", StringComparison.Ordinal).Should().BeLessThan(js.IndexOf("let a =", StringComparison.Ordinal));
        js.IndexOf("let $n1;", StringComparison.Ordinal).Should().BeLessThan(js.IndexOf("let b =", StringComparison.Ordinal));
    }

    [Fact]
    public void ABodyWrittenWithoutBraces_GetsTheBracesItsDeclarationNeeds()
    {
        var js = Convert("string Get() => \" abc \"; var flag = true; string x = null; if (flag) x = Get()?.Trim(); return x;").Js;
        js.Should().MatchRegex(@"if \(flag\) \{\s*let \$n0;\s*x = \(\(\$n0 = get\(\)\) == null \? null : \$eq\.text\.trim\(\$n0\)\);\s*\}");
    }

    [Fact]
    public void AConciseLambda_ThatBindsATemporary_DeclaresItInABlockOfItsOwn()
    {
        var js = Convert("Func<Holder, bool> f = h => h.Items?.Contains(\"a\") == true; return f(new Holder());", Holder).Js;
        js.Should().MatchRegex(@"\(h\) => \{\s*let \$n0;\s*return \(\(\$n0 = h\.items\) == null \? null : \$eq\.collections\.contains\(\$n0, 'a'\)\) === true;\s*\}");
        Count(js, "let $n0;").Should().Be(1, "the lambda declares it, and the statement around the lambda does not");
    }

    [Fact]
    public void AnAsyncConciseLambda_AwaitsInItsOwnBlock()
    {
        var (js, diagnostics) = Convert(
            Needle + "Func<Holder, Task<bool?>> f = async h => h.Name?.StartsWith(await Needle(), StringComparison.Ordinal); return await f(new Holder());",
            Holder);
        js.Should().MatchRegex(@"async \(h\) => \{\s*let \$n0;\s*return \(\(\$n0 = h\.name\) == null \? null : \$eq\.text\.startsWith\(\$n0, await needle\(\), 'ordinal'\)\);\s*\}");
        diagnostics.Should().BeEmpty();
    }

    private const string Bell = "public class Bell { public string? Tag; public Bell? Next; public void Ring() { } }";

    /// <summary>
    /// An optional chain answers null where its value is used, as C# does, and stays bare where nothing
    /// can tell undefined from null: a call that returns nothing, a statement, the left of a
    /// <c>??</c>, and the tail of another chain (#633).
    /// </summary>
    [Fact]
    public void AChain_AnswersNullWhereItsValueIsUsed_AndOnlyThere()
    {
        var js = Convert(
            "Bell? b = null; string? tag = b?.Tag; string? deep = b?.Next?.Tag; var named = b?.Tag ?? \"none\"; "
            + "b?.Ring(); System.Action ring = () => b?.Ring(); return tag + deep + named;", Bell).Js;

        js.Should().Contain("let tag = (b?.tag ?? null);");
        js.Should().Contain("let deep = (b?.next?.tag ?? null);", "the chain answers null where it ends, once");
        js.Should().Contain("let named = b?.tag ?? 'none';");
        js.Should().Contain("b?.ring();");
        js.Should().Contain("() => b?.ring()");
        js.Should().NotContain("ring() ?? null", "a call that returns nothing has no value to tell apart");
    }

    [Fact]
    public void AConciseLambda_ThatBindsNothing_StaysAnExpression() =>
        Convert("Func<Holder, int?> f = h => h.Name?.Length; return f(new Holder());", Holder).Js
            .Should().Contain("(h) => (h.name?.length ?? null)", "the lambda answers null where h.Name is, as C# does (#633)");

    [Fact]
    public void AStatementConvertedAgain_DeclaresTheTemporaryItsTranslationNames()
    {
        // The node cache serves an expression's translation again. One that names a temporary is
        // converted afresh instead, so the statement it lands in declares the slot it uses: served
        // from the cache, it named a slot only the first statement declared.
        var (converter, run) = Converter("string Get() => \" abc \"; var a = Get()?.Trim(); return a;");
        var statement = run.Body!.Statements.OfType<LocalDeclarationStatementSyntax>().Single();

        var first = converter.ConvertStatement(statement);
        var second = converter.ConvertStatement(statement);

        first.Should().Contain("let $n0;").And.Contain("($n0 = get())");
        second.Should().Contain("let $n1;").And.Contain("($n1 = get())").And.NotContain("$n0");
    }

    private static int Count(string text, string needle)
    {
        var count = 0;
        for (var at = text.IndexOf(needle, StringComparison.Ordinal); at >= 0;
             at = text.IndexOf(needle, at + needle.Length, StringComparison.Ordinal))
            count++;
        return count;
    }

    private static (string Js, IReadOnlyList<ConversionDiagnostic> Diagnostics) Convert(string body, string types = "")
    {
        var (converter, run) = Converter(body, types);
        return (converter.Convert(run.Body!), converter.Diagnostics.ToList());
    }

    private static (CSharpToJsConverter Converter, MethodDeclarationSyntax Run) Converter(string body, string types = "")
    {
        var tree = CSharpSyntaxTree.ParseText(
            "using System;\nusing System.Threading.Tasks;\npublic class Sample\n{\n    public async Task<object> Run()\n    {\n        "
            + body + "\n    }\n}\n" + types);
        var compilation = CSharpCompilation.Create("NullConditionalTail", [tree], References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var converter = new CSharpToJsConverter { SymbolsAreAuthoritative = true };
        converter.SetSemanticModel(compilation.GetSemanticModel(tree));
        return (converter, tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.Text == "Run"));
    }

    /// <summary>The framework, through the one owner of the test process's references (#549).</summary>
    private static readonly Lazy<MetadataReference[]> References = new(() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(TestReferences.Of)
            .ToArray());
}
