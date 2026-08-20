using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Strategy for LINQ <c>Join</c> (inner/equi-join):
/// <c>outer.Join(inner, outerKeySelector, innerKeySelector, resultSelector)</c>.
/// Emits an order-preserving hash join: inner is bucketed by key into a Map, then each outer element
/// (in order) is paired with every matching inner element (in order), yielding
/// <c>resultSelector(outer, inner)</c>. Keys are compared via Map (works for primitive/string/enum
/// keys — the common case; record/struct keys would use reference equality).
/// </summary>
public class JoinStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return context.IsLinqMethod(node, "Join");
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
            $"const _r = []; for (const _y of {outer}) {{ const _g = _m.get(({outerKey})(_y)); " +
            $"if (_g) for (const _z of _g) _r.push(({result})(_y, _z)); }} return _r; }})()";
    }

    public int Priority => 10;
}
