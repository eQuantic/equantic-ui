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

    private string Convert(string bodyCode)
    {
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

    [Fact]
    public void ANullableBase_CrossesAsTheUnionItIs()
    {
        var js = Convert("Base? z = flag ? new A() : null;");
        Assert.Contains("let z: Base | null =", js);
    }
}
