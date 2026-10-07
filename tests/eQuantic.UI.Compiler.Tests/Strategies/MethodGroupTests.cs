using eQuantic.UI.Compiler.CodeGen;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// A method group is a delegate made from a method and the receiver C# reads when the delegate is
/// made. The receiver is read once (#619); <c>base.M</c> is the base's method on this object, and
/// <c>super</c> is not a value JavaScript lets anything be passed; and an extension's method lives on
/// the home its call goes to, which the receiver never has as a member (#655).
/// </summary>
public class MethodGroupTests
{
    private const string Types = """
        public class Counter { public int Made; public Counter Make() { Made++; return this; } public int Value() => Made; }
        public class Animal { public virtual string Sound() => "..."; }
        public static class Ext { public static string Twice(this string s) => s + s; }
        """;

    [Fact]
    public void AReceiverThatIsACall_IsReadOnce() =>
        Convert("public class Sample { public object Run() { var c = new Counter(); Func<int> read = c.Make().Value; return read(); } }")
            .Js.Should().Contain("let read = (($0) => $0.value.bind($0))(c.make());");

    [Fact]
    public void AReceiverThatIsANameOrThis_IsWrittenWhereItIs() =>
        Convert("public class Sample { public object Run() { var c = new Counter(); Func<int> read = c.Value; "
                + "Func<string> text = this.ToString; return read() + text(); } }")
            .Js.Should().Contain("let read = c.value.bind(c);").And.Contain("let text = this.toString.bind(this);");

    [Fact]
    public void ABaseGroup_BindsTheBaseMethodToThis()
    {
        var (js, diagnostics) = Convert(
            "public class Dog : Animal { public override string Sound() => \"woof\"; public object Run() { Func<string> quiet = base.Sound; return quiet(); } }");
        js.Should().Contain("let quiet = super.sound.bind(this);").And.NotContain("bind(super)");
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void AnExtensionGroup_BindsItsReceiverOnItsHome_ReadOnce()
    {
        var (js, diagnostics) = Convert(
            "public class Sample { public object Run() { var s = \"ab\"; Func<string> twice = s.Twice; "
            + "Func<string> again = s.Trim().Twice; return twice() + again(); } }");
        js.Should().Contain("let twice = Ext.twice.bind(Ext, s);");
        js.Should().Contain("let again = Ext.twice.bind(Ext, $eq.text.trim(s));");
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void AnExtensionGroupNothingEmits_FailsTheBuild_AsItsCallDoes() =>
        Convert("public class Sample { public object Run() { var list = new System.Collections.Generic.List<int>(); "
                + "Func<bool> any = list.Any; return any(); } }")
            .Diagnostics.Should().Contain(d => d.Code == "EQ2004" && d.Message.Contains("write the lambda that calls it"),
                "the group of a BCL extension named a member no array has, and the lambda is what translates");

    private static (string Js, IReadOnlyList<ConversionDiagnostic> Diagnostics) Convert(string source)
    {
        var tree = CSharpSyntaxTree.ParseText("using System;\nusing System.Linq;\n" + source + "\n" + Types);
        var compilation = CSharpCompilation.Create("MethodGroup", [tree], References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
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
