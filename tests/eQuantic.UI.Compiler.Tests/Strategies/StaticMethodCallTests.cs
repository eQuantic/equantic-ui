using System;
using System.Linq;
using System.Reflection;
using eQuantic.UI.Compiler.CodeGen;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// Sibling STATIC members are reached through the enclosing class, never bare. A developer's natural
/// helper factoring — <c>Thousands(n)</c>, <c>onPressed: Helper</c> — must transpile to
/// <c>Widget.thousands(n)</c> / <c>Widget.helper</c>, not a bare <c>thousands(...)</c> that is
/// <c>undefined</c> at runtime (the write-once showroom regression). Instance calls still take
/// <c>this.</c>. Resolution needs the semantic model, so these build a real compilation.
/// </summary>
public class StaticMethodCallTests
{
    [Fact]
    public void StaticSiblingCall_IsQualifiedWithTheClass()
        => Convert("Format(5)", "static string Format(int n) => \"x\";")
            .Should().Be("Widget.format(5)");

    [Fact]
    public void StaticSiblingCall_ThroughAnotherStaticMethod_IsQualified()
        => Convert("Format(Wrap(2))", "static string Format(int n) => \"x\"; static int Wrap(int n) => n;")
            .Should().Be("Widget.format(Widget.wrap(2))");

    [Fact]
    public void StaticMethodGroup_PassedAsDelegate_IsQualified()
        => Convert("Use(Format)",
                "static string Format(int n) => \"x\"; static void Use(System.Func<int, string> f) {}")
            .Should().Be("Widget.use(Widget.format)");

    [Fact]
    public void InstanceCall_StillUsesThis()
        => Convert("Render()", "string Render() => \"x\";")
            .Should().Be("this.render()");

    /// <summary>Convert the first statement's expression of <c>Method()</c>, with <paramref name="members"/>
    /// injected as siblings on the enclosing <c>Widget</c> class (so the semantic model resolves them).</summary>
    private static string Convert(string call, string members)
    {
        var tree = CSharpSyntaxTree.ParseText($@"
            public class Widget {{
                {members}
                void Method() {{ {call}; }}
            }}");

        var compilation = CSharpCompilation.Create("StaticMethodTest", new[] { tree }, new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
        });

        var converter = new CSharpToJsConverter();
        converter.SetSemanticModel(compilation.GetSemanticModel(tree));

        var method = tree.GetRoot().DescendantNodes()
            .OfType<MethodDeclarationSyntax>().First(m => m.Identifier.Text == "Method");
        var expression = ((ExpressionStatementSyntax)method.Body!.Statements.First()).Expression;

        return converter.ConvertExpression(expression);
    }
}
