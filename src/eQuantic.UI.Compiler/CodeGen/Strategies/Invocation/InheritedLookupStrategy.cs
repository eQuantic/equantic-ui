using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Invocation;

/// <summary>
/// <c>context.DependOn&lt;T&gt;()</c> and <c>context.Find&lt;T&gt;()</c>, the lookup of an inherited
/// value (docs/ELEMENT-TREE-PLAN.md 3.3), lowered with <c>T</c>'s twin as its argument:
/// <c>context.dependOn(Score)</c>. On .NET the context hands the engine <c>inherited is T</c>, and a type
/// argument does not reach JavaScript, so without the twin the browser's context had nothing to look
/// for and answered null (#1395). The runtime's position tests what it meets with <c>instanceof</c>.
/// <para>
/// Bound by the model, never by the name: a list's <c>Find</c> is another method. The type argument is
/// a closed type in any component that compiles against the constraint; an open one is refused, since
/// no twin stands for it.
/// </para>
/// </summary>
internal sealed class InheritedLookupStrategy : IExpressionIrStrategy
{
    private const string Context = "eQuantic.UI.Primitives.ComponentContext";

    /// <summary>Before the ordinary invocation, which writes the call with its type argument dropped.</summary>
    public int Priority => 10;

    public bool CanConvert(SyntaxNode node, ConversionContext context) =>
        node is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax } invocation
        && Lookup(invocation, context) is not null;

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var method = Lookup(invocation, context)!;
        var receiver = context.Converter.ConvertIr(((MemberAccessExpressionSyntax)invocation.Expression).Expression);
        if (method.TypeArguments[0] is not INamedTypeSymbol inherited)
            return JsExpr.Opaque(context.Unhandled(node, $"{method.Name} with a type argument no twin stands for"));

        var name = method.Name == "DependOn" ? "dependOn" : "find";
        return JsExpr.Call(JsExpr.Member(receiver, name), JsExpr.Identifier(inherited.IntroduceTwin(context)));
    }

    /// <summary>The context's lookup this invocation binds to, or null for any other method.</summary>
    private static IMethodSymbol? Lookup(InvocationExpressionSyntax invocation, ConversionContext context) =>
        context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol
        {
            Name: "DependOn" or "Find",
            TypeArguments.Length: 1,
        } method
        && method.ContainingType?.ToDisplayString() == Context
            ? method
            : null;
}
