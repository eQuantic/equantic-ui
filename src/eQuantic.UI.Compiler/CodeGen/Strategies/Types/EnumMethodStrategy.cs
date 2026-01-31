using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Strategy for Enum static methods.
/// Handles:
/// - Enum.Parse&lt;T&gt;(string) -> parseEnum(string, EnumType)
/// - Enum.TryParse&lt;T&gt;(string, out T) -> tryParseEnum(string, EnumType, out result)
/// - Enum.GetValues&lt;T&gt;() -> Object.values(EnumType)
/// - Enum.GetNames&lt;T&gt;() -> Object.keys(EnumType)
/// - Enum.IsDefined(type, value) -> EnumType[value] !== undefined
/// </summary>
public class EnumMethodStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;

        var expr = memberAccess.Expression.ToString();
        var name = memberAccess.Name.Identifier.Text;

        if (expr != "Enum" && expr != "System.Enum") return false;

        return name is "Parse" or "TryParse" or "GetValues" or "GetNames" or "IsDefined";
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var name = memberAccess.Name.Identifier.Text;
        var args = invocation.ArgumentList.Arguments;

        // Extract generic type argument if present
        string? enumTypeName = null;
        if (memberAccess.Name is GenericNameSyntax genericName)
        {
            var typeArg = genericName.TypeArgumentList.Arguments.FirstOrDefault();
            if (typeArg != null)
            {
                enumTypeName = typeArg.ToString();
            }
        }

        if (name == "Parse")
        {
            // Enum.Parse<Status>("active") -> Status['active'] || 'active'
            // Fallback: Try to match enum member, otherwise return string
            if (args.Count == 0) return "undefined";

            var value = context.Converter.ConvertExpression(args[0].Expression);

            if (enumTypeName != null)
            {
                // Use helper that tries to match case-insensitively
                context.UsedHelpers.Add("parseEnum");
                return $"parseEnum({value}, {enumTypeName})";
            }

            return value; // Fallback
        }

        if (name == "TryParse")
        {
            // Enum.TryParse<Status>(str, out var result)
            // -> (result = parseEnum(str, Status), result !== undefined)
            if (args.Count < 2) return "false";

            var input = context.Converter.ConvertExpression(args[0].Expression);
            var outArg = args[1];

            string varName = "";
            if (outArg.Expression is DeclarationExpressionSyntax decl)
            {
                if (decl.Designation is SingleVariableDesignationSyntax single)
                {
                    varName = single.Identifier.Text;
                }
            }
            else
            {
                varName = context.Converter.ConvertExpression(outArg.Expression);
            }

            if (enumTypeName != null)
            {
                context.UsedHelpers.Add("parseEnum");
                return $"({varName} = parseEnum({input}, {enumTypeName}), {varName} !== undefined)";
            }

            return "false";
        }

        if (name == "GetValues")
        {
            // Enum.GetValues<Status>() -> Object.values(Status)
            // Returns array of enum values
            if (enumTypeName != null)
            {
                return $"Object.values({enumTypeName})";
            }

            // Fallback: Enum.GetValues(typeof(Status))
            if (args.Count > 0)
            {
                var typeofArg = args[0].Expression;
                if (typeofArg is TypeOfExpressionSyntax typeofExpr)
                {
                    var typeName = typeofExpr.Type.ToString();
                    return $"Object.values({typeName})";
                }
            }

            return "[]";
        }

        if (name == "GetNames")
        {
            // Enum.GetNames<Status>() -> Object.keys(Status)
            // Returns array of enum member names
            if (enumTypeName != null)
            {
                return $"Object.keys({enumTypeName})";
            }

            // Fallback: Enum.GetNames(typeof(Status))
            if (args.Count > 0)
            {
                var typeofArg = args[0].Expression;
                if (typeofArg is TypeOfExpressionSyntax typeofExpr)
                {
                    var typeName = typeofExpr.Type.ToString();
                    return $"Object.keys({typeName})";
                }
            }

            return "[]";
        }

        if (name == "IsDefined")
        {
            // Enum.IsDefined(typeof(Status), value) -> Status[value] !== undefined
            if (args.Count < 2) return "false";

            var typeofArg = args[0].Expression;
            var value = context.Converter.ConvertExpression(args[1].Expression);

            if (typeofArg is TypeOfExpressionSyntax typeofExpr)
            {
                var typeName = typeofExpr.Type.ToString();
                return $"({typeName}[{value}] !== undefined)";
            }

            return "false";
        }

        return node.ToString();
    }

    public int Priority => 10;
}
