using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Invocation;

/// <summary>
/// Strategy for Console method invocations.
/// Handles: Console.WriteLine, Console.Write → console.log
/// </summary>
public class ConsoleStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        var methodName = memberAccess.Name.Identifier.Text;
        if (methodName is not ("WriteLine" or "Write"))
            return false;

        // Check via semantic model if available
        var symbol = context.SemanticHelper.GetSymbol(invocation) as IMethodSymbol;
        if (symbol != null)
        {
            return context.SemanticHelper.IsSystemConsole(symbol.ContainingType);
        }

        // Fallback: check expression text
        var callerText = memberAccess.Expression.ToString();
        return callerText is "Console" or "console" or "System.Console";
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        
        var args = string.Join(", ", 
            invocation.ArgumentList.Arguments.Select(a => 
                context.Converter.ConvertExpression(a.Expression)));
        
        return $"console.log({args})";
    }

    public int Priority => 10; // Higher than InvocationStrategy (1)
}
