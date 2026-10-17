using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;

/// <summary>
/// Strategy for System.Char static methods. C# chars are JS single-character strings, so the case
/// methods are the runtime's casing of a string of one, and the Is* classifiers Unicode-aware regex tests.
/// </summary>
public class CharMethodStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        // `"a1b2".Count(char.IsDigit)`, the method handed over as a delegate, is the delegate its call is
        // (MethodGroupStrategy), which asks this strategy for the call (#839).
        if (node is not InvocationExpressionSyntax invocation) return false;
        // `IsDigit(c)` under `using static System.Char`: the model names the method.
        if (invocation.Expression is IdentifierNameSyntax)
            return context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol
            {
                IsStatic: true, ContainingType.SpecialType: SpecialType.System_Char,
            } called && Lowers(called.Name);
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;

        if (!context.ReceiverIsType(memberAccess.Expression,
                named => named.SpecialType == SpecialType.System_Char,
                "char", "Char", "System.Char"))
            return false;

        return Lowers(memberAccess.Name.Identifier.ValueText);
    }

    /// <summary>The methods this strategy lowers.</summary>
    private static bool Lowers(string name) => name is
        // The casing is .NET's, in the culture the call names (#848, #725).
        "ToUpper" or "ToUpperInvariant" or "ToLower" or "ToLowerInvariant"
        or "IsDigit" or "IsLetter" or "IsLetterOrDigit"
        or "IsWhiteSpace" or "IsUpper" or "IsLower" or "IsNumber" or "IsPunctuation"
        or "IsSeparator" or "IsSymbol" or "IsControl" or "IsAscii";

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var method = (IMethodSymbol)context.SemanticHelper.GetSymbol(invocation)!;
        // The culture is read from how the call names it (Culture, below) and never as a value, which
        // the browser has none of: converted, `CultureInfo.InvariantCulture` was a read EQ2004 refuses (#847).
        var culture = CultureArgument(invocation, method);
        var args = invocation.ArgumentList.Arguments
            .Select(a => a == culture ? JsExpr.Identifier("undefined") : context.Converter.ConvertIr(a.Expression))
            .ToArray();
        if (args.Length == 0) return JsExpr.Identifier("undefined");
        return Lowered(method, args,
            template => PrimitiveStaticStrategy.BindNamedArguments(template, invocation, method), context,
            Culture(invocation, method, context));
    }

    /// <summary>
    /// What a casing's culture argument hands the twin: <c>true</c> for the invariant culture, nothing
    /// for the current one, and <c>null</c> for a null, which the twin refuses as .NET does, after the
    /// char is evaluated. Any other culture fails the build (EQ2108). Its argument was dropped, so a
    /// null was never refused and a page in tr-TR upper-cased an i as the invariant culture does (#725).
    /// </summary>
    private static string? Culture(InvocationExpressionSyntax invocation, IMethodSymbol method, ConversionContext context)
    {
        if (CultureArgument(invocation, method) is not { } argument) return null;
        if (NamedCulture.IsNull(argument.Expression, context)) return "null";
        return NamedCulture.ReadsInvariant(invocation, argument.Expression, context, StringMethodStrategy.ChangingCase)
            ? "true"
            : null;
    }

    /// <summary>The argument a casing's <c>CultureInfo</c> parameter takes, by its name or its place, or
    /// null for a call that takes none.</summary>
    private static ArgumentSyntax? CultureArgument(InvocationExpressionSyntax invocation, IMethodSymbol? method)
    {
        if (method?.Parameters.FirstOrDefault(parameter => parameter.Type.IsNamed("System.Globalization.CultureInfo")) is not { } parameter)
            return null;
        return invocation.ArgumentList.Arguments
            .FirstOrDefault(argument => argument.NameColon?.Name.Identifier.ValueText == parameter.Name)
            ?? invocation.ArgumentList.Arguments[parameter.Ordinal];
    }

    /// <summary>
    /// The call of <paramref name="method"/> on <paramref name="args"/>, already converted, a
    /// template over parameter holes that <paramref name="place"/> orders as the call names them.
    /// </summary>
    private static JsExpr Lowered(IMethodSymbol method, JsExpr[] args, Func<string, string> place, ConversionContext context,
        string? culture = null)
    {
        var name = method.Name;
        // The invariant pair names its culture by its name; ToUpper and ToLower by their argument.
        culture = name.EndsWith("Invariant", StringComparison.Ordinal) ? "true" : culture;
        var cased = culture is null ? "" : $", {culture}";

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
            "ToUpper" or "ToUpperInvariant" => $"{Eq.ToUpper}({c}{cased})",
            "ToLower" or "ToLowerInvariant" => $"{Eq.ToLower}({c}{cased})",
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
        if (name is "IsWhiteSpace" or "ToUpper" or "ToUpperInvariant" or "ToLower" or "ToLowerInvariant")
            context.UsedHelpers.Add(Eq.Import);
        return JsExpr.Template(place(template), args);
    }

    /// <summary>A pattern tested against the character, which is an argument of the test.</summary>
    private static string Test(string pattern, string c) => $"({pattern}.test({c}))";

    // Ahead of IdentifierStrategy's 10, which writes a bare static method's name through its class:
    // `IsDigit` under `using static System.Char` was `Char.isDigit`, a class nothing defines.
    public int Priority => 11;
}
