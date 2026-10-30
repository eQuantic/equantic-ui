using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;

/// <summary>
/// Strategy for System.Convert.ToXxx conversions, each by the overload C# bound. <c>ToDecimal</c> is
/// .NET's, by the type it converts (see <see cref="ToDecimal"/>), and text into any other numeric type
/// reads as that type's Parse does (see <see cref="TextReader"/>). Anything else into an integer, a
/// double, a single or a char goes through the runtime (<c>utils/number-convert.ts</c>), which rounds and
/// checks as .NET does, and a value whose type the call site cannot settle, an <c>object</c>, is read by
/// what it holds when the call runs (see <see cref="ToInteger"/>, #401).
/// </summary>
public class ConvertStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;

        var expr = memberAccess.Expression.ToString();
        if (expr is not ("Convert" or "System.Convert")) return false;
        // A class of the app's that shares the name is its own, as its methods are.
        if (context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol { ContainingType: { } type }
            && type.ToDisplayString() != "System.Convert") return false;

        return memberAccess.Name.Identifier.ValueText.StartsWith("To");
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var name = memberAccess.Name.Identifier.ValueText;
        var args = invocation.ArgumentList.Arguments;
        if (Encodings.TryGetValue(name, out var encoding))
            return Encoded(invocation, name, encoding, context);
        if (args.Count == 0) return JsExpr.Identifier("undefined");

        // Reading or writing an integer in a BASE is settled by the overload C# bound, which the
        // method's name cannot say: the base went nowhere, so "ff" in base 16 was NaN and -1 in
        // base 16 was "-1".
        if (context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol method && BaseTemplate(method) is { } template)
        {
            context.UsedHelpers.Add(Eq.Import);
            var parts = args.Select(argument => context.Converter.ConvertIr(argument.Expression)).ToArray();
            return JsExpr.Template(PrimitiveStaticStrategy.BindNamedArguments(template, invocation, method),
                parts);
        }

        var (argExpr, providerExpr) = Arguments(invocation, context);
        if (name == "ToDecimal")
        {
            // Text is read in the culture the call names (NumberMethodStrategy). A number converts
            // with no culture involved; a value that may be text when the call runs (an object
            // holding "1,5") is read in the culture the call names, as text is.
            var invariant = MayHoldText(context.SemanticHelper.GetType(argExpr), context)
                && NamedCulture.ReadsInvariant(invocation, providerExpr, context, NumberMethodStrategy.ReadingANumber);
            return ToDecimal(argExpr, invariant, context);
        }
        if (name == "ToBoolean")
        {
            // A bool reads its text the same in every culture, so a provider is not consulted, as .NET
            // does not consult it. It is still EVALUATED, after the value and before the conversion,
            // as C# evaluates every argument it writes: a provider with a side effect or an exception
            // went nowhere (found in review, #421). The invariant and the current culture, and a null,
            // are reads with no effect, left out as ToString leaves them. Any other CultureInfo
            // (GetCultureInfo(name), new CultureInfo(name)) may throw, and CultureInfo has no twin to
            // evaluate it with, so it is EQ2108 rather than a call dropped in silence.
            if (providerExpr is not null && !NamedCulture.IsInvariant(providerExpr, context)
                && !NamedCulture.IsCurrent(providerExpr, context) && BindsToCultureInfo(providerExpr, context))
                context.Report(invocation, ConversionSeverity.Error, "EQ2108",
                    "Convert.ToBoolean does not consult its provider, but C# evaluates it, and this culture cannot be "
                    + "evaluated in the browser, where CultureInfo has no twin. Pass CultureInfo.InvariantCulture, or no provider.");
            var provider = providerExpr is null || NamedCulture.IsInvariant(providerExpr, context)
                || NamedCulture.IsCurrent(providerExpr, context) || BindsToCultureInfo(providerExpr, context)
                ? null
                : context.Converter.ConvertIr(providerExpr);
            var reads = ReadsText(invocation, argExpr, context);
            if (reads) context.UsedHelpers.Add(Eq.Import);
            JsExpr Of(JsExpr value) => reads
                ? JsExpr.Template($"{Eq.BoolConvert}({{0}})", [value])
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
                [Of(JsExpr.Identifier("$v")), providerFirst ? provider : value, providerFirst ? value : provider]);
        }
        // A value's TEXT is its ToString in the culture the call names, which is all .NET's
        // Convert.ToString does: what a concatenation writes in the culture in force (#454), and the
        // invariant text for the invariant culture. It was String(), JavaScript's invariant text, so
        // `Convert.ToString(-1.5)` read `-1.5` on a pt-BR page that wrote `-1,5` beside it.
        if (name == "ToString")
            return TextOf(invocation, argExpr, providerExpr, context);
        if (name == "ToDateTime")
            return ToDateTime(invocation, argExpr, providerExpr, context);
        if (ReadsText(invocation, argExpr, context) && TextReader(name) is { } text)
        {
            // Text is read in the culture the call names (NumberMethodStrategy). A null literal is
            // never read, so no culture is involved.
            var invariant = !argExpr.IsKind(SyntaxKind.NullLiteralExpression)
                && NamedCulture.ReadsInvariant(invocation, providerExpr, context, NumberMethodStrategy.ReadingANumber);
            context.UsedHelpers.Add(Eq.Import);
            return JsExpr.Template($"{text.Reader}({{0}}, '{text.Tag}'{(invariant ? ", true" : "")})",
                [context.Converter.ConvertIr(argExpr)]);
        }
        if (TextReader(name) is { Reader: Eq.IntConvert } integer)
            return ToInteger(invocation, argExpr, providerExpr, integer.Tag, context);
        if (TextReader(name) is { Reader: Eq.RealConvert } real)
            return ToReal(invocation, argExpr, providerExpr, real.Tag, context);
        if (name == "ToChar")
            return ToChar(invocation, argExpr, context);
        return JsExpr.Opaque(context.Converter.ConvertExpression(argExpr));
    }

    /// <summary>The type of the value the overload C# bound takes, or the argument's where no model
    /// can say. It is <c>object</c> for a nullable and an enum, which have no overload of their own.</summary>
    private static ITypeSymbol? Source(InvocationExpressionSyntax invocation, ExpressionSyntax value, ConversionContext context) =>
        context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol { Parameters: [var first, ..] }
            ? first.Type
            : context.SemanticHelper.GetType(value);

    /// <summary>Whether a call reads an <c>object</c> in the invariant culture, which only a text it holds
    /// when the call runs can ask of it.</summary>
    private static bool ObjectReadsInvariant(InvocationExpressionSyntax invocation, ExpressionSyntax value,
        ExpressionSyntax? provider, ConversionContext context) =>
        MayHoldText(context.SemanticHelper.GetType(value), context)
        && NamedCulture.ReadsInvariant(invocation, provider, context, NumberMethodStrategy.ReadingANumber);

    /// <summary>What a conversion with no meaning throws, after evaluating its argument, as .NET does.</summary>
    private static JsExpr InvalidCast(JsExpr value, string from, string to, ConversionContext context)
    {
        var parameter = context.TypeAnnotations ? "(_: unknown)" : "(_)";
        context.UsedHelpers.Add(Eq.Import);
        return JsExpr.Template(
            $"({parameter} => {{ throw {Eq.ExceptionOf}('System.InvalidCastException', \"Invalid cast from '{from}' to '{to}'.\"); }})({{0}})",
            [value]);
    }

    /// <summary>
    /// <c>Convert.To&lt;integer&gt;</c> of what is not text, by the overload C# bound (#401): an integer
    /// of another width with its range checked, a double or a float rounded half to even, a decimal by
    /// <c>decimal.Round</c>, a char as its code unit, a bool as 1 or 0, and an <c>object</c> by what it
    /// holds when the call runs, an enum boxed for the call being its value. Every one was
    /// <c>$eq.math.round</c>, which checked no range, made no BigInt, handed a decimal back and read a
    /// char and a text as a number.
    /// </summary>
    private static JsExpr ToInteger(InvocationExpressionSyntax invocation, ExpressionSyntax value, ExpressionSyntax? provider,
        string tag, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        var converted = context.Converter.ConvertIr(value);
        var source = Source(invocation, value, context);
        var quoted = JsExpr.Literal($"'{tag}'");
        JsExpr Call(string helper, JsExpr of) => JsExpr.Call(JsExpr.Identifier(helper), of, quoted);
        if (context.SemanticHelper.GetType(value) is INamedTypeSymbol { TypeKind: TypeKind.Enum } written)
            return Call(Eq.IntegerFrom, Types.EnumShape.ValueOf(written, converted, context));
        switch (source?.SpecialType)
        {
            case SpecialType.System_Double or SpecialType.System_Single:
                return Call(Eq.IntegerFromDouble, converted);
            case SpecialType.System_Decimal:
                return Call(Eq.IntegerFromDecimal, converted);
            case SpecialType.System_Char:
                return Call(Eq.IntegerFrom, JsExpr.Template("{0}.charCodeAt(0)", [converted]));
            case SpecialType.System_Boolean:
                return tag is "long" or "ulong" ? JsExpr.Template("({0} ? 1n : 0n)", [converted]) : JsExpr.Template("({0} ? 1 : 0)", [converted]);
            case SpecialType.System_DateTime:
                return InvalidCast(converted, "DateTime", source.Name, context);
        }
        if (source is { } integer && integer.IsIntegral())
            return IntegerWidth.Of(integer) == IntegerWidth.Of(context.SemanticHelper.GetType(invocation)) ? converted : Call(Eq.IntegerFrom, converted);
        return ObjectReadsInvariant(invocation, value, provider, context)
            ? JsExpr.Call(JsExpr.Identifier(Eq.IntegerFromObject), converted, quoted, JsExpr.Literal("true"))
            : Call(Eq.IntegerFromObject, converted);
    }

    /// <summary>
    /// <c>Convert.ToDouble</c> and <c>ToSingle</c> of what is not text: a 64-bit integer to a single
    /// directly, as the cast converts it, through a double it rounds twice; an <c>object</c> by what it
    /// holds when the call runs; and a char and a date, which have no conversion to a number here.
    /// </summary>
    private static JsExpr ToReal(InvocationExpressionSyntax invocation, ExpressionSyntax value, ExpressionSyntax? provider,
        string tag, ConversionContext context)
    {
        var converted = context.Converter.ConvertIr(value);
        var source = Source(invocation, value, context);
        var target = tag == "single" ? "Single" : "Double";
        switch (source?.SpecialType)
        {
            case SpecialType.System_Char:
                return InvalidCast(converted, "Char", target, context);
            case SpecialType.System_DateTime:
                return InvalidCast(converted, "DateTime", target, context);
            case SpecialType.System_Int64 or SpecialType.System_UInt64 when tag == "single":
                context.UsedHelpers.Add(Eq.Import);
                return JsExpr.Call(JsExpr.Identifier(Eq.SingleFromLong), converted);
        }
        if (source is null or { SpecialType: SpecialType.System_Object } or { TypeKind: TypeKind.TypeParameter or TypeKind.Dynamic })
        {
            context.UsedHelpers.Add(Eq.Import);
            return ObjectReadsInvariant(invocation, value, provider, context)
                ? JsExpr.Call(JsExpr.Identifier(Eq.RealFromObject), converted, JsExpr.Literal($"'{tag}'"), JsExpr.Literal("true"))
                : JsExpr.Call(JsExpr.Identifier(Eq.RealFromObject), converted, JsExpr.Literal($"'{tag}'"));
        }
        // A single, as every float this side produces (SinglePrecision).
        return JsExpr.Template(tag == "single" ? "Math.fround(Number({0}))" : "Number({0})", [converted]);
    }

    /// <summary>
    /// <c>Convert.ToChar</c>: a char is itself, a text must be one char, an integer is its code unit in
    /// the char's range, an <c>object</c> is read by what it holds when the call runs, and a bool, a real
    /// number, a decimal and a date have no conversion to a char. Every one was
    /// <c>String.fromCharCode</c>, which read a text as a zero and wrapped an integer past the range.
    /// </summary>
    private static JsExpr ToChar(InvocationExpressionSyntax invocation, ExpressionSyntax value, ConversionContext context)
    {
        var converted = context.Converter.ConvertIr(value);
        var source = Source(invocation, value, context);
        switch (source?.SpecialType)
        {
            case SpecialType.System_Char:
                return converted;
            case SpecialType.System_String:
                context.UsedHelpers.Add(Eq.Import);
                return JsExpr.Call(JsExpr.Identifier(Eq.CharFromText), converted);
            case SpecialType.System_Boolean or SpecialType.System_Double or SpecialType.System_Single
                or SpecialType.System_Decimal or SpecialType.System_DateTime:
                return InvalidCast(converted, source.Name, "Char", context);
        }
        context.UsedHelpers.Add(Eq.Import);
        return JsExpr.Call(JsExpr.Identifier(source is { } integer && integer.IsIntegral() ? Eq.CharFrom : Eq.CharFromObject), converted);
    }

    /// <summary>
    /// The encodings, each the twin it calls and the twin's parameters in order, by the names .NET gives
    /// them. The overloads over a span name their array <c>bytes</c>, which is the twin's
    /// <c>inArray</c> (see <see cref="SlotOf"/>).
    /// </summary>
    private static readonly Dictionary<string, (string Twin, string[] Slots)> Encodings = new(StringComparer.Ordinal)
    {
        ["ToBase64String"] = (Eq.ToBase64String, ["inArray", "offset", "length", "options"]),
        ["ToBase64CharArray"] = (Eq.ToBase64CharArray, ["inArray", "offsetIn", "length", "outArray", "offsetOut", "options"]),
        ["ToHexString"] = (Eq.ToHexString, ["inArray", "offset", "length"]),
        ["ToHexStringLower"] = (Eq.ToHexStringLower, ["inArray", "offset", "length"]),
    };

    /// <summary>The twin's parameter a .NET parameter of <paramref name="parameter"/>'s name fills, or -1.</summary>
    private static int SlotOf(string[] slots, string parameter) =>
        Array.IndexOf(slots, parameter == "bytes" ? "inArray" : parameter);

    /// <summary>
    /// The parameters an encoding's overload takes, in order, where its argument count settles which
    /// overload it is: only the count is there to read when no model can be asked, and every count of
    /// these four names one overload.
    /// </summary>
    private static string[]? Spelled(string name, int count) => (name, count) switch
    {
        ("ToBase64String" or "ToHexString" or "ToHexStringLower", 1) => ["inArray"],
        ("ToBase64String", 2) => ["inArray", "options"],
        ("ToBase64String" or "ToHexString" or "ToHexStringLower", 3) => ["inArray", "offset", "length"],
        ("ToBase64String", 4) => ["inArray", "offset", "length", "options"],
        ("ToBase64CharArray", 5) => ["inArray", "offsetIn", "length", "outArray", "offsetOut"],
        ("ToBase64CharArray", 6) => ["inArray", "offsetIn", "length", "outArray", "offsetOut", "options"],
        _ => null,
    };

    /// <summary>
    /// <c>Convert.ToBase64String</c>, <c>ToBase64CharArray</c>, <c>ToHexString</c> and
    /// <c>ToHexStringLower</c>, every overload, as the runtime's twin of each (#716): each argument handed
    /// to the parameter it fills, the ones the overload does not take <c>undefined</c>, and all of them
    /// evaluated in the order they are written. They were <c>String()</c> of the array, so
    /// <c>new byte[] { 1, 2, 255 }</c> was <c>1,2,255</c> where .NET writes <c>AQL/</c>. A span that is
    /// not a collection expression has no JavaScript form, and is EQ1004.
    /// </summary>
    private static JsExpr Encoded(InvocationExpressionSyntax invocation, string name, (string Twin, string[] Slots) encoding,
        ConversionContext context)
    {
        var arguments = invocation.ArgumentList.Arguments;
        var filling = Enumerable.Repeat(-1, encoding.Slots.Length).ToArray();
        JsExpr Refused(string form) => JsExpr.Opaque(context.Unhandled(invocation, $"Convert.{name} {form}"));
        if (context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol method)
        {
            var written = ParameterTemplate.WrittenFor(arguments, method);
            foreach (var parameter in method.Parameters)
            {
                var slot = SlotOf(encoding.Slots, parameter.Name);
                if (slot < 0) return Refused($"with a parameter, '{parameter.Name}', its twin does not take");
                if (written[parameter.Ordinal] < 0) continue;
                // A span's overload takes an array too (`bytes: array` binds it), and a collection
                // expression is an array here; any other span has no JavaScript form.
                var bytes = arguments[written[parameter.Ordinal]].Expression;
                if (encoding.Slots[slot] == "inArray" && parameter.Type is not IArrayTypeSymbol
                    && bytes is not CollectionExpressionSyntax
                    && context.SemanticHelper.GetType(bytes) is not IArrayTypeSymbol)
                    return Refused("over a span, which JavaScript does not have");
                filling[slot] = written[parameter.Ordinal];
            }
        }
        else
        {
            // No model: the count settles the overload, and a named argument fills the parameter it
            // names, which must be one that overload takes.
            if (!context.CanGuess(invocation) || Spelled(name, arguments.Count) is not { } shape)
                return Refused("whose overload the spelling cannot settle");
            for (var i = 0; i < arguments.Count; i++)
            {
                var parameter = arguments[i].NameColon?.Name.Identifier.ValueText ?? shape[i];
                var slot = SlotOf(encoding.Slots, parameter);
                if (slot < 0 || !shape.Contains(encoding.Slots[slot]) || filling[slot] >= 0)
                    return Refused("whose overload the spelling cannot settle");
                filling[slot] = i;
            }
        }

        // The holes name WRITTEN positions already, so the template is not bound again.
        var holes = filling.Select(at => at < 0 ? "undefined" : "{" + at + "}").ToList();
        while (holes.Count > 1 && holes[^1] == "undefined") holes.RemoveAt(holes.Count - 1);
        context.UsedHelpers.Add(Eq.Import);
        return ParameterTemplate.Call($"{encoding.Twin}({string.Join(", ", holes)})", null, invocation, null, context);
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
    /// it, in the page's culture or the <paramref name="invariant"/> one, except that null is 0; that,
    /// and a value whose type the call site cannot settle (an object, a nullable), is the runtime's to
    /// dispatch. A char and a DateTime have no conversion: .NET throws for them after evaluating the
    /// argument, and so does this.
    /// </summary>
    private static JsExpr ToDecimal(ExpressionSyntax argument, bool invariant, ConversionContext context)
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
                return JsExpr.Template($"{Eq.Dec}({{0}} ? 1 : 0)", [value]);
            case SpecialType.System_Char or SpecialType.System_DateTime:
                return InvalidCast(value, type.Name, "Decimal", context);
        }
        if (type.IsIntegral()) return Call(Eq.Dec);
        return invariant ? Call(Eq.DecConvert, JsExpr.Literal("'double'"), JsExpr.Literal("true")) : Call(Eq.DecConvert);
    }

    /// <summary>Whether the provider is a member or a constructor of <c>CultureInfo</c> itself, which
    /// has no twin to evaluate it with.</summary>
    /// <summary>
    /// <c>Convert.ToString(value)</c> and <c>Convert.ToString(value, provider)</c>: a text is itself,
    /// a null one included, and anything else is its text in the current culture, with no provider,
    /// the current one or a null, or in the invariant culture. Any other provider is EQ2108, as it is
    /// for <c>ToString</c>: the browser formats in no other culture.
    /// </summary>
    private static JsExpr TextOf(InvocationExpressionSyntax invocation, ExpressionSyntax value, ExpressionSyntax? provider,
        ConversionContext context)
    {
        var converted = context.Converter.ConvertIr(value);
        if (context.SemanticHelper.GetType(value)?.SpecialType == SpecialType.System_String) return converted;
        if (provider is null || NamedCulture.IsCurrent(provider, context) || context.SemanticHelper.IsNullConstant(provider))
            return StringConversion.ToText(value, converted, context);
        if (NamedCulture.IsInvariant(provider, context))
            return StringConversion.InvariantText(value, converted, context);
        context.Report(invocation, ConversionSeverity.Error, "EQ2108",
            "Only CultureInfo.InvariantCulture and CultureInfo.CurrentCulture cross to JavaScript. "
            + "Convert with one of them, or format with an explicit specifier, ToString(\"N2\"), which "
            + "follows the app's culture on both targets.");
        return StringConversion.ToText(value, converted, context);
    }

    /// <summary>
    /// <c>Convert.ToDateTime</c> by the overload C# bound (#844): a text reads as <c>DateTime.Parse</c>
    /// reads it, in the culture the call names (<see cref="NamedCulture.ReadsInvariant"/>),
    /// except that a null one is <c>MinValue</c>; an object is read by what it holds when the call runs,
    /// so is a call no model binds; a date is itself; and a value of any other type has no conversion to
    /// a date, which .NET throws after evaluating it. A char held in an object is read as a text of one
    /// character, since a boxed value does not carry its type (#552). Every overload was
    /// <c>String(value)</c>, so the call answered the text of its argument, and a page that read its year
    /// read <c>undefined</c>.
    /// </summary>
    private static JsExpr ToDateTime(InvocationExpressionSyntax invocation, ExpressionSyntax value, ExpressionSyntax? provider,
        ConversionContext context)
    {
        var converted = context.Converter.ConvertIr(value);
        var type = context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol { Parameters: [var first, ..] }
            ? first.Type
            : null;
        if (type?.SpecialType == SpecialType.System_DateTime) return converted;
        context.UsedHelpers.Add(Eq.Import);
        if (type is null or { SpecialType: SpecialType.System_String or SpecialType.System_Object })
        {
            List<JsExpr> read = [converted];
            if (NamedCulture.ReadsInvariant(invocation, provider, context, Types.TimeParseStrategy.ReadingADate))
                read.Add(JsExpr.Literal("true"));
            return JsExpr.Call(JsExpr.Identifier($"{Eq.DateTime}.convert"), read);
        }
        return InvalidCast(converted, type.Name, "DateTime", context);
    }

    private static bool BindsToCultureInfo(ExpressionSyntax provider, ConversionContext context) =>
        context.SemanticHelper.GetSymbol(provider)?.ContainingType?.ToDisplayString() == "System.Globalization.CultureInfo";

    /// <summary>
    /// <c>Convert.ToBoolean</c> by the type of what it converts, as <see cref="ToDecimal"/> is (found in
    /// review, #421): a bool is itself; a number is whether it is not zero, a NaN included and a
    /// negative zero not, where a 64-bit integer is a BigInt and a decimal its twin, each compared with
    /// its own zero; and a char or a DateTime has no conversion, which .NET throws after evaluating the
    /// argument. Text reads as <c>bool.Parse</c> does before this. The lowering compared every value
    /// with the number zero, so a false bool was true (<c>false !== 0</c>) and so was <c>0L</c>
    /// (<c>0n !== 0</c>). An object, and a nullable boxed for the call, are the run time's to settle
    /// (#401): a null is false and a text reads as <c>bool.Parse</c> reads it, where <c>null !== 0</c> was
    /// true and so was any text.
    /// </summary>
    private static JsExpr ToBoolean(JsExpr value, ITypeSymbol? type, ConversionContext context)
    {
        JsExpr Template(string template) => JsExpr.Template(template, [value]);
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
                return InvalidCast(value, type.Name, "Boolean", context);
            case SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Byte or SpecialType.System_SByte
                or SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32:
                return Template("(({0}) !== 0)");
            default:
                context.UsedHelpers.Add(Eq.Import);
                return JsExpr.Call(JsExpr.Identifier(Eq.BoolFromObject), value);
        }
    }

    // Above ToStringStrategy (10) so Convert.ToString(x) is handled here, not as x.ToString().
    public int Priority => 15;
}
