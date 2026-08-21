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
        
        var name = memberAccess.Name.Identifier.Text;
        if (name is not ("Parse" or "TryParse")) return false;

        return context.ReceiverIsType(memberAccess.Expression,
            named => named.SpecialType is SpecialType.System_Int32 or SpecialType.System_Int64
                or SpecialType.System_Double or SpecialType.System_Single or SpecialType.System_Decimal,
            [.. Types]);
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
            // The `out` is the LAST argument, not the second. `TryParse(s, out var n)` and
            // `TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var n)` are the same
            // method with the same answer, and taking args[1] on the four-argument overload wrote
            // the NUMBER STYLE where the variable belonged: `(511 = parseFloat(value), !isNaN(511))`,
            // which is not even syntax. The styles and the provider have no JS equivalent worth
            // emitting — `parseFloat` is already invariant and permissive — so they are dropped,
            // deliberately, and the value they carried is the one this comment owes you.
            var outArg = args[^1];
            
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

        return context.Unhandled(node, "numeric Parse/TryParse");
    }

    public int Priority => 10;
}
