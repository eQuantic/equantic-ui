using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// <c>value.GetHashCode()</c>, <c>base.GetHashCode()</c> and <c>HashCode.Combine(…)</c>, by .NET's
/// contract (<c>utils/hash.ts</c>): a value hashes as its <c>Equals</c> compares it, and a type that
/// overrides <c>GetHashCode</c> answers its own, the runtime asking its twin's <c>getHashCode</c>. Each
/// was a call of a <c>getHashCode</c> nothing defines, for a string, a number and a record alike, and
/// threw (#519). .NET's numbers are not stable across processes, so what the browser keeps is the
/// contract, never the server's number. A base call reaches the override the app wrote on the base,
/// and the identity's where the base's is <c>object</c>'s.
/// </summary>
public class GetHashCodeStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context) =>
        node is InvocationExpressionSyntax invocation
        && context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol method
        && (IsGetHashCode(method) || IsCombine(method));

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var method = (IMethodSymbol)context.SemanticHelper.GetSymbol(invocation)!;
        context.UsedHelpers.Add(Eq.Import);
        if (IsCombine(method))
            return JsExpr.Call(JsExpr.Identifier(Eq.HashCombine),
                invocation.ArgumentList.Arguments.Select(argument => context.Converter.ConvertIr(argument.Expression)).ToList());

        var self = JsExpr.Identifier("this");
        if (invocation.Expression is MemberAccessExpressionSyntax { Expression: BaseExpressionSyntax })
        {
            // The base's own override where the app wrote one, and object's, the identity's, otherwise.
            var declared = !method.IsImplicitlyDeclared && method.Locations.Any(location => location.IsInSource);
            return declared
                ? JsExpr.Call(JsExpr.Member(JsExpr.Identifier("super"), "getHashCode"))
                : JsExpr.Call(JsExpr.Identifier(Eq.HashIdentity), self);
        }

        var receiver = invocation.Expression is MemberAccessExpressionSyntax access
            ? context.Converter.ConvertIr(access.Expression)
            : self;
        return JsExpr.Call(JsExpr.Identifier(Eq.Hash), receiver);
    }

    /// <summary>An instance <c>GetHashCode()</c>, object's or any override of it.</summary>
    private static bool IsGetHashCode(IMethodSymbol method) =>
        method is { Name: "GetHashCode", IsStatic: false, Parameters.Length: 0 };

    /// <summary><c>System.HashCode.Combine</c>, of any arity.</summary>
    private static bool IsCombine(IMethodSymbol method) =>
        method is { Name: "Combine", IsStatic: true, ContainingType: { Name: "HashCode" } type }
        && type.ContainingNamespace?.ToDisplayString() == "System";

    public int Priority => 12;
}
