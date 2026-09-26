using System.Linq;
using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// An EXPLICIT C# local type crosses as a TypeScript annotation — exactly when it matters.
/// <para>
/// `VisualNode menu = new Anchored(...)` declares the base on purpose: the variable is reassigned
/// to a Shortcut two lines later. Unannotated, TypeScript infers the derived type and rejects the
/// reassignment — which is how the transpiled Menu turned red the day it grew keyboard bindings,
/// caught by tsc and by nothing else (vitest does not type-check).
/// </para>
/// </summary>
public class LocalTypeAnnotationTests
{
    private readonly CSharpToJsConverter _converter = new();

    private string Convert(string bodyCode, bool typeAnnotations = true)
    {
        // The TypeScript a build writes, unless the test asks for plain JavaScript.
        _converter.EmitTypeAnnotations(typeAnnotations);
        // A REAL semantic model: the widening rule compares the declared type against the
        // initializer's, and both have to resolve for the comparison to mean anything.
        var classCode = "class Base { } class A : Base { } class B : Base { }\n"
            + $"class Wrapper {{ void Method(bool flag) {{ {bodyCode} }} }}";
        var tree = CSharpSyntaxTree.ParseText(classCode);
        var compilation = CSharpCompilation.Create("probe", [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        _converter.SetSemanticModel(compilation.GetSemanticModel(tree));

        var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        return _converter.Convert(method.Body!).Trim();
    }

    [Fact]
    public void ADeclaredBaseType_CrossesAsAnAnnotation()
    {
        var js = Convert("Base x = new A(); x = new B();");
        Assert.Contains("let x: Base = new A();", js);
    }

    [Fact]
    public void VarStaysBare_InferenceWasTheAuthorsChoice()
    {
        var js = Convert("var y = new A();");
        Assert.Contains("let y = new A();", js);
        Assert.DoesNotContain("y:", js);
    }

    [Fact]
    public void ADeclaredTypeEqualToTheInitializer_StaysBare()
    {
        // Annotating everything would be noise; the annotation exists for the WIDENING case only.
        var js = Convert("A same = new A();");
        Assert.Contains("let same = new A();", js);
    }

    /// <summary>
    /// A local that STARTS as null has nothing for TypeScript to infer from: <c>let path = null</c>
    /// is typed as it goes, and a closure that reads it sees <c>any</c>, which the runtime's own
    /// build refuses. The patch reader resets its paths from a local function, and was the first to
    /// hit it. The declared type crosses, with the null it starts as.
    /// </summary>
    [Theory]
    [InlineData("string? path = null;", "let path: string | null = null;")]
    [InlineData("int? count = null;", "let count: number | null = null;")]
    [InlineData("string? later;", "let later: string | null = null;")]
    [InlineData("Base? found = null;", "let found: Base | null = null;")]
    [InlineData("System.Action? done = null;", "let done: (() => void) | null = null;")]
    [InlineData("string plain = null;", "let plain: string | null = null;")]
    [InlineData("System.Collections.Generic.List<string?>? names = null;", "let names: (string | null)[] | null = null;")]
    public void ALocalThatStartsNull_CrossesWithItsDeclaredType(string csharp, string expected)
    {
        Assert.Contains(expected, Convert(csharp));
    }

    /// <summary>
    /// Plain JavaScript carries no annotation, whatever the local: the design host compiles with none
    /// and inlines what eqc writes as one script, so a <c>: T</c> in it is a syntax error that keeps
    /// the preview from loading. A local that starts null was the common case, and a declared base
    /// or an empty list was already written with its annotation there.
    /// </summary>
    [Theory]
    [InlineData("string? path = null;", "let path = null;")]
    [InlineData("Base found = new A();", "let found = new A();")]
    [InlineData("var names = new System.Collections.Generic.List<string>();", "let names = [];")]
    public void APlainJavaScriptLocal_CarriesNoAnnotation(string csharp, string expected)
    {
        Assert.Contains(expected, Convert(csharp, typeAnnotations: false));
    }

    /// <summary>
    /// A local with no initializer and a type that holds no null is definitely assigned before C#
    /// lets anything read it. Annotated <c>number | null</c>, every read after a branch would be a
    /// possible null to TypeScript, so it stays as it was.
    /// </summary>
    [Fact]
    public void ALocalWithNoStartOfANonNullableType_StaysBare()
    {
        var js = Convert("float x0; if (flag) x0 = 1; else x0 = 2;");
        Assert.Contains("let x0 = null;", js);
    }

    [Fact]
    public void ANullableBase_CrossesAsTheUnionItIs()
    {
        var js = Convert("Base? z = flag ? new A() : null;");
        Assert.Contains("let z: Base | null =", js);
    }
}
