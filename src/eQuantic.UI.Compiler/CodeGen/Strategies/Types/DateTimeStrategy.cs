using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Strategy for DateTime and DateTimeOffset.
/// Handles: 
/// - DateTime.Now -> new Date()
/// - DateTime.UtcNow -> new Date()
/// - date.Year -> date.getFullYear()
/// </summary>
public class DateTimeStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not MemberAccessExpressionSyntax memberAccess) return false;
        
        var symbol = context.SemanticHelper.GetSymbol(node);
        if (symbol != null) 
        {
            var type = symbol.ContainingType?.ToDisplayString();
            return type == "System.DateTime" || type == "System.DateTimeOffset";
        }

        // Heuristic: Check if accessing static member of known types
        var expressionStr = memberAccess.Expression.ToString();
        return expressionStr == "DateTime" || expressionStr == "DateTimeOffset";
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)node;
        var name = memberAccess.Name.Identifier.Text;
        
        // Static access: DateTime.Now
        // Heuristic: if expression is literally "DateTime"
        if (context.SemanticHelper.IsStatic(node) || memberAccess.Expression.ToString() == "DateTime")
        {
             return name switch
             {
                 "Now" => "new Date()",
                 "UtcNow" => "new Date()", // JS Date is UTC/Local hybrid
                 "Today" => "new Date()",
                 _ => $"Date.{ToCamelCase(name)}"
             };
        }
        
        // Instance access: date.Year
        var expr = context.Converter.ConvertExpression(memberAccess.Expression);
        return name switch
        {
            "Year" => $"{expr}.getFullYear()",
            "Month" => $"({expr}.getMonth() + 1)", // JS Month is 0-indexed
            "Day" => $"{expr}.getDate()",
            "Hour" => $"{expr}.getHours()",
            "Minute" => $"{expr}.getMinutes()",
            "Second" => $"{expr}.getSeconds()",
            "Millisecond" => $"{expr}.getMilliseconds()",
            "DayOfWeek" => $"{expr}.getDay()",
            _ => $"{expr}.{ToCamelCase(name)}"
        };
    }
    
    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    public int Priority => 10;
}
