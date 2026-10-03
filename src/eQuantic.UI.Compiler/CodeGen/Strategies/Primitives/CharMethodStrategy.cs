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
        // `"a1b2".Count(char.IsDigit)`: the method handed over as a delegate, a method GROUP.
        if (node is MemberAccessExpressionSyntax group) return Group(group, context) is not null;
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;

        if (!context.ReceiverIsType(memberAccess.Expression,
                named => named.SpecialType == SpecialType.System_Char,
                "char", "Char", "System.Char"))
            return false;

        return Lowers(memberAccess.Name.Identifier.Text);
    }

    /// <summary>The methods this strategy lowers.</summary>
    private static bool Lowers(string name) => name is
        // The INVARIANT pair is the one a UI reaches for — a sort key, a lookup key — and it
        // was the only casing method missing. JS `toUpperCase` is already culture-independent.
        "ToUpper" or "ToUpperInvariant" or "ToLower" or "ToLowerInvariant"
        or "IsDigit" or "IsLetter" or "IsLetterOrDigit"
        or "IsWhiteSpace" or "IsUpper" or "IsLower" or "IsNumber" or "IsPunctuation"
        or "IsSeparator" or "IsSymbol" or "IsControl" or "IsAscii";

    /// <summary>The method a member access names WITHOUT calling it, when it is one of the char
    /// statics this strategy lowers; null for anything else, a call included.</summary>
    private static IMethodSymbol? Group(MemberAccessExpressionSyntax access, ConversionContext context) =>
        access.Parent is InvocationExpressionSyntax call && call.Expression == access
            ? null
            : context.SemanticHelper.GetSymbol(access) is IMethodSymbol
                {
                    IsStatic: true, ContainingType.SpecialType: SpecialType.System_Char,
                } method && Lowers(method.Name)
                ? method
                : null;

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        // A method GROUP is an arrow over the method's own parameters, its body the call as it is
        // lowered when written out. It was EQ1001: the `char` it names was converted as a value
        // (#524). Typed, since nothing may type it from outside (`Func<char, bool> f = char.IsDigit`).
        if (node is MemberAccessExpressionSyntax group && Group(group, context) is { } named)
        {
            var parameters = named.Parameters.Select(p => p.Name.ToJsIdentifier()).ToArray();
            var typed = named.Parameters.Select((p, i) => context.TypeAnnotations
                ? $"{parameters[i]}: {(p.Type.SpecialType == SpecialType.System_Int32 ? "number" : "string")}"
                : parameters[i]);
            var body = Lowered(named, parameters.Select(JsExpr.Identifier).ToArray(), template => template, context);
            return JsExpr.Arrow(string.Join(", ", typed), body);
        }

        var invocation = (InvocationExpressionSyntax)node;
        var args = invocation.ArgumentList.Arguments
            .Select(a => context.Converter.ConvertIr(a.Expression))
            .ToArray();
        if (args.Length == 0) return JsExpr.Identifier("undefined");
        var method = (IMethodSymbol)context.SemanticHelper.GetSymbol(invocation)!;
        return Lowered(method, args,
            template => PrimitiveStaticStrategy.BindNamedArguments(template, invocation, method), context);
    }

    /// <summary>
    /// The call of <paramref name="method"/> on <paramref name="args"/>, already converted, a
    /// template over parameter holes that <paramref name="place"/> orders as the call names them.
    /// </summary>
    private static JsExpr Lowered(IMethodSymbol method, JsExpr[] args, Func<string, string> place, ConversionContext context)
    {
        var name = method.Name;

        // The (string, index) overloads classify the character AT the index, and read a surrogate
        // pair there as the one code point it is, which is what .NET does. They were handed the
        // STRING, so `char.IsDigit("a1", 1)` tested "a1" against a one-character pattern and every
        // one of them answered false.
        //
        // A template over PARAMETER holes: the writer fences a hole an operator or a member access
        // touches, so `char.ToUpper(c ? a : b)` upper-cases the conditional's answer and not its
        // last branch, and binds each part once, in the order C# evaluates the arguments, so a
        // named argument written out of order (`char.IsLetter(index: 1, s: "1a")`) fills its own.
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
            // .NET's white space is the Unicode White_Space property. JavaScript's `\s` is not: it
            // leaves out NEXT LINE (U+0085) and takes in the byte order mark (U+FEFF), and those
            // are the whole difference over the BMP, measured on both sides. The runtime keeps the
            // set in one place (utils/white-space), which Trim and Split read too, as a comparison
            // per code unit: the code editor's tokenizers ask it of every character, and a pattern
            // tested there measured about five times slower.
            "IsWhiteSpace" => $"{Eq.IsWhiteSpace}({c})",
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
        if (name == "IsWhiteSpace") context.UsedHelpers.Add(Eq.Import);
        return JsExpr.Template(place(template), args, context.TypeAnnotations);
    }

    /// <summary>A pattern tested against the character, which is an argument of the test.</summary>
    private static string Test(string pattern, string c) => $"({pattern}.test({c}))";

    public int Priority => 10;
}
