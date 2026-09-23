using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;

/// <summary>
/// Strategy for System.Char static methods. C# chars are JS single-character strings, so case
/// methods map to string case methods and the Is* classifiers map to Unicode-aware regex tests.
/// </summary>
public class CharMethodStrategy : IExpressionIrStrategy
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

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var name = memberAccess.Name.Identifier.Text;
        var args = invocation.ArgumentList.Arguments;
        if (args.Count == 0) return JsExpr.Identifier("undefined");

        // The (string, index) overloads classify the character AT the index, and read a surrogate
        // pair there as the one code point it is, which is what .NET does. They were handed the
        // STRING, so `char.IsDigit("a1", 1)` tested "a1" against a one-character pattern and every
        // one of them answered false. The string and the index each appear once.
        //
        // IR, not text: an ARGUMENT in C# becomes a RECEIVER here, and an argument needs no
        // parentheses where a receiver does. Spliced as text, `char.ToUpper(c ? a : b)` read
        // `c ? a : b.toUpperCase()`, upper-casing only the false branch; the writer fences it.
        var c = args.Count == 2
            && context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol { Parameters: [{ Type.SpecialType: SpecialType.System_String }, ..] }
            ? JsExpr.Call(JsExpr.Identifier("String.fromCodePoint"),
                CodePointAt(context.Converter.ConvertIr(args[0].Expression), context.Converter.ConvertIr(args[1].Expression)))
            : context.Converter.ConvertIr(args[0].Expression);

        return name switch
        {
            "ToUpper" or "ToUpperInvariant" => JsExpr.Call(JsExpr.Member(c, "toUpperCase")),
            "ToLower" or "ToLowerInvariant" => JsExpr.Call(JsExpr.Member(c, "toLowerCase")),
            "IsDigit" => Test(@"/^\p{Nd}$/u", c),
            "IsNumber" => Test(@"/^\p{N}$/u", c),
            "IsLetter" => Test(@"/^\p{L}$/u", c),
            "IsLetterOrDigit" => Test(@"/^[\p{L}\p{Nd}]$/u", c),
            "IsWhiteSpace" => Test(@"/^\s$/", c),
            "IsUpper" => Test(@"/^\p{Lu}$/u", c),
            "IsLower" => Test(@"/^\p{Ll}$/u", c),
            "IsPunctuation" => Test(@"/^\p{P}$/u", c),
            "IsSeparator" => Test(@"/^\p{Z}$/u", c),
            "IsSymbol" => Test(@"/^\p{S}$/u", c),
            "IsControl" => Test(@"/^\p{Cc}$/u", c),
            "IsAscii" => JsExpr.Group(JsExpr.Binary(CodePointAt(c, JsExpr.Literal("0")), "<", JsExpr.Literal("128"))),
            _ => c,
        };
    }

    /// <summary>The code point at <paramref name="index"/>, as the number it is in C#:
    /// <c>codePointAt</c> answers <c>number | undefined</c>, which a strict tsc will not compare.</summary>
    private static JsExpr CodePointAt(JsExpr text, JsExpr index) =>
        JsExpr.Call(JsExpr.Identifier("Number"), JsExpr.Call(JsExpr.Member(text, "codePointAt"), index));

    /// <summary>A pattern tested against the character. The character is an ARGUMENT of the test,
    /// fenced by its parentheses, so the template needs no more than it has; the outer pair is the
    /// spelling this has always had.</summary>
    private static JsExpr Test(string pattern, JsExpr c) => JsExpr.Template($"({pattern}.test({{0}}))", c);

    public int Priority => 10;
}
