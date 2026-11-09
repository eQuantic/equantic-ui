using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;
using eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// <c>a.GetType() == b.GetType()</c>, <c>!=</c>, and either one against <c>typeof(X)</c>, as the runtime's
/// <c>$eq.types</c> (<c>utils/runtime-types.ts</c>) compares them (#1202): Flutter's <c>canUpdate</c>, which
/// the element engine asks of every child it reconciles. <c>GetType()</c> is fenced
/// (<see cref="PlatformMemberRead"/>) because the browser cannot answer it for every value, and it stays
/// fenced everywhere but here, where the receiver's static type says whether the browser can tell its
/// runtime type apart.
/// <para>
/// It can for a value whose twin is a class of its own (<see cref="PatternConverter.IsAJsClass"/>), by its
/// prototype and the type arguments it was built with (<see cref="ObjectCreationStrategy.ClosureOf"/>), and
/// for an exception, by the .NET type it carries. It cannot for a value the browser holds as a primitive or
/// a shared prototype, whatever .NET type it is (a number, a string, an enum, an array, a tuple, a
/// delegate), nor for one whose static type admits such a value (<c>object</c>, an interface, a type
/// parameter with no class constraint that qualifies): those are refused (EQ2004), where the fence refused
/// every one.
/// </para>
/// </summary>
internal sealed class RuntimeTypeEqualityStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not BinaryExpressionSyntax binary
            || !(binary.IsKind(SyntaxKind.EqualsExpression) || binary.IsKind(SyntaxKind.NotEqualsExpression)))
            return false;
        var left = GetTypeCall(binary.Left, context) is not null;
        var right = GetTypeCall(binary.Right, context) is not null;
        return (left || right)
            && (left || Bare(binary.Left) is TypeOfExpressionSyntax)
            && (right || Bare(binary.Right) is TypeOfExpressionSyntax);
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var binary = (BinaryExpressionSyntax)node;
        // Each side is refused on its own, so a comparison of two that do not qualify says both.
        var left = RuntimeType(binary.Left, context);
        var right = RuntimeType(binary.Right, context);
        if (left is null || right is null) return JsExpr.Opaque("false");
        context.UsedHelpers.Add(Eq.Import);
        var same = JsExpr.Call(JsExpr.Identifier(Eq.TypesSame), left, right);
        return binary.IsKind(SyntaxKind.NotEqualsExpression) ? JsExpr.Prefix("!", same) : same;
    }

    /// <summary>
    /// <c>object.GetType()</c> called on a receiver or on the instance, its parentheses looked through, and
    /// <c>Exception.GetType()</c>, which hides it to answer the same runtime type; null for any other
    /// expression, a type of the app's own <c>GetType</c> that hides it included.
    /// </summary>
    private static InvocationExpressionSyntax? GetTypeCall(ExpressionSyntax expression, ConversionContext context) =>
        Bare(expression) is InvocationExpressionSyntax { ArgumentList.Arguments.Count: 0 } call
        && call.Expression is MemberAccessExpressionSyntax or IdentifierNameSyntax
        && context.SemanticHelper.GetSymbol(call) is IMethodSymbol { Name: "GetType", IsStatic: false, ContainingType: { } declaring }
        && (declaring.SpecialType == SpecialType.System_Object || declaring.IsNamed("System.Exception"))
            ? call
            : null;

    private static ExpressionSyntax Bare(ExpressionSyntax expression) =>
        expression is ParenthesizedExpressionSyntax parenthesized ? Bare(parenthesized.Expression) : expression;

    /// <summary>One side as the runtime type it is, or null where it was refused.</summary>
    private static JsExpr? RuntimeType(ExpressionSyntax side, ConversionContext context)
    {
        if (GetTypeCall(side, context) is { } call)
        {
            // `GetType()` is the instance's, and `base.GetType()` too: it is not virtual.
            var receiver = call.Expression is MemberAccessExpressionSyntax { Expression: not BaseExpressionSyntax } access
                ? access.Expression
                : null;
            var type = receiver is null
                ? context.SemanticHelper.GetSymbol(call) is not null ? EnclosingType(call, context) : null
                : context.SemanticHelper.GetType(receiver);
            if (Unknowable(type) is { } reason) return Refused(call, type, reason, context);
            context.UsedHelpers.Add(Eq.Import);
            return JsExpr.Call(JsExpr.Identifier(Eq.TypesOf),
                receiver is null ? JsExpr.Identifier("this") : context.Converter.ConvertIr(receiver));
        }
        var typeOf = (TypeOfExpressionSyntax)Bare(side);
        var named = context.SemanticHelper.GetSymbol(typeOf.Type) as INamedTypeSymbol;
        if (named is null || named.IsUnboundGenericType || ObjectCreationStrategy.ContainsTypeParameter(named))
            return Refused(typeOf, named, "its type arguments are known only at run time", context);
        if (ExceptionTypes.Is(named)) return JsExpr.Literal(JsStringLiteral.Quote(ExceptionTypes.NameOf(named)));
        if (!PatternConverter.IsAJsClass(named))
            return Refused(typeOf, named, "no value the browser holds is told apart as one of it", context);
        if (named.ReportIfKeptOut(typeOf, context)) return null;
        var twin = JsExpr.Identifier(named.IntroduceTwin(context));
        context.UsedHelpers.Add(Eq.Import);
        return ObjectCreationStrategy.ClosureOf(named) is { } closure
            ? JsExpr.Call(JsExpr.Identifier(Eq.TypesNamed), twin, JsExpr.Literal(JsStringLiteral.Quote(closure)))
            : JsExpr.Call(JsExpr.Identifier(Eq.TypesNamed), twin);
    }

    private static ITypeSymbol? EnclosingType(SyntaxNode node, ConversionContext context) =>
        node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault() is { } declaration
            ? context.SemanticHelper.GetDeclaredSymbol(declaration) as ITypeSymbol
            : null;

    /// <summary>
    /// Why the browser cannot tell the runtime type of a value of <paramref name="type"/> apart, or null where
    /// it can: an exception; a class or a struct whose twin is a class of its own, the vocabulary's roots
    /// included; a <c>Nullable&lt;T&gt;</c> of such a struct, which boxes to it; a type parameter through a
    /// class constraint that qualifies.
    /// </summary>
    private static string? Unknowable(ITypeSymbol? type)
    {
        switch (type.UnwrapNullable() ?? type)
        {
            case null:
                return "its type is not known";
            case ITypeParameterSymbol parameter:
                return parameter.ConstraintTypes.Any(constraint => Unknowable(constraint) is null)
                    ? null
                    : "it may be held as a value whose type the browser does not keep";
            case INamedTypeSymbol named when ExceptionTypes.Is(named) || IsVocabularyRoot(named)
                || named.TypeKind is TypeKind.Class or TypeKind.Struct && PatternConverter.IsAJsClass(named):
                return null;
            default:
                return "the browser holds a value of it as one whose type it does not keep";
        }
    }

    /// <summary>The vocabulary's roots, whose twins are classes as every type derived from them is.</summary>
    private static bool IsVocabularyRoot(INamedTypeSymbol type) =>
        type.Name is "VisualNode" or "UiComponent"
        && type.ContainingNamespace?.ToDisplayString() == "eQuantic.UI.Primitives";

    private static JsExpr? Refused(SyntaxNode node, ITypeSymbol? type, string reason, ConversionContext context)
    {
        context.Report(node, ConversionSeverity.Error, "EQ2004",
            $"'{node}' has no JavaScript translation as a runtime type of {type?.ToDisplayString() ?? "an unknown type"}: "
            + $"{reason}. A runtime type is compared in the browser for an exception and for a class or a "
            + "struct whose values it builds; test the value with a type pattern (`x is T`) instead.");
        return null;
    }

    public int Priority => 30;
}
