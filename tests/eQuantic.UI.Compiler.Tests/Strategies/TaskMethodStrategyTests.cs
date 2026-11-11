using eQuantic.UI.Compiler.CodeGen;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class TaskMethodStrategyTests
{
    /// <summary>A class of the app's that shares the name keeps its own methods: the call is the app's,
    /// not the runtime's.</summary>
    [Fact]
    public void AClassNamedTask_IsTheAppsOwn()
    {
        var tree = CSharpSyntaxTree.ParseText("""
            public static class Task { public static int FromException(int code) => code; }
            public class Sample { public int Read(int code) => Task.FromException(code); }
            """);
        var compilation = CSharpCompilation.Create("ClassUnderTest", [tree], [TestReferences.Of(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var converter = new CSharpToJsConverter { SymbolsAreAuthoritative = true };
        converter.SetSemanticModel(compilation.GetSemanticModel(tree));

        converter.ConvertExpression(tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single())
            .Should().Be("Task.fromException(code)");
    }
}
