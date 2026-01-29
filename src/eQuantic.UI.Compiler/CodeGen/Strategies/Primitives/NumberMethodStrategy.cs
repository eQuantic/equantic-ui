using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;

/// <summary>
/// Strategy for Number methods (int.Parse, double.TryParse, etc).
/// Handles:
/// - int.Parse(s) -> parseInt(s)
/// - double.Parse(s) -> parseFloat(s)
/// - int.TryParse(s, out var x) -> x = parseInt(s); return !isNaN(x)
/// </summary>
public class NumberMethodStrategy : IConversionStrategy
{
    private static readonly HashSet<string> Types = new() { "int", "Int32", "double", "Double", "float", "Single", "decimal", "Decimal", "long", "Int64" };

    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;
        
        var type = memberAccess.Expression.ToString();
        var name = memberAccess.Name.Identifier.Text;
        
        if (!Types.Contains(type)) return false;
        
        return name is "Parse" or "TryParse";        
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var type = memberAccess.Expression.ToString();
        var name = memberAccess.Name.Identifier.Text;
        var args = invocation.ArgumentList.Arguments;
        
        string parsMethod = (type == "int" || type == "Int32" || type == "long" || type == "Int64") 
            ? "parseInt" 
            : "parseFloat";

        if (name == "Parse")
        {
            var input = context.Converter.ConvertExpression(args[0].Expression);
            return $"{parsMethod}({input})";
        }
        
        if (name == "TryParse")
        {
            // int.TryParse(s, out var result)
            // Transform to IIFE: (() => { result = parseInt(s); return !isNaN(result); })()
            // BUT: This updates a local variable 'result'. 
            // If the argument is `out var result` (DeclarationExpression), we need to handle scope.
            // If it's `out result` (IdentifierName), we assign to it.
            
            if (args.Count < 2) return "false";
            
            var input = context.Converter.ConvertExpression(args[0].Expression);
            var outArg = args[1];
            
            string varName = "";
            bool isDeclaration = false;
            
            if (outArg.Expression is DeclarationExpressionSyntax decl)
            {
                if (decl.Designation is SingleVariableDesignationSyntax single)
                {
                    varName = single.Identifier.Text;
                    isDeclaration = true;
                }
            }
            else
            {
                varName = context.Converter.ConvertExpression(outArg.Expression);
            }
            
            // Note: In strict JS logic, assignment relies on variable being available. 
            // If it's `out var x`, `x` is hoisted in C# scope. In JS `var` is hoisted too, but let isn't.
            // We'll trust LocalDeclarationStrategy or standard var usage handled elsewhere if verified.
            // For now, simpler: assume variable exists or is created.
            
            return $"({varName} = {parsMethod}({input}), !isNaN({varName}))";
        }
        
        return node.ToString();
    }

    public int Priority => 10;
}
