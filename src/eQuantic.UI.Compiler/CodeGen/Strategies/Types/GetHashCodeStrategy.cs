using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;
using eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// <c>value.GetHashCode()</c>, <c>base.GetHashCode()</c>, <c>HashCode.Combine(…)</c> and the method
/// group <c>value.GetHashCode</c>, by .NET's contract (<c>utils/hash.ts</c>): a value hashes as its
/// <c>Equals</c> compares it, and a type that overrides <c>GetHashCode</c> answers its own, the runtime
/// asking its twin's <c>getHashCode</c>. Each was a call of a <c>getHashCode</c> nothing defines, for a
/// string, a number and a record alike, and threw (#519). .NET's numbers are not stable across
/// processes, so what the browser keeps is the contract, never the server's number.
/// <para>
/// The receiver's TYPE settles what the runtime cannot see: an array is a reference, hashed by its
/// identity however its items change, where a value tuple, an array here too, hashes by its items. A
/// reference receiver is refused when null, as the instance call throws NullReferenceException, and
/// a value type never is (an empty <c>Nullable&lt;T&gt;</c> answers 0).
/// </para>
/// </summary>
public class GetHashCodeStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context) => node switch
    {
        InvocationExpressionSyntax invocation =>
            context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol method
            && (IsGetHashCode(method) || IsCombine(method)),
        // A method GROUP, `Func<int> f = n.GetHashCode;`, read a `getHashCode` a number does not have.
        MemberAccessExpressionSyntax access =>
            !IsCalled(access)
            && context.SemanticHelper.GetSymbol(access) is IMethodSymbol method
            && IsGetHashCode(method),
        _ => false,
    };

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        if (node is MemberAccessExpressionSyntax group) return Group(group, context);

        var invocation = (InvocationExpressionSyntax)node;
        var method = (IMethodSymbol)context.SemanticHelper.GetSymbol(invocation)!;
        if (IsCombine(method)) return Combine(invocation, method, context);

        var self = JsExpr.Identifier("this");
        if (invocation.Expression is MemberAccessExpressionSyntax { Expression: BaseExpressionSyntax })
            return Base(method, self);

        if (invocation.Expression is not MemberAccessExpressionSyntax access)
            return JsExpr.Call(JsExpr.Identifier(Eq.Hash), self);
        var receiver = context.Converter.ConvertIr(access.Expression);
        return context.SemanticHelper.GetType(access.Expression) switch
        {
            IArrayTypeSymbol => JsExpr.Call(JsExpr.Identifier(Eq.HashIdentity), receiver),
            { IsValueType: true } => JsExpr.Call(JsExpr.Identifier(Eq.Hash), receiver),
            _ => JsExpr.Call(JsExpr.Identifier(Eq.HashInstance), receiver),
        };
    }

    /// <summary>
    /// <c>base.GetHashCode()</c>: the base's own override, written by the app or synthesized for a
    /// record, whose twin carries it either way; ValueType's, a struct's members, without calling the
    /// override back; and object's, the identity's, otherwise.
    /// </summary>
    private static JsExpr Base(IMethodSymbol method, JsExpr self)
    {
        if (method.ContainingType.Locations.Any(location => location.IsInSource))
            return JsExpr.Call(JsExpr.Member(JsExpr.Identifier("super"), "getHashCode"));
        return method.ContainingType.SpecialType == SpecialType.System_ValueType
            ? JsExpr.Call(JsExpr.Identifier(Eq.HashFields), self)
            : JsExpr.Call(JsExpr.Identifier(Eq.HashIdentity), self);
    }

    /// <summary>
    /// <c>HashCode.Combine</c>, its values in their PARAMETERS' order: <c>Combine(value2: 2, value1: 1)</c>
    /// hashes as <c>Combine(1, 2)</c> does. The arguments are still evaluated as written, which the
    /// template writer keeps when the holes do not follow it.
    /// </summary>
    private static JsExpr Combine(InvocationExpressionSyntax invocation, IMethodSymbol method, ConversionContext context)
    {
        var arguments = invocation.ArgumentList.Arguments;
        var holes = string.Join(", ", Enumerable.Range(0, arguments.Count).Select(slot => $"{{{slot}}}"));
        var template = PrimitiveStaticStrategy.BindNamedArguments($"{Eq.HashCombine}({holes})", invocation, method);
        var parts = arguments.Select(argument => context.Converter.ConvertIr(argument.Expression)).ToArray();
        return JsExpr.Template(template, parts, context.TypeAnnotations);
    }

    /// <summary>
    /// The method group: a delegate over the receiver as it is when the delegate is made, refused
    /// there when the receiver is null, as .NET refuses it, an empty <c>Nullable&lt;T&gt;</c> included
    /// (it boxes to null). An array's answers its identity's.
    /// </summary>
    private static JsExpr Group(MemberAccessExpressionSyntax group, ConversionContext context)
    {
        var receiver = group.Expression is BaseExpressionSyntax
            ? JsExpr.Identifier("this")
            : context.Converter.ConvertIr(group.Expression);
        return context.SemanticHelper.GetType(group.Expression) is IArrayTypeSymbol
            ? JsExpr.Call(JsExpr.Identifier(Eq.HashGroup), receiver, JsExpr.Literal("true"))
            : JsExpr.Call(JsExpr.Identifier(Eq.HashGroup), receiver);
    }

    /// <summary>Whether the member access is the method an invocation calls, rather than a group.</summary>
    private static bool IsCalled(MemberAccessExpressionSyntax access) =>
        access.Parent is InvocationExpressionSyntax invocation && invocation.Expression == access;

    /// <summary>An instance <c>GetHashCode()</c>, object's or any override of it.</summary>
    private static bool IsGetHashCode(IMethodSymbol method) =>
        method is { Name: "GetHashCode", IsStatic: false, Parameters.Length: 0 };

    /// <summary><c>System.HashCode.Combine</c>, of any arity.</summary>
    private static bool IsCombine(IMethodSymbol method) =>
        method is { Name: "Combine", IsStatic: true, ContainingType: { Name: "HashCode" } type }
        && type.ContainingNamespace?.ToDisplayString() == "System";

    public int Priority => 12;
}
