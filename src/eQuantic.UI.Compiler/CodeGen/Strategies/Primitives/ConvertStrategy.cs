using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;

/// <summary>
/// Strategy for System.Convert.ToXxx conversions, argument-type aware where it matters.
/// Covers the common faithful cases; semantics that need .NET-exact behavior (banker's rounding for
/// numeric→integer, decimal, Int64 precision) are best-effort here and will move to the eq compat
/// helper (see DOTNET-COVERAGE-PROGRAM.md).
/// </summary>
public class ConvertStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;

        var expr = memberAccess.Expression.ToString();
        if (expr is not ("Convert" or "System.Convert")) return false;

        return memberAccess.Name.Identifier.Text.StartsWith("To");
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var name = memberAccess.Name.Identifier.Text;
        var args = invocation.ArgumentList.Arguments;
        if (args.Count == 0) return "undefined";

        var argExpr = args[0].Expression;
        var value = context.Converter.ConvertExpression(argExpr);
        var argType = context.SemanticHelper.GetType(argExpr);
        var isStringArg = argType?.SpecialType == SpecialType.System_String;

        // Numeric → integer uses .NET banker's rounding via the `round` compat helper.
        bool isIntegerTarget = name is "ToInt32" or "ToInt16" or "ToByte" or "ToSByte"
            or "ToUInt32" or "ToUInt16" or "ToInt64" or "ToUInt64";
        if (isIntegerTarget && !isStringArg)
        {
            context.UsedHelpers.Add("round");
            return $"round({value})";
        }

        return name switch
        {
            "ToString" => $"String({value})",
            "ToInt32" or "ToInt16" or "ToByte" or "ToSByte" or "ToUInt32" or "ToUInt16" or "ToInt64" or "ToUInt64"
                => $"parseInt({value}, 10)", // string arg
            "ToDouble" or "ToSingle" or "ToDecimal"
                => isStringArg ? $"parseFloat({value})" : $"Number({value})",
            "ToBoolean" => isStringArg
                ? $"(String({value}).trim().toLowerCase() === 'true')"
                : $"(({value}) !== 0)",
            "ToChar" => $"String.fromCharCode({value})",
            _ => $"String({value})"
        };
    }

    // Above ToStringStrategy (10) so Convert.ToString(x) is handled here, not as x.ToString().
    public int Priority => 15;
}
