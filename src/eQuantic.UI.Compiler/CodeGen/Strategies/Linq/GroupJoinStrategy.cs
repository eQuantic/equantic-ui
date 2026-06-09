using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Strategy for LINQ <c>GroupJoin</c> (left/group join):
/// <c>outer.GroupJoin(inner, outerKeySelector, innerKeySelector, resultSelector)</c> where
/// <c>resultSelector</c> is <c>(outer, IEnumerable&lt;inner&gt; group) => …</c>. Every outer element
/// appears exactly once, paired with the (possibly empty) array of inner elements sharing its key.
/// Inner is bucketed by key into a Map (primitive/string/enum keys — the common case).
/// </summary>
public class GroupJoinStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return context.SemanticHelper.IsLinqMethod(node, "GroupJoin");
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var outer = context.Converter.ConvertExpression(memberAccess.Expression);
        var args = invocation.ArgumentList.Arguments;
        if (args.Count < 4) return outer;

        var inner = context.Converter.ConvertExpression(args[0].Expression);
        var outerKey = context.Converter.ConvertExpression(args[1].Expression);
        var innerKey = context.Converter.ConvertExpression(args[2].Expression);
        var result = context.Converter.ConvertExpression(args[3].Expression);

        return
            "(() => { " +
            $"const _m = new Map(); for (const _x of {inner}) {{ const _k = ({innerKey})(_x); " +
            "let _g = _m.get(_k); if (!_g) _m.set(_k, _g = []); _g.push(_x); } " +
            $"return {outer}.map(_y => ({result})(_y, _m.get(({outerKey})(_y)) ?? [])); }})()";
    }

    public int Priority => 10;
}
