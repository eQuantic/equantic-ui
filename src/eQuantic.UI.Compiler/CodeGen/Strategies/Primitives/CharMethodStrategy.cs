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

        // The (string, index) overloads classify the character AT the index, and read a surrogate
        // pair there as the one code point it is, which is what .NET does. They were handed the
        // STRING, so `char.IsDigit("a1", 1)` tested "a1" against a one-character pattern and every
        // one of them answered false. The string and the index each appear once.
        var c = args.Count == 2
            && context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol { Parameters: [{ Type.SpecialType: SpecialType.System_String }, ..] }
            ? $"String.fromCodePoint(Number({context.Converter.ConvertExpression(args[0].Expression)}.codePointAt({context.Converter.ConvertExpression(args[1].Expression)})))"
            : context.Converter.ConvertExpression(args[0].Expression);

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
            // Number(): `codePointAt` answers `number | undefined`, which a strict tsc will not compare.
            "IsAscii" => $"(Number({c}.codePointAt(0)) < 128)",
            _ => $"{c}"
        };
    }

    public int Priority => 10;
}
