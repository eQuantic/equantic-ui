using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;

/// <summary>
/// Strategy for System.Char static methods. C# chars are JS single-character strings, so case
/// methods map to string case methods and the Is* classifiers map to Unicode-aware regex tests.
/// </summary>
public class CharMethodStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;

        if (!context.ReceiverIsType(memberAccess.Expression,
                named => named.SpecialType == SpecialType.System_Char,
                "char", "Char", "System.Char"))
            return false;

        return memberAccess.Name.Identifier.Text is
            // The INVARIANT pair is the one a UI reaches for — a sort key, a lookup key — and it
            // was the only casing method missing. JS `toUpperCase` is already culture-independent.
            "ToUpper" or "ToUpperInvariant" or "ToLower" or "ToLowerInvariant"
            or "IsDigit" or "IsLetter" or "IsLetterOrDigit"
            or "IsWhiteSpace" or "IsUpper" or "IsLower" or "IsNumber" or "IsPunctuation"
            or "IsSeparator" or "IsSymbol" or "IsControl" or "IsAscii";
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var name = memberAccess.Name.Identifier.Text;
        var args = invocation.ArgumentList.Arguments;
        if (args.Count == 0) return "undefined";

        var c = context.Converter.ConvertExpression(args[0].Expression);

        return name switch
        {
            "ToUpper" or "ToUpperInvariant" => $"{c}.toUpperCase()",
            "ToLower" or "ToLowerInvariant" => $"{c}.toLowerCase()",
            "IsDigit" => $"(/^\\p{{Nd}}$/u.test({c}))",
            "IsNumber" => $"(/^\\p{{N}}$/u.test({c}))",
            "IsLetter" => $"(/^\\p{{L}}$/u.test({c}))",
            "IsLetterOrDigit" => $"(/^[\\p{{L}}\\p{{Nd}}]$/u.test({c}))",
            "IsWhiteSpace" => $"(/^\\s$/.test({c}))",
            "IsUpper" => $"(/^\\p{{Lu}}$/u.test({c}))",
            "IsLower" => $"(/^\\p{{Ll}}$/u.test({c}))",
            "IsPunctuation" => $"(/^\\p{{P}}$/u.test({c}))",
            "IsSeparator" => $"(/^\\p{{Z}}$/u.test({c}))",
            "IsSymbol" => $"(/^\\p{{S}}$/u.test({c}))",
            "IsControl" => $"(/^\\p{{Cc}}$/u.test({c}))",
            "IsAscii" => $"({c}.codePointAt(0) < 128)",
            _ => $"{c}"
        };
    }

    public int Priority => 10;
}
