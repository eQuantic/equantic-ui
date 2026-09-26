using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;

/// <summary>
/// Strategy for System.Convert.ToXxx conversions, argument-type aware where it matters.
/// <c>ToDecimal</c> is .NET's, by the type it converts (see <see cref="ToDecimal"/>), and text into
/// any other numeric type reads as that type's Parse does (see <see cref="TextReader"/>). The rest
/// cover the common faithful cases; semantics that need .NET-exact behavior (banker's rounding for
/// numeric→integer, Int64 precision) are best-effort here and will move to the eq compat helper
/// (see DOTNET-COVERAGE-PROGRAM.md).
/// </summary>
public class ConvertStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;

        var expr = memberAccess.Expression.ToString();
        if (expr is not ("Convert" or "System.Convert")) return false;

        return memberAccess.Name.Identifier.Text.StartsWith("To");
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var name = memberAccess.Name.Identifier.Text;
        var args = invocation.ArgumentList.Arguments;
        if (args.Count == 0) return JsExpr.Identifier("undefined");

        // Reading or writing an integer in a BASE is settled by the overload C# bound, which the
        // method's name cannot say: the base went nowhere, so "ff" in base 16 was NaN and -1 in
        // base 16 was "-1".
        if (context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol method && BaseTemplate(method) is { } template)
        {
            context.UsedHelpers.Add(Eq.Import);
            var parts = args.Select(argument => context.Converter.ConvertIr(argument.Expression)).ToArray();
            return JsExpr.Template(PrimitiveStaticStrategy.BindNamedArguments(template, invocation, method),
                parts, context.TypeAnnotations);
        }

        var (argExpr, providerExpr) = Arguments(invocation, context);
        if (name == "ToDecimal")
        {
            // Text is read in a culture, and the browser reads the invariant one (see ParseCulture).
            // A number converts with no culture involved; a value that may be text when the call
            // runs (an object holding "1,5") is read in the culture the call names, as text is.
            if (MayHoldText(context.SemanticHelper.GetType(argExpr), context))
                ParseCulture.Check(invocation, providerExpr, context);
            return ToDecimal(argExpr, context);
        }
        if (name == "ToBoolean")
        {
            // A bool reads its text the same in every culture, so a provider is not consulted, as .NET
            // does not consult it. It is still EVALUATED, after the value and before the conversion,
            // as C# evaluates every argument it writes: a provider with a side effect or an exception
            // went nowhere (found in review, #421). One CultureInfo names is left out, as ToString
            // leaves it out: CultureInfo's own members read a culture and do nothing else, and have no
            // twin to evaluate. The numeric readers need no such care: they accept only
            // CultureInfo.InvariantCulture (ParseCulture).
            var provider = providerExpr is null || NamesACulture(providerExpr, context)
                ? null
                : context.Converter.ConvertIr(providerExpr);
            var reads = ReadsText(invocation, argExpr, context);
            if (reads) context.UsedHelpers.Add(Eq.Import);
            JsExpr Of(JsExpr value) => reads
                ? JsExpr.Template($"{Eq.BoolConvert}({{0}})", [value], context.TypeAnnotations)
                : ToBoolean(value, context.SemanticHelper.GetType(argExpr), context);
            if (provider is null) return Of(context.Converter.ConvertIr(argExpr));
            // Both arguments in the order they are WRITTEN, which a named argument may reverse
            // (`Convert.ToBoolean(provider: P(), value: V())` runs P first), then the conversion of
            // the value (found in review, #421).
            var providerFirst = providerExpr!.SpanStart < argExpr.SpanStart;
            var value = context.Converter.ConvertIr(argExpr);
            var parameters = (providerFirst, context.TypeAnnotations) switch
            {
                (true, true) => "(_provider: unknown, $v: any)",
                (true, false) => "(_provider, $v)",
                (false, true) => "($v: any, _provider: unknown)",
                (false, false) => "($v, _provider)",
            };
            return JsExpr.Template($"({parameters} => {{0}})({{1}}, {{2}})",
                [Of(JsExpr.Identifier("$v")), providerFirst ? provider : value, providerFirst ? value : provider],
                context.TypeAnnotations);
        }
        if (ReadsText(invocation, argExpr, context) && TextReader(name) is { } text)
        {
            // Text is read in a culture, and the browser reads the invariant one (see ParseCulture).
            // A null literal is never read, so no culture is involved.
            if (!argExpr.IsKind(SyntaxKind.NullLiteralExpression))
                ParseCulture.Check(invocation, providerExpr, context);
            context.UsedHelpers.Add(Eq.Import);
            return JsExpr.Template($"{text.Reader}({{0}}, '{text.Tag}')", [context.Converter.ConvertIr(argExpr)],
                context.TypeAnnotations);
        }
        return JsExpr.Opaque(Converted(name, argExpr, context));
    }

    /// <summary>
    /// Whether the overload C# bound reads TEXT: its value parameter is a string. The argument's
    /// own type cannot say for a bare null, which has none and binds to the string overload all the
    /// same, so <c>Convert.ToInt32(null)</c> is 0 and was the null <c>$eq.math.round</c> handed
    /// back. With no model, the argument's type is all there is.
    /// </summary>
    private static bool ReadsText(InvocationExpressionSyntax invocation, ExpressionSyntax value, ConversionContext context) =>
        context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol method
            ? method.Parameters.FirstOrDefault(parameter => parameter.Ordinal == 0)?.Type.SpecialType == SpecialType.System_String
            : context.SemanticHelper.GetType(value)?.SpecialType == SpecialType.System_String;

    /// <summary>
    /// The runtime's reader for text converted to an integer or a binary floating-point type, and
    /// the tag that names the type to it, or null for a conversion that is not one of those.
    /// <c>Convert.ToInt32(string)</c> is <c>int.Parse</c> over the text, except that a null text is
    /// 0, and so on for every width: <c>parseInt</c> read "12abc" as 12 and a long's text into a
    /// number, where every other long on this side is a BigInt (#376).
    /// </summary>
    private static (string Reader, string Tag)? TextReader(string name) => name switch
    {
        "ToByte" => (Eq.IntConvert, "byte"),
        "ToSByte" => (Eq.IntConvert, "sbyte"),
        "ToInt16" => (Eq.IntConvert, "short"),
        "ToUInt16" => (Eq.IntConvert, "ushort"),
        "ToInt32" => (Eq.IntConvert, "int"),
        "ToUInt32" => (Eq.IntConvert, "uint"),
        "ToInt64" => (Eq.IntConvert, "long"),
        "ToUInt64" => (Eq.IntConvert, "ulong"),
        "ToDouble" => (Eq.RealConvert, "double"),
        "ToSingle" => (Eq.RealConvert, "single"),
        _ => null,
    };

    /// <summary>
    /// The template for an overload that reads or writes an integer in a base, or null. Reading is
    /// <c>(string value, int fromBase)</c> into any of the eight integer types, answering what .NET's
    /// overload for that type answers: the text is the type's BITS in a base other than 10, so
    /// "ffffffff" is -1 for an int. Writing is <c>(integer value, int toBase)</c> on a byte, a short,
    /// an int or a long, a byte written as the int it widens to, as .NET's own overload does, and a
    /// negative number as its bits in any base but 10.
    /// </summary>
    private static string? BaseTemplate(IMethodSymbol method)
    {
        if (method.Parameters is not [var value, { Type.SpecialType: SpecialType.System_Int32 } radix]) return null;
        if (radix.Name == "fromBase" && value.Type.SpecialType == SpecialType.System_String)
        {
            var target = method.ReturnType.SpecialType switch
            {
                SpecialType.System_Byte => "byte",
                SpecialType.System_SByte => "sbyte",
                SpecialType.System_Int16 => "short",
                SpecialType.System_UInt16 => "ushort",
                SpecialType.System_Int32 => "int",
                SpecialType.System_UInt32 => "uint",
                SpecialType.System_Int64 => "long",
                SpecialType.System_UInt64 => "ulong",
                _ => null,
            };
            return target is null ? null : $"{Eq.FromBase}({{0}}, {{1}}, '{target}')";
        }
        if (radix.Name == "toBase")
        {
            var bits = value.Type.SpecialType switch
            {
                SpecialType.System_Int16 => 16,
                SpecialType.System_Byte or SpecialType.System_Int32 => 32,
                SpecialType.System_Int64 => 64,
                _ => 0,
            };
            return bits == 0 ? null : $"{Eq.ToBase}({{0}}, {{1}}, {bits})";
        }
        return null;
    }

    /// <summary>
    /// Which argument is the value and which the format provider, as the bound method says: a named
    /// argument may come in any order, and <c>Convert.ToDecimal(provider: p, value: s)</c> has its
    /// value second. Taking the first argument for the value emitted the provider as one, and
    /// <c>CultureInfo</c> is not defined in a browser. Without a model, the value is the first.
    /// </summary>
    private static (ExpressionSyntax Value, ExpressionSyntax? Provider) Arguments(
        InvocationExpressionSyntax invocation, ConversionContext context)
    {
        var args = invocation.ArgumentList.Arguments;
        if (context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol method)
        {
            ExpressionSyntax? value = null, provider = null;
            for (var i = 0; i < args.Count; i++)
            {
                var named = args[i].NameColon?.Name.Identifier.ValueText;
                var parameter = named is null
                    ? (i < method.Parameters.Length ? method.Parameters[i] : null)
                    : method.Parameters.FirstOrDefault(p => p.Name == named);
                if (parameter?.Ordinal == 0) value = args[i].Expression;
                else if (parameter?.Type is { Name: "IFormatProvider", ContainingNamespace.Name: "System" }) provider = args[i].Expression;
            }
            if (value is not null) return (value, provider);
        }
        return (args[0].Expression, args.Count > 1 ? args[1].Expression : null);
    }

    /// <summary>Whether a value of <paramref name="type"/> may be a string when the call runs: a
    /// string itself, a type a string converts to by reference (<c>object</c>, <c>IConvertible</c>),
    /// <c>dynamic</c>, or a type parameter. A type the model cannot give is not guessed at.</summary>
    private static bool MayHoldText(ITypeSymbol? type, ConversionContext context)
    {
        if (type is null || context.SemanticModel is not { Compilation: var compilation }) return false;
        if (type.TypeKind is TypeKind.TypeParameter or TypeKind.Dynamic) return true;
        var conversion = compilation.ClassifyCommonConversion(compilation.GetSpecialType(SpecialType.System_String), type);
        return conversion.IsIdentity || (conversion.IsImplicit && conversion.IsReference);
    }

    /// <summary>
    /// <c>Convert.ToDecimal</c> by the type of what it converts (#358), which the overload C# chose
    /// is named after: a double or a float by .NET's own conversion, to 15 or 7 digits; an integer
    /// exactly; a bool as 1 or 0; a decimal as itself. A string parses as <c>decimal.Parse</c> reads
    /// it, except that null is 0; that, and a value whose type the call site cannot settle (an
    /// object, a nullable), is the runtime's to dispatch. A char and a DateTime have no conversion:
    /// .NET throws for them after evaluating the argument, and so does this.
    /// </summary>
    private static JsExpr ToDecimal(ExpressionSyntax argument, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        var value = context.Converter.ConvertIr(argument);
        var type = context.SemanticHelper.GetType(argument);
        JsExpr Call(string helper, params JsExpr[] arguments) =>
            JsExpr.Call(JsExpr.Identifier(helper), [value, .. arguments]);
        if (type.IsNullableValue())
        {
            return type.UnwrapNullable()?.SpecialType == SpecialType.System_Single
                ? Call(Eq.DecConvert, JsExpr.Literal("'single'"))
                : Call(Eq.DecConvert);
        }
        switch (type?.SpecialType)
        {
            case SpecialType.System_Decimal:
                return value;
            case SpecialType.System_Double:
                return Call(Eq.DecFromDouble);
            case SpecialType.System_Single:
                return Call(Eq.DecFromSingle);
            case SpecialType.System_Boolean:
                return JsExpr.Template($"{Eq.Dec}({{0}} ? 1 : 0)", [value], context.TypeAnnotations);
            case SpecialType.System_Char or SpecialType.System_DateTime:
                var from = type.SpecialType == SpecialType.System_Char ? "Char" : "DateTime";
                var parameter = context.TypeAnnotations ? "(_: unknown)" : "(_)";
                return JsExpr.Template(
                    $"({parameter} => {{ throw new Error(\"Invalid cast from '{from}' to 'Decimal'.\"); }})({{0}})",
                    [value], context.TypeAnnotations);
        }
        return type.IsIntegral() ? Call(Eq.Dec) : Call(Eq.DecConvert);
    }

    /// <summary>Whether a provider only names a culture: the invariant or the current one (a null
    /// included), or any other member of <c>CultureInfo</c> itself, which reads a culture and does
    /// nothing else.</summary>
    private static bool NamesACulture(ExpressionSyntax provider, ConversionContext context) =>
        NamedCulture.IsInvariant(provider, context)
        || NamedCulture.IsCurrent(provider, context)
        || context.SemanticHelper.GetSymbol(provider)?.ContainingType?.ToDisplayString() == "System.Globalization.CultureInfo";

    /// <summary>
    /// <c>Convert.ToBoolean</c> by the type of what it converts, as <see cref="ToDecimal"/> is (found in
    /// review, #421): a bool is itself; a number is whether it is not zero, a NaN included and a
    /// negative zero not, where a 64-bit integer is a BigInt and a decimal its twin, each compared with
    /// its own zero; and a char or a DateTime has no conversion, which .NET throws after evaluating the
    /// argument. Text reads as <c>bool.Parse</c> does before this. The lowering compared every value
    /// with the number zero, so a false bool was true (<c>false !== 0</c>) and so was <c>0L</c>
    /// (<c>0n !== 0</c>). An object is #401's: its type is the run time's to settle, and it still
    /// compares with zero.
    /// </summary>
    private static JsExpr ToBoolean(JsExpr value, ITypeSymbol? type, ConversionContext context)
    {
        JsExpr Template(string template) => JsExpr.Template(template, [value], context.TypeAnnotations);
        switch (type?.SpecialType)
        {
            case SpecialType.System_Boolean:
                return value;
            case SpecialType.System_Int64 or SpecialType.System_UInt64:
                return Template("(({0}) !== 0n)");
            case SpecialType.System_Decimal:
                context.UsedHelpers.Add(Eq.Import);
                return Template($"!({{0}}).equals({Eq.Dec}(0))");
            case SpecialType.System_Char or SpecialType.System_DateTime:
                var from = type.SpecialType == SpecialType.System_Char ? "Char" : "DateTime";
                var parameter = context.TypeAnnotations ? "(_: unknown)" : "(_)";
                return Template($"({parameter} => {{ throw new Error(\"Invalid cast from '{from}' to 'Boolean'.\"); }})({{0}})");
            default:
                return Template("(({0}) !== 0)");
        }
    }

    private static string Converted(string name, ExpressionSyntax argExpr, ConversionContext context)
    {
        var value = context.Converter.ConvertExpression(argExpr);
        var argType = context.SemanticHelper.GetType(argExpr);

        // Numeric → integer uses .NET banker's rounding via the $eq.math.round compat helper. Text
        // never gets here: it reads as the type's Parse does (TextReader).
        bool isIntegerTarget = name is "ToInt32" or "ToInt16" or "ToByte" or "ToSByte"
            or "ToUInt32" or "ToUInt16" or "ToInt64" or "ToUInt64";
        if (isIntegerTarget)
        {
            context.UsedHelpers.Add(Eq.Import);
            return $"{Eq.Round}({value})";
        }

        // A 64-bit integer converts to a single ONCE, from all 64 bits, as the cast does: through a
        // double it rounds twice, and a value just above a midpoint between two singles lands on it.
        if (name == "ToSingle" && argType?.SpecialType is SpecialType.System_Int64 or SpecialType.System_UInt64)
        {
            context.UsedHelpers.Add(Eq.Import);
            return $"{Eq.SingleFromLong}({value})";
        }

        return name switch
        {
            "ToString" => $"String({value})",
            // A single, as every float this side produces (SinglePrecision). Text never gets here:
            // it reads as float.Parse and double.Parse do (TextReader).
            "ToSingle" => $"Math.fround(Number({value}))",
            "ToDouble" => $"Number({value})",
            "ToChar" => $"String.fromCharCode({value})",
            _ => $"String({value})"
        };
    }

    // Above ToStringStrategy (10) so Convert.ToString(x) is handled here, not as x.ToString().
    public int Priority => 15;
}
