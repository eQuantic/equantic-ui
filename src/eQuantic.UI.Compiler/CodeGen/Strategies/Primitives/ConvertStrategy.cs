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

        var argExpr = args[0].Expression;
        if (name == "ToDecimal") return ToDecimal(argExpr, context);
        return JsExpr.Opaque(Converted(name, argExpr, context));
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
