using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Strategy for OfType LINQ method.
/// Handles: source.OfType&lt;T&gt;() -> source.filter(x => x instanceof T || typeof x === 'type')
/// Filters elements by type at runtime.
/// </summary>
public class OfTypeStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return context.SemanticHelper.IsLinqMethod(node, "OfType");
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var source = context.Converter.ConvertExpression(memberAccess.Expression);

        // Extract the generic type argument if available
        if (memberAccess.Name is GenericNameSyntax genericName)
        {
            var typeArg = genericName.TypeArgumentList.Arguments.FirstOrDefault();
            if (typeArg != null)
            {
                var typeName = typeArg.ToString();

                // For primitive types, use typeof
                if (IsPrimitiveType(typeName))
                {
                    var jsType = MapToJsType(typeName);
                    return $"{source}.filter(x => typeof x === '{jsType}')";
                }

                // For reference types, use instanceof
                return $"{source}.filter(x => x instanceof {typeName})";
            }
        }

        // Fallback: just return source (no filtering)
        return source;
    }

    private bool IsPrimitiveType(string typeName)
    {
        return typeName switch
        {
            "string" or "String" => true,
            "number" or "int" or "double" or "float" or "decimal" or "long" => true,
            "boolean" or "bool" => true,
            _ => false
        };
    }

    private string MapToJsType(string typeName)
    {
        return typeName switch
        {
            "string" or "String" => "string",
            "int" or "double" or "float" or "decimal" or "long" or "number" => "number",
            "bool" or "boolean" => "boolean",
            _ => "object"
        };
    }

    public int Priority => 10;
}
