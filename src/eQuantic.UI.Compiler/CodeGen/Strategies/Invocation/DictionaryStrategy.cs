using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Invocation;

/// <summary>
/// Strategy for Dictionary method invocations.
/// Handles:
/// - ContainsKey(key) → key in obj
/// - TryGetValue(key, out var val) → (val = obj[key]) !== undefined
/// </summary>
public class DictionaryStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        var methodName = memberAccess.Name.Identifier.Text;
        if (methodName is not ("ContainsKey" or "TryGetValue" or "TryGetValueOrDefault"))
            return false;

        // Check via semantic model if available
        var symbol = context.SemanticHelper.GetSymbol(invocation) as IMethodSymbol;
        if (symbol != null)
        {
            var containingType = symbol.ContainingType.ToDisplayString();
            return containingType.Contains("Dictionary") || 
                   containingType.Contains("IDictionary");
        }

        // Allow fallback for common patterns
        return true;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var methodName = memberAccess.Name.Identifier.Text;
        var caller = context.Converter.ConvertExpression(memberAccess.Expression);
        
        var args = invocation.ArgumentList.Arguments;
        
        // ContainsKey(key) → key in obj
        if (methodName == "ContainsKey" && args.Count > 0)
        {
            var key = context.Converter.ConvertExpression(args[0].Expression);
            return $"{key} in {caller}";
        }
        
        // TryGetValue(key, out var val) → (val = obj[key]) !== undefined
        if ((methodName == "TryGetValue" || methodName == "TryGetValueOrDefault") && args.Count > 1)
        {
            var key = context.Converter.ConvertExpression(args[0].Expression);
            
            // Extract the out variable name
            string outVar;
            var outArg = args[1];
            if (outArg.RefOrOutKeyword.IsKind(SyntaxKind.OutKeyword))
            {
                if (outArg.Expression is DeclarationExpressionSyntax decl)
                {
                    outVar = decl.Designation.ToString();
                }
                else
                {
                    outVar = outArg.Expression.ToString().Trim();
                }
            }
            else
            {
                outVar = context.Converter.ConvertExpression(outArg.Expression);
            }
            
            return $"({outVar} = {caller}[{key}]) !== undefined";
        }
        
        // Fallback
        var argsStr = string.Join(", ", args.Select(a => context.Converter.ConvertExpression(a.Expression)));
        return $"{caller}.{ToCamelCase(methodName)}({argsStr})";
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    public int Priority => 10;
}
