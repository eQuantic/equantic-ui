using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Invocation;

/// <summary>
/// Strategy for IServiceProvider method invocations.
/// Handles:
/// - GetService&lt;T&gt;() → getService('T')
/// - GetRequiredService&lt;T&gt;() → getRequiredService('T')
/// - GetService(typeof(T)) → getService('T')
/// </summary>
public class ServiceProviderStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        var methodName = memberAccess.Name.Identifier.Text;
        if (methodName is not ("GetService" or "GetRequiredService"))
            return false;

        // Check via semantic model if available
        var symbol = context.SemanticHelper.GetSymbol(invocation) as IMethodSymbol;
        if (symbol != null)
        {
            var containingType = symbol.ContainingType.ToDisplayString();
            return containingType.Contains("IServiceProvider") || 
                   containingType.Contains("ServiceProvider");
        }

        // Allow fallback - these method names are specific enough
        return true;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var caller = context.Converter.ConvertExpression(memberAccess.Expression);
        var methodName = memberAccess.Name.Identifier.Text;
        var jsMethodName = methodName == "GetRequiredService" ? "getService" : ToCamelCase(methodName);
        
        // Check if generic: GetService<T>()
        if (memberAccess.Name is GenericNameSyntax genericName && 
            genericName.TypeArgumentList.Arguments.Count > 0)
        {
            var typeName = genericName.TypeArgumentList.Arguments[0].ToString();
            return $"{caller}.{jsMethodName}('{typeName}')";
        }
        
        // Check for typeof(T) argument: GetService(typeof(T))
        var args = invocation.ArgumentList.Arguments;
        if (args.Count > 0)
        {
            var argExpr = args[0].Expression.ToString();
            if (argExpr.StartsWith("typeof("))
            {
                var typeName = argExpr.Substring(7, argExpr.Length - 8);
                return $"{caller}.{jsMethodName}('{typeName}')";
            }
            
            var arg = context.Converter.ConvertExpression(args[0].Expression);
            return $"{caller}.{jsMethodName}({arg})";
        }
        
        return $"{caller}.{jsMethodName}()";
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    public int Priority => 10;
}
