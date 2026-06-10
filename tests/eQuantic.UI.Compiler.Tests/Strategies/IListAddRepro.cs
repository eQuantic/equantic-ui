using eQuantic.UI.Compiler.CodeGen;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// Regression guard: with a real semantic model, calling Add() on an interface-typed collection
/// (IList&lt;T&gt;/ICollection&lt;T&gt;, where Add is inherited from ICollection&lt;T&gt;) must map to the
/// JS array <c>push</c> — not degrade to a naive camel-cased <c>.add()</c>. This mirrors HtmlElement.Children
/// (IList&lt;IComponent&gt;), which broke SPA boot when the eqc semantic model lacked the eQuantic references
/// and could not resolve the receiver type. The plain ListStrategyTests only cover the no-semantic-info
/// fallback, so they never exercised this path.
/// </summary>
public class IListAddRepro
{
    private static string ConvertAddOn(string receiverType)
    {
        var converter = new CSharpToJsConverter();
        var tree = CSharpSyntaxTree.ParseText($@"
            using System.Collections.Generic;

            public interface IComponent {{ IList<IComponent> Children {{ get; }} }}
            public abstract class HtmlElement : IComponent {{
                protected readonly List<IComponent> _children = new();
                public IList<IComponent> Children => _children;
            }}
            public class Box : HtmlElement {{ }}

            public class TestClass {{
                public void Method() {{
                    {receiverType} target = new Box();
                    var child = (IComponent)null;
                    target.Children.Add(child);
                }}
            }}");

        var compilation = CSharpCompilation.Create("IListAddRepro", new[] { tree },
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(List<>).Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location)
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        converter.SetSemanticModel(compilation.GetSemanticModel(tree));

        var addStmt = tree.GetRoot().DescendantNodes()
            .OfType<ExpressionStatementSyntax>()
            .First(s => s.Expression is InvocationExpressionSyntax);
        return converter.ConvertExpression(addStmt.Expression);
    }

    [Theory]
    [InlineData("Box")]      // concrete receiver, Children typed IList<IComponent>
    [InlineData("IComponent")] // interface receiver, Children typed IList<IComponent>
    public void ChildrenAdd_MapsToPush(string receiverType)
    {
        ConvertAddOn(receiverType).Should().Be("target.children.push(child)");
    }
}
