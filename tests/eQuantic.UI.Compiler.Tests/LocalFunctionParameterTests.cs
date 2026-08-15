using System.Linq;
using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A local function's parameters carry what C# lets them carry.
/// <para>
/// The DEFAULT was dropped while the method emitter had always kept it, so a helper written
/// `Pane(title, body, leading = null)` and called with two arguments emitted three REQUIRED
/// parameters — TS2554 from source that compiles in C#. Found by writing the ListDetail component,
/// which is exactly the shape that wants a local helper: one builder called twice, once with a
/// leading affordance and once without.
/// </para>
/// </summary>
public class LocalFunctionParameterTests
{
    private readonly CSharpToJsConverter _converter = new();

    private string Convert(string bodyCode, bool annotations = true)
    {
        var classCode = $"class Wrapper {{ void Method() {{ {bodyCode} }} }}";
        var tree = CSharpSyntaxTree.ParseText(classCode);
        var compilation = CSharpCompilation.Create("probe", [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        _converter.SetSemanticModel(compilation.GetSemanticModel(tree));
        _converter.EmitTypeAnnotations(annotations);

        var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        return _converter.Convert(method.Body!).Trim();
    }

    /// <summary>A `null` default becomes `?:` — that is what the annotation is FOR, and it keeps the
    /// twin idiomatic instead of spelling `= undefined`.</summary>
    [Fact]
    public void ANullDefault_BecomesAnOptionalParameter()
    {
        var js = Convert("string Pane(string title, string? leading = null) => title; Pane(\"a\");");

        Assert.Contains("leading?: string | null", js);
        Assert.DoesNotContain("leading: string | null)", js);
    }

    [Fact]
    public void AValueDefault_KeepsItsValue()
    {
        var js = Convert("int Add(int a, int b = 2) => a + b; Add(1);");

        Assert.Contains("b: number = 2", js);
    }

    /// <summary>Without annotations there is no `?:` to lean on, so the default itself has to be
    /// emitted or the parameter arrives undefined.</summary>
    [Fact]
    public void InPlainJavaScript_TheDefaultIsStillWritten()
    {
        var js = Convert("string Pane(string title, string? leading = null) => title; Pane(\"a\");",
            annotations: false);

        Assert.Contains("leading = null", js);
        Assert.DoesNotContain(": string", js);
    }

    /// <summary>`params` is a REST parameter here for the same reason it is on a method: emitted as a
    /// plain one it binds only the first argument.</summary>
    [Fact]
    public void ParamsBecomesARestParameter()
    {
        var js = Convert("int Count(params int[] values) => values.Length; Count(1, 2);");

        Assert.Contains("...values", js);
    }

    [Fact]
    public void APlainParameter_IsUnchanged()
    {
        var js = Convert("int Twice(int a) => a * 2; Twice(2);");

        Assert.Contains("(a: number)", js);
        Assert.DoesNotContain("a?:", js);
    }
}
