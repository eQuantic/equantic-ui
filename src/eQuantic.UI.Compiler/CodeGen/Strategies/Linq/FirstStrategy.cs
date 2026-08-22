using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Converts LINQ .First()/.FirstOrDefault() to JavaScript.
/// - First() -> [0]
/// - First(predicate) -> find(predicate)
/// </summary>
public class FirstStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        var name = memberAccess.Name.Identifier.Text;
        if (name != "First" && name != "FirstOrDefault")
            return false;

        // Semantic Check
        var symbol = context.SemanticHelper.GetSymbol(invocation);
        if (symbol is IMethodSymbol ms && context.SemanticHelper.IsLinqExtension(ms.ContainingType))
        {
            return true;
        }

        // Name decides ONLY where guessing is honest — see ConversionContext.CanGuess. Under an
        // AUTHORITATIVE model, in-tree-but-unbindable is reported (EQ2006), never guessed.
        if (symbol == null && context.CanGuess(node))
        {
            return true;
        }

        return false;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        
        var caller = context.Converter.ConvertExpression(memberAccess.Expression);
        var args = invocation.ArgumentList.Arguments;
        
        // FirstOrDefault answers the ELEMENT's default when nothing matches — 0 for an int
        // sequence, not null (DefaultValue). First() keeps the bare lookup: C# throws on an empty
        // sequence and this hands back undefined, a divergence documented here since before the
        // conformance suite existed and left as it was.
        var orDefault = memberAccess.Name.Identifier.Text == "FirstOrDefault"
            ? $" ?? {DefaultValue.OfElement(context.SemanticHelper.GetType(memberAccess.Expression), context)}"
            : "";

        if (args.Count > 0)
        {
            // First(predicate) -> find(predicate)
            var predicate = context.Converter.ConvertExpression(args[0].Expression);
            return orDefault.Length > 0
                ? $"({caller}.find({predicate}){orDefault})"
                : $"{caller}.find({predicate})";
        }

        return orDefault.Length > 0 ? $"({caller}[0]{orDefault})" : $"{caller}[0]";
    }

    public int Priority => 10;
}
