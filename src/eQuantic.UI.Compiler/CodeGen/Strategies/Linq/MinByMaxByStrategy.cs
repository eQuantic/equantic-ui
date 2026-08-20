using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Converts LINQ .MinBy(selector) / .MaxBy(selector) to a reduce that returns the element with the
/// smallest/largest key. Ties keep the first element (strict comparison), matching .NET.
/// </summary>
public class MinByMaxByStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;

        var name = memberAccess.Name.Identifier.Text;
        if (name != "MinBy" && name != "MaxBy") return false;

        var symbol = context.SemanticHelper.GetSymbol(invocation);
        if (symbol is IMethodSymbol ms && context.SemanticHelper.IsLinqExtension(ms.ContainingType)) return true;
        return symbol == null && context.CanGuess(node);
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var name = memberAccess.Name.Identifier.Text;
        var caller = context.Converter.ConvertExpression(memberAccess.Expression);
        var args = invocation.ArgumentList.Arguments;
        if (args.Count == 0) return caller;

        var selector = context.Converter.ConvertExpression(args[0].Expression);
        var op = name == "MaxBy" ? ">" : "<";
        // reduce with no seed → first element is the initial accumulator; strict op keeps first on ties.
        return $"{caller}.reduce((_a, _b) => (({selector})(_b) {op} ({selector})(_a) ? _b : _a))";
    }

    public int Priority => 10;
}
