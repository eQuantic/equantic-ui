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

        // Fallback
        if (context.SemanticModel == null)
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
        
        if (args.Count > 0)
        {
            // First(predicate) -> find(predicate)
            var predicate = context.Converter.ConvertExpression(args[0].Expression);
            return $"{caller}.find({predicate})";
        }
        else
        {
            // First() -> [0]
            // Note: In C#, First() throws if empty, but JS array access returns undefined (like FirstOrDefault)
            // This is an acceptable divergence for transpilation unless strict emulation is required.
            return $"{caller}[0]"; 
        }
    }

    public int Priority => 10;
}
