using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Maps element access on a value tuple to an array index, since tuples are emitted as JS arrays.
/// Handles both the positional accessor (<c>t.Item1</c> → <c>t[0]</c>) and declared element names
/// (<c>(int X, int Y) p; p.X</c> → <c>p[0]</c>). Gated on a tuple receiver whose member resolves to an
/// element; anything else (e.g. <c>t.ToString()</c>) falls through to the default member access.
/// Priority 12 — above the general <see cref="Expressions.MemberAccessStrategy"/>.
/// </summary>
public class TupleMemberAccessStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not MemberAccessExpressionSyntax ma) return false;
        var type = context.SemanticHelper.GetType(ma.Expression);
        return type.TupleElementIndex(ma.Name.Identifier.Text) >= 0;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var ma = (MemberAccessExpressionSyntax)node;
        var type = context.SemanticHelper.GetType(ma.Expression);
        var index = type.TupleElementIndex(ma.Name.Identifier.Text);
        var receiver = context.Converter.ConvertExpression(ma.Expression);
        return $"{receiver}[{index}]";
    }

    public int Priority => 12;
}
