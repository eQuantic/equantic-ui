using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Maps <c>System.Text.StringBuilder</c> to the runtime <c>StringBuilder</c> compat type.
/// <c>new StringBuilder(...)</c> becomes the <c>stringBuilder(...)</c> factory; instance methods
/// (<c>Append</c>, <c>AppendLine</c>, <c>Insert</c>, <c>Remove</c>, <c>Replace</c>, <c>Clear</c>,
/// <c>ToString</c>) and <c>Length</c> become their camelCase equivalents on the value, which take each
/// overload by its count of arguments. The <c>char[]</c> overloads of <c>Append</c> and <c>Insert</c>
/// are named for what they are (<c>appendChars</c>, <c>insertChars</c>): the runtime cannot tell a null
/// array from a null string, and .NET refuses the two in different words (#650).
/// </summary>
/// <remarks>
/// Priority 15 so it wins over the generic ToString (10), ObjectCreation (5) and member-access (0)
/// strategies for StringBuilder nodes. Gated on the semantic type (falls back to the type name when no
/// semantic model is present).
/// </remarks>
public class StringBuilderStrategy : ConversionStrategyBase
{
    private const string TypeName = "System.Text.StringBuilder";

    public override bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        switch (node)
        {
            case BaseObjectCreationExpressionSyntax oc:
                return IsType(context.SemanticHelper.GetType(oc))
                    || (oc is ObjectCreationExpressionSyntax named && named.Type.ToString() == "StringBuilder");

            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma }:
                return IsMember(ma, context);

            case MemberAccessExpressionSyntax member:
                return IsMember(member, context);

            default:
                return false;
        }
    }

    public override string Convert(SyntaxNode node, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        switch (node)
        {
            case BaseObjectCreationExpressionSyntax oc:
                return $"{Eq.StringBuilder}({ConvertArgs(oc.ArgumentList, context)})";

            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma } inv:
            {
                var name = ma.Name.Identifier.Text;
                var runtimeName = RuntimeName(context.SemanticHelper.GetSymbol(ma) as IMethodSymbol, name);
                // The VALUE `Append`, `AppendLine` and `Insert` write is its ToString, in the culture in
                // force: what a concatenation writes (StringConversion), a number in the culture's
                // symbols (#454), a bool as True, an enum as its name, a null as nothing. It is the
                // argument the bound tree binds to the `value` parameter, written by position or by
                // name, and the call passes every argument where C# binds it, each evaluated in the
                // order it is written. The runtime's builder took JavaScript's String() of it, so
                // `Append(1.5)` read `1.5` on a pt-BR page. A value that is text already passes as it
                // is (IsText), a null included, which a ranged overload refuses as .NET does (#650).
                if (name is "Append" or "AppendLine" or "Insert"
                    && context.SemanticHelper.GetOperation(inv) is IInvocationOperation operation
                    && operation.Arguments.FirstOrDefault(argument => argument.Parameter is { Name: "value" } parameter
                        && !IsText(parameter.Type) && argument.ArgumentKind == ArgumentKind.Explicit)?.Value.Syntax is ExpressionSyntax value
                    && BoundArguments.Of(operation, argument => argument == value
                        ? StringConversion.ToDotNetString(argument, context.Converter.ConvertIr(argument), context)
                        : context.Converter.ConvertIr(argument)) is { } bound)
                {
                    return Ir.JsExprWriter.Write(bound.Call(context.Converter.ConvertIr(ma.Expression),
                        runtimeName, context.TypeAnnotations));
                }
                var receiver = context.Converter.ConvertExpression(ma.Expression);
                return $"{receiver}.{runtimeName}({ConvertArgs(inv.ArgumentList, context)})";
            }

            case MemberAccessExpressionSyntax member:
            {
                var receiver = context.Converter.ConvertExpression(member.Expression);
                return $"{receiver}.{member.Name.Identifier.Text.ToCamelCase()}";
            }

            default:
                return context.Unhandled(node, "StringBuilder");
        }
    }

    /// <summary>The runtime method an overload is: its name in camelCase, and the <c>char[]</c>
    /// overloads of <c>Append</c> and <c>Insert</c> as their own method.</summary>
    private static string RuntimeName(IMethodSymbol? method, string name) =>
        method is { Name: "Append" or "Insert" }
            && method.Parameters.Any(parameter => parameter is { Name: "value", Type: IArrayTypeSymbol })
            ? $"{name.ToCamelCase()}Chars"
            : name.ToCamelCase();

    /// <summary>
    /// Whether a builder's <c>value</c> is text already, written as it is: a string, a char, a char[],
    /// another builder or a span of chars. Only the rest (a number, a bool, an enum, an object) is a
    /// value whose text the culture writes. A null string or builder handed to a ranged overload is a
    /// refusal .NET makes, which the text conversion turned into an empty string.
    /// </summary>
    private static bool IsText(ITypeSymbol type) =>
        type.SpecialType is SpecialType.System_String or SpecialType.System_Char
        || type is IArrayTypeSymbol
        || type is INamedTypeSymbol { Name: "StringBuilder", ContainingNamespace: { Name: "Text", ContainingNamespace.Name: "System" } }
        || type is INamedTypeSymbol { Name: "ReadOnlySpan" or "ReadOnlyMemory", TypeArguments: [{ SpecialType: SpecialType.System_Char }] };

    private static bool IsMember(MemberAccessExpressionSyntax ma, ConversionContext context)
    {
        var symbol = context.SemanticHelper.GetSymbol(ma);
        if (symbol?.ContainingType != null)
            return symbol.ContainingType.ToDisplayString() == TypeName;

        return IsType(context.SemanticHelper.GetType(ma.Expression));
    }

    private static bool IsType(ITypeSymbol? type) => type?.ToDisplayString() == TypeName;

    public override int Priority => 15;
}
