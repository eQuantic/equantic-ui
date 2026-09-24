using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;

/// <summary>
/// Strategy for System.Convert.ToXxx conversions, argument-type aware where it matters.
/// <c>ToDecimal</c> is .NET's, by the type it converts (see <see cref="ToDecimal"/>). The rest cover
/// the common faithful cases; semantics that need .NET-exact behavior (banker's rounding for
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
        return JsExpr.Opaque(Converted(name, argExpr, context));
    }

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

    private static string Converted(string name, ExpressionSyntax argExpr, ConversionContext context)
    {
        var value = context.Converter.ConvertExpression(argExpr);
        var argType = context.SemanticHelper.GetType(argExpr);
        var isStringArg = argType?.SpecialType == SpecialType.System_String;

        // Numeric → integer uses .NET banker's rounding via the $eq.math.round compat helper.
        bool isIntegerTarget = name is "ToInt32" or "ToInt16" or "ToByte" or "ToSByte"
            or "ToUInt32" or "ToUInt16" or "ToInt64" or "ToUInt64";
        if (isIntegerTarget && !isStringArg)
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
            "ToInt32" or "ToInt16" or "ToByte" or "ToSByte" or "ToUInt32" or "ToUInt16" or "ToInt64" or "ToUInt64"
                => $"parseInt({value}, 10)", // string arg
            // A single, as every float this side produces (SinglePrecision).
            "ToSingle" => isStringArg ? $"Math.fround(parseFloat({value}))" : $"Math.fround(Number({value}))",
            "ToDouble" => isStringArg ? $"parseFloat({value})" : $"Number({value})",
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
