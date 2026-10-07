using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Maps <c>System.Text.StringBuilder</c> to the runtime <c>StringBuilder</c> compat type.
/// <c>new StringBuilder(...)</c> becomes the <c>stringBuilder(...)</c> factory; instance methods
/// (<c>Append</c>, <c>AppendLine</c>, <c>Insert</c>, <c>Remove</c>, <c>Replace</c>, <c>Clear</c>,
/// <c>ToString</c>) and <c>Length</c> become their camelCase equivalents on the value, which take each
/// overload by its count of arguments. The <c>char[]</c> overloads of <c>Append</c> and <c>Insert</c>
/// are named for what they are (<c>appendChars</c>, <c>insertChars</c>): the runtime cannot tell a null
/// array from a null string, and .NET refuses the two in different words (#650).
/// <para>
/// <c>AppendFormat</c> and <c>AppendJoin</c> append what <c>string.Format</c> and <c>string.Join</c>
/// write, bound by the same parameters and under the same culture policy; <c>Equals(StringBuilder)</c>
/// is the runtime's <c>equalsBuilder</c>, since <c>Equals(object)</c> is identity; and the
/// <c>Chars</c> indexer is the twin's <c>item</c> and <c>setItem</c> (<see cref="CarriesIndexer"/>).
/// Each was a member the runtime did not have, a TypeError in the browser behind a green build (#679).
/// </para>
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
                var method = context.SemanticHelper.GetSymbol(ma) as IMethodSymbol;
                if (method is not null && Uncrossable(method) is { } why) return context.Unhandled(node, why);
                var receiver = context.Converter.ConvertExpression(ma.Expression);
                switch (method)
                {
                    case { Name: "AppendFormat" }:
                        return $"{receiver}.append({StringStaticStrategy.FormatCall(inv, inv.ArgumentList.Arguments, context)})";
                    case { Name: "AppendJoin" }:
                        return $"{receiver}.append({StringStaticStrategy.JoinCall(inv, method, context)})";
                    case { Name: "Append" or "AppendLine", Parameters: [{ Type.Name: "IFormatProvider" }, _] }:
                        return Provided(receiver, method, inv, context);
                }
                var name = RuntimeName(method, ma.Name.Identifier.Text);
                return $"{receiver}.{name}({ConvertArgs(inv.ArgumentList, context)})";
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

    /// <summary>The runtime method an overload is: its name in camelCase, the <c>char[]</c>
    /// overloads of <c>Append</c> and <c>Insert</c> as their own method, and <c>Equals(StringBuilder)</c>
    /// as <c>equalsBuilder</c>.</summary>
    private static string RuntimeName(IMethodSymbol? method, string name) => method switch
    {
        { Name: "Append" or "Insert" } when method.Parameters.Any(parameter => parameter is { Name: "value", Type: IArrayTypeSymbol })
            => $"{name.ToCamelCase()}Chars",
        { Name: "Equals", Parameters: [{ Type: var other }] } when IsType(other) => "equalsBuilder",
        _ => name.ToCamelCase(),
    };

    /// <summary>
    /// <c>Append(IFormatProvider, $"…")</c> and <c>AppendLine</c>'s twin: the text in the current
    /// culture, which a null provider or <c>CultureInfo.CurrentCulture</c> names. Any other culture is
    /// EQ2108, since an interpolation formats in the app's culture alone. The provider was passed where
    /// the runtime reads the value, so the text was not what was appended.
    /// </summary>
    private static string Provided(string receiver, IMethodSymbol method, InvocationExpressionSyntax invocation,
        ConversionContext context)
    {
        if (ParameterTemplate.Filling(invocation, method, 0)?.Expression is not { } provider
            || ParameterTemplate.Filling(invocation, method, 1)?.Expression is not { } text)
            return context.Unhandled(invocation, "StringBuilder." + method.Name + " with a provider");
        if (!NamedCulture.IsCurrent(provider, context))
        {
            context.Report(invocation, ConversionSeverity.Error, "EQ2108",
                "An interpolated string crosses to JavaScript in the app's culture alone. Pass "
                + "CultureInfo.CurrentCulture or no provider, or append string.Format's text, which takes "
                + "CultureInfo.InvariantCulture too.");
            return "''";
        }
        return $"{receiver}.{method.Name.ToCamelCase()}({context.Converter.ConvertExpression(text)})";
    }

    /// <summary>Why a member cannot cross, or null: <c>GetChunks</c>, whose chunks the browser's
    /// builder does not keep, and an overload that takes a span or a memory, which JavaScript does not
    /// have. A <c>params</c> span is the values written one by one, which .NET 9 binds
    /// <c>AppendJoin(",", "a", "b")</c> to.</summary>
    private static string? Uncrossable(IMethodSymbol method)
    {
        if (method.Name == "GetChunks") return "StringBuilder.GetChunks, whose chunks the browser's builder does not keep";
        return method.Parameters.Any(parameter => !parameter.IsParams
            && parameter.Type is INamedTypeSymbol { Name: "Span" or "ReadOnlySpan" or "Memory" or "ReadOnlyMemory" })
            ? $"StringBuilder.{method.Name} over a span or a memory, which JavaScript does not have"
            : null;
    }

    /// <summary>Whether an indexer is the builder's <c>Chars</c>, which the runtime's builder carries
    /// as <c>item</c> and <c>setItem</c>: read through JavaScript's subscript, <c>sb[0]</c> was a
    /// property no builder had, and a write set one nobody read (#679).</summary>
    internal static bool CarriesIndexer(IPropertySymbol indexer) => IsType(indexer.ContainingType);

    private static bool IsMember(MemberAccessExpressionSyntax ma, ConversionContext context)
    {
        var symbol = context.SemanticHelper.GetSymbol(ma);
        if (symbol?.ContainingType != null)
            return symbol.ContainingType.ToDisplayString() == TypeName;

        return IsType(context.SemanticHelper.GetType(ma.Expression));
    }

    /// <summary>Whether a type is the builder, a nullable reference to one included
    /// (<c>Equals(StringBuilder?)</c>'s parameter).</summary>
    private static bool IsType(ITypeSymbol? type) =>
        type?.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString() == TypeName;

    public override int Priority => 15;
}
