using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Maps element access on a value tuple to an array index, since tuples are emitted as JS arrays.
/// Handles both the positional accessor (<c>t.Item1</c> → <c>t[0]</c>) and declared element names
/// (<c>(int X, int Y) p; p.X</c> → <c>p[0]</c>). Gated on a tuple receiver whose member resolves to an
/// element; anything else (e.g. <c>t.ToString()</c>) falls through to the default member access.
/// A nullable tuple's element is read through <c>?.</c>, whose rebuilt receiver has the nullable's
/// type, so the tuple is read through it: <c>t?.A</c> was <c>t?.a</c>, undefined on the array (#847).
/// Priority 12 — above the general <see cref="Expressions.MemberAccessStrategy"/>.
/// </summary>
public class TupleMemberAccessStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not MemberAccessExpressionSyntax ma) return false;
        return TupleOf(context.SemanticHelper.GetType(ma.Expression)).TupleElementIndex(ma.Name.Identifier.ValueText) >= 0;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var ma = (MemberAccessExpressionSyntax)node;
        var index = TupleOf(context.SemanticHelper.GetType(ma.Expression)).TupleElementIndex(ma.Name.Identifier.ValueText);
        var receiver = context.Converter.ConvertExpression(ma.Expression);
        return $"{receiver}[{index}]";
    }

    /// <summary>The tuple a value of <paramref name="type"/> holds, a nullable's included.</summary>
    private static ITypeSymbol? TupleOf(ITypeSymbol? type) =>
        type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? nullable.TypeArguments[0]
            : type;

    public int Priority => 12;
}
