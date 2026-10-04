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
/// and handed back the result of a task the call returned. Any other receiver is bound by an arrow,
/// where an await has no faithful form, so a tail that awaits is refused there.
/// </summary>
public class NullConditionalTailTests
{
    private const string Needle = "async Task<string> Needle() { await Task.Yield(); return \"a\"; } ";

    [Fact]
    public void ALocalReceiver_IsGuardedByAConditional_WithTheAwaitInsideIt()
    {
        var (js, diagnostics) = Convert(
            "string s = \"abc\"; " + Needle + "return s?.StartsWith(await Needle(), StringComparison.Ordinal);");
        js.Should().Contain("return (s == null ? null : $eq.text.startsWith(s, await needle(), 'ordinal'));");
        js.Should().NotContain("async ($r)");
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void ALocalNamedInTheTail_LeavesTheReceiverWhereItWas() =>
        Convert("string s = \"abc\"; return s?.Replace(s, \"x\");").Js
            .Should().Contain("(s == null ? null : $eq.text.replace(s, s, 'x', 'ordinal'))");

    [Fact]
    public void ACallReceiver_WithATailThatAwaits_IsRefused() =>
        Convert("string Get() => \"abc\"; " + Needle + "return Get()?.StartsWith(await Needle(), StringComparison.Ordinal);")
            .Diagnostics.Should().Contain(d => d.Code == "EQ1004" && d.Message.Contains("bind the receiver to a local first"));

    [Fact]
    public void ACallReceiver_WithATailThatDoesNotAwait_IsStillBoundOnce() =>
        Convert("string Get() => \" abc \"; return Get()?.Trim();").Js
            .Should().Contain("(($r) => $r == null ? null : $eq.text.trim($r))(get())");

    private static (string Js, IReadOnlyList<ConversionDiagnostic> Diagnostics) Convert(string body)
    {
        var tree = CSharpSyntaxTree.ParseText(
            "using System;\nusing System.Threading.Tasks;\npublic class Sample\n{\n    public async Task<object> Run()\n    {\n        "
            + body + "\n    }\n}");
        var compilation = CSharpCompilation.Create("NullConditionalTail", [tree], References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var converter = new CSharpToJsConverter { SymbolsAreAuthoritative = true };
        converter.SetSemanticModel(compilation.GetSemanticModel(tree));
        var run = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.Text == "Run");
        return (converter.Convert(run.Body!), converter.Diagnostics.ToList());
    }

    /// <summary>The framework, through the one owner of the test process's references (#549).</summary>
    private static readonly Lazy<MetadataReference[]> References = new(() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(TestReferences.Of)
            .ToArray());
}
