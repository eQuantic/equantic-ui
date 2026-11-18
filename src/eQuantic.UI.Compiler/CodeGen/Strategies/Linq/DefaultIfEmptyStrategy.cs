using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

public class DefaultIfEmptyStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;
        if (memberAccess.Name.Identifier.ValueText != "DefaultIfEmpty") return false;

        var symbol = context.SemanticHelper.GetSymbol(invocation);
        if (symbol is IMethodSymbol ms && context.SemanticHelper.IsLinqExtension(ms.ContainingType)) return true;

        return symbol == null && context.CanGuess(node);
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var args = invocation.ArgumentList.Arguments;

        // DefaultIfEmpty(val) -> source.length > 0 ? source : [val]
        // DefaultIfEmpty() -> source.length > 0 ? source : [null] (or default)
        
        // With no argument the filler is the ELEMENT's default, which for a value type is not null.
        var defaultVal = args.Count > 0
            ? context.Converter.ConvertExpression(args[0].Expression)
            : DefaultValue.OfElement(context.SemanticHelper.GetType(memberAccess.Expression), context);

        // Asked when the query is walked, of the source as it is then (#685); the filler is evaluated when
        // the operator is called, as C# evaluates an argument.
        return JsExprWriter.Write(JsExpr.Template(
            LinqSource.Defer("({0}.length > 0 ? {0} : [{1}])", memberAccess.Expression, context),
            [LinqSource.Lazy(memberAccess.Expression, context), JsExpr.Opaque(defaultVal)]));
    }

    public int Priority => 10;
}
