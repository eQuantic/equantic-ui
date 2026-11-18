using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Extensions;

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
        return context.IsLinqMethod(node, "OfType");
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        // Extract the generic type argument if available
        if (memberAccess.Name is GenericNameSyntax genericName
            && genericName.TypeArgumentList.Arguments.FirstOrDefault() is { } typeArg)
        {
            var typeName = typeArg.ValueText();

            // For primitive types, use typeof; for reference types, use instanceof
            var test = IsPrimitiveType(typeName) ? $"typeof $x === '{MapToJsType(typeName)}'" : $"$x instanceof {typeName}";

            // The filter runs when the query is walked, over the source as it is then (#685).
            return Ir.JsExprWriter.Write(Ir.JsExpr.Template(
                LinqSource.Defer($"{{0}}.filter(($x) => {test})", memberAccess.Expression, context),
                [LinqSource.Lazy(memberAccess.Expression, context)]));
        }

        // Fallback: just return source (no filtering)
        return LinqSource.Text(memberAccess.Expression, context);
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
