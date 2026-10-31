using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

public class ElementAtStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;
        if (memberAccess.Name.Identifier.ValueText != "ElementAt" && memberAccess.Name.Identifier.ValueText != "ElementAtOrDefault") return false;

        var symbol = context.SemanticHelper.GetSymbol(invocation);
        if (symbol is IMethodSymbol ms && context.SemanticHelper.IsLinqExtension(ms.ContainingType)) return true;

        return symbol == null && context.CanGuess(node);
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var args = invocation.ArgumentList.Arguments;

        // Over a query the runtime walks lazily, the walk stops at the element (#685).
        if (args.Count == 1 && LinqSource.IsQuery(memberAccess.Expression, context))
        {
            context.UsedHelpers.Add(Eq.Import);
            var query = JsExprWriter.Write(LinqSource.Lazy(memberAccess.Expression, context));
            var at = context.Converter.ConvertExpression(args[0].Expression);
            return memberAccess.Name.Identifier.ValueText != "ElementAtOrDefault"
                ? $"{Eq.Query}.elementAt({query}, {at})"
                : $"{Eq.Query}.elementAt({query}, {at}, {{ value: {DefaultValue.OfElement(context.SemanticHelper.GetType(memberAccess.Expression), context)} }})";
        }

        var caller = LinqSource.Text(memberAccess.Expression, context);

        if (args.Count == 1)
        {
            var index = context.Converter.ConvertExpression(args[0].Expression);
            // ElementAt(i) is source[i]; ElementAtOrDefault(i) answers the ELEMENT's default when
            // the index is out of range, where a bare lookup would hand back undefined.
            if (memberAccess.Name.Identifier.ValueText != "ElementAtOrDefault") return $"{caller}[{index}]";
            var fallback = DefaultValue.OfElement(context.SemanticHelper.GetType(memberAccess.Expression), context);
            return $"({caller}[{index}] ?? {fallback})";
        }

        return caller;
    }

    public int Priority => 10;
}
