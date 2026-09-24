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
        var args = invocation.ArgumentList.Arguments
            .Select(a => context.Converter.ConvertIr(a.Expression))
            .ToArray();
        if (args.Length == 0) return JsExpr.Identifier("undefined");

        // The (string, index) overloads classify the character AT the index, and read a surrogate
        // pair there as the one code point it is, which is what .NET does. They were handed the
        // STRING, so `char.IsDigit("a1", 1)` tested "a1" against a one-character pattern and every
        // one of them answered false.
        //
        // A template over PARAMETER holes: the writer fences a hole an operator or a member access
        // touches, so `char.ToUpper(c ? a : b)` upper-cases the conditional's answer and not its
        // last branch, and binds each part once, in the order C# evaluates the arguments, so a
        // named argument written out of order (`char.IsLetter(index: 1, s: "1a")`) fills its own.
        var method = (IMethodSymbol)context.SemanticHelper.GetSymbol(invocation)!;
        var c = method.Parameters is [{ Type.SpecialType: SpecialType.System_String }, ..] && args.Length == 2
            ? "String.fromCodePoint(Number({0}.codePointAt({1})))"
            : "{0}";

        var template = name switch
        {
            "ToUpper" or "ToUpperInvariant" => $"{c}.toUpperCase()",
            "ToLower" or "ToLowerInvariant" => $"{c}.toLowerCase()",
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
            // Number(): `codePointAt` answers `number | undefined`, which a strict tsc will not compare.
            "IsAscii" => $"(Number({c}.codePointAt(0)) < 128)",
            _ => c,
        };
        return JsExpr.Template(PrimitiveStaticStrategy.BindNamedArguments(template, invocation, method),
            args, context.TypeAnnotations);
    }

    /// <summary>A pattern tested against the character, which is an argument of the test.</summary>
    private static string Test(string pattern, string c) => $"({pattern}.test({c}))";

    public int Priority => 10;
}
