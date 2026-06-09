using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for binary expressions (operators).
/// Handles:
/// - == -> === (strict)
/// - != -> !==
/// - &&, || pass through
/// </summary>
public class BinaryExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is BinaryExpressionSyntax;
    }

    private static readonly SpecialType[] IntegralTypes =
    {
        SpecialType.System_SByte, SpecialType.System_Byte,
        SpecialType.System_Int16, SpecialType.System_UInt16,
        SpecialType.System_Int32, SpecialType.System_UInt32,
        SpecialType.System_Int64, SpecialType.System_UInt64,
    };

    private static bool IsIntegral(ITypeSymbol? type)
    {
        if (type == null) return false;

        // Unwrap Nullable<T> (int? / int? still divides as integers).
        if (type is INamedTypeSymbol named
            && named.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T
            && named.TypeArguments.Length == 1)
        {
            type = named.TypeArguments[0];
        }

        return Array.IndexOf(IntegralTypes, type.SpecialType) >= 0;
    }

    private static bool IsDecimal(ITypeSymbol? type)
    {
        if (type is INamedTypeSymbol named
            && named.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T
            && named.TypeArguments.Length == 1)
        {
            type = named.TypeArguments[0];
        }
        return type?.SpecialType == SpecialType.System_Decimal;
    }

    private static bool IsLong(ITypeSymbol? type)
    {
        if (type is INamedTypeSymbol named
            && named.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T
            && named.TypeArguments.Length == 1)
        {
            type = named.TypeArguments[0];
        }
        return type?.SpecialType is SpecialType.System_Int64 or SpecialType.System_UInt64;
    }

    private static bool IsNamed(ITypeSymbol? type, string fullName)
    {
        if (type is INamedTypeSymbol named
            && named.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T
            && named.TypeArguments.Length == 1)
        {
            type = named.TypeArguments[0];
        }
        return type?.ToDisplayString() == fullName;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var binary = (BinaryExpressionSyntax)node;
        var left = context.Converter.ConvertExpression(binary.Left);
        var right = context.Converter.ConvertExpression(binary.Right);
        var op = binary.OperatorToken.Text;

        // decimal is an exact base-10 type implemented by the runtime Decimal class; route its
        // operators to method calls. (Null comparisons fall through to the loose-equality logic.)
        if (left != "null" && right != "null"
            && (IsDecimal(context.SemanticHelper.GetType(binary.Left))
                || IsDecimal(context.SemanticHelper.GetType(binary.Right))))
        {
            var decResult = ConvertDecimal(left, right, op, binary, context);
            if (decResult != null) return decResult;
        }

        // long/ulong are exact 64-bit via BigInt. Wrap operands in long() and use native BigInt
        // operators (BigInt `/` truncates, matching C# long division — so this must run before the
        // integer-division branch below). Null comparisons fall through to the loose-equality logic.
        if (left != "null" && right != "null" && op != "&&" && op != "||"
            && (IsLong(context.SemanticHelper.GetType(binary.Left))
                || IsLong(context.SemanticHelper.GetType(binary.Right))))
        {
            context.UsedHelpers.Add(Eq.Import);
            var jsOp = op switch { "==" => "===", "!=" => "!==", _ => op };
            return $"({Eq.Long}({left}) {jsOp} {Eq.Long}({right}))";
        }

        // DateTime/TimeSpan are runtime compat classes with operator overloads (+ - and comparisons).
        // Route to their methods based on the operand types. (Null comparisons fall through.)
        if (left != "null" && right != "null" && op != "&&" && op != "||")
        {
            var lt = context.SemanticHelper.GetType(binary.Left);
            var rt = context.SemanticHelper.GetType(binary.Right);
            var dtResult = ConvertDateTimeOrTimeSpan(left, right, op, lt, rt);
            if (dtResult != null) return dtResult;
        }

        // C# integer division truncates toward zero; JS `/` is always float division.
        // When the result type is integral, emit Math.trunc to preserve C# semantics
        // (7 / 2 == 3, not 3.5). Chained divisions nest correctly.
        if (op == "/" && IsIntegral(context.SemanticHelper.GetType(binary)))
        {
            return $"Math.trunc({left} / {right})";
        }

        // Convert C# operators to JS equivalents
        // Use loose equality for null checks to catch both null and undefined
        if ((left == "null" || right == "null") && (op == "==" || op == "!="))
        {
            // Keep op as == or != (loose)
        }
        else
        {
            op = op switch
            {
                "&&" => "&&",
                "||" => "||",
                "==" => "===", // Use strict equality in JS for non-null
                "!=" => "!==",
                _ => op
            };
        }

        return $"{left} {op} {right}";
    }

    /// <summary>
    /// Routes a decimal binary operation to the runtime Decimal class. Non-decimal operands are
    /// wrapped with dec(...). Returns null for operators not modelled (falls back to the default).
    /// </summary>
    private static string? ConvertDecimal(string left, string right, string op, BinaryExpressionSyntax binary, ConversionContext context)
    {
        // Always wrap both operands in $eq.num.dec(): it is a pass-through for existing Decimals and
        // coerces plain numbers (e.g. a decimal field that arrived from state as a JS number) — so the
        // call is safe regardless of the runtime representation.
        context.UsedHelpers.Add(Eq.Import);
        var l = $"{Eq.Dec}({left})";
        var r = $"{Eq.Dec}({right})";

        return op switch
        {
            "+" => $"{l}.add({r})",
            "-" => $"{l}.sub({r})",
            "*" => $"{l}.mul({r})",
            "/" => $"{l}.div({r})",
            "==" => $"{l}.equals({r})",
            "!=" => $"!{l}.equals({r})",
            "<" => $"({l}.compareTo({r}) < 0)",
            ">" => $"({l}.compareTo({r}) > 0)",
            "<=" => $"({l}.compareTo({r}) <= 0)",
            ">=" => $"({l}.compareTo({r}) >= 0)",
            _ => null
        };
    }

    /// <summary>
    /// Routes a binary operation involving DateTime/TimeSpan to the runtime compat methods:
    /// <c>DateTime - DateTime -> TimeSpan</c> (diff), <c>DateTime ± TimeSpan -> DateTime</c> (add/subtract),
    /// <c>TimeSpan ± TimeSpan</c>, and comparisons via <c>compareTo</c>/<c>equals</c>. Returns null for
    /// any combination not modelled (falls back to the default operator handling).
    /// </summary>
    private static string? ConvertDateTimeOrTimeSpan(string left, string right, string op, ITypeSymbol? lt, ITypeSymbol? rt)
    {
        const string dt = "System.DateTime";
        const string ts = "System.TimeSpan";
        bool lDt = IsNamed(lt, dt), rDt = IsNamed(rt, dt);
        bool lTs = IsNamed(lt, ts), rTs = IsNamed(rt, ts);

        if (lDt && rDt)
        {
            return op switch
            {
                "-" => $"{left}.diff({right})",          // -> TimeSpan
                "==" => $"{left}.equals({right})",
                "!=" => $"!{left}.equals({right})",
                "<" => $"({left}.compareTo({right}) < 0)",
                ">" => $"({left}.compareTo({right}) > 0)",
                "<=" => $"({left}.compareTo({right}) <= 0)",
                ">=" => $"({left}.compareTo({right}) >= 0)",
                _ => null,
            };
        }

        if (lDt && rTs)
        {
            return op switch
            {
                "+" => $"{left}.add({right})",
                "-" => $"{left}.subtract({right})",
                _ => null,
            };
        }

        if (lTs && rDt && op == "+") return $"{right}.add({left})"; // TimeSpan + DateTime (commutative)

        // DateOnly/TimeOnly: comparisons + equality (no operator arithmetic modelled here).
        bool lDo = IsNamed(lt, "System.DateOnly"), rDo = IsNamed(rt, "System.DateOnly");
        bool lTo = IsNamed(lt, "System.TimeOnly"), rTo = IsNamed(rt, "System.TimeOnly");
        if ((lDo && rDo) || (lTo && rTo))
        {
            return op switch
            {
                "==" => $"{left}.equals({right})",
                "!=" => $"!{left}.equals({right})",
                "<" => $"({left}.compareTo({right}) < 0)",
                ">" => $"({left}.compareTo({right}) > 0)",
                "<=" => $"({left}.compareTo({right}) <= 0)",
                ">=" => $"({left}.compareTo({right}) >= 0)",
                _ => null,
            };
        }

        if (lTs && rTs)
        {
            return op switch
            {
                "+" => $"{left}.add({right})",
                "-" => $"{left}.sub({right})",
                "==" => $"{left}.equals({right})",
                "!=" => $"!{left}.equals({right})",
                "<" => $"({left}.compareTo({right}) < 0)",
                ">" => $"({left}.compareTo({right}) > 0)",
                "<=" => $"({left}.compareTo({right}) <= 0)",
                ">=" => $"({left}.compareTo({right}) >= 0)",
                _ => null,
            };
        }

        return null;
    }

    public int Priority => 0;
}
