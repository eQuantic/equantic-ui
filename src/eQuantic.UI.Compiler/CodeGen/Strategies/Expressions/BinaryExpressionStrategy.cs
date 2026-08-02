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

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var binary = (BinaryExpressionSyntax)node;
        var left = context.Converter.ConvertExpression(binary.Left);
        var right = context.Converter.ConvertExpression(binary.Right);
        var op = binary.OperatorToken.Text;

        // A USER-DEFINED operator: JavaScript cannot overload `+`, so the emitted class carries the
        // operator as a static method and the call site has to reach it. Left alone, `a + b` on two
        // objects concatenated their toString()s — wrong output, nothing to see, no error.
        if (context.SemanticHelper.GetSymbol(binary) is IMethodSymbol
            { MethodKind: MethodKind.UserDefinedOperator, IsImplicitlyDeclared: false,
              ContainingType: { } declaring }
            && RecordTypeEmitter.OperatorMethodName(op) is { } operatorMethod
            && declaring.Locations.Any(location => location.IsInSource))
        {
            return $"{declaring.Name}.{operatorMethod}({left}, {right})";
        }

        // decimal is an exact base-10 type implemented by the runtime Decimal class; route its
        // operators to method calls. (Null comparisons fall through to the loose-equality logic.)
        if (left != "null" && right != "null"
            && (context.SemanticHelper.GetType(binary.Left).IsDecimal()
                || context.SemanticHelper.GetType(binary.Right).IsDecimal()))
        {
            var decResult = ConvertDecimal(left, right, op, binary, context);
            if (decResult != null) return decResult;
        }

        // long/ulong are exact 64-bit via BigInt. Wrap operands in long() and use native BigInt
        // operators (BigInt `/` truncates, matching C# long division — so this must run before the
        // integer-division branch below). Null comparisons fall through to the loose-equality logic.
        if (left != "null" && right != "null" && op != "&&" && op != "||"
            && (context.SemanticHelper.GetType(binary.Left).IsLong()
                || context.SemanticHelper.GetType(binary.Right).IsLong()))
        {
            context.UsedHelpers.Add(Eq.Import);
            var jsOp = op switch { "==" => "===", "!=" => "!==", _ => op };
            return $"({Eq.Long}({left}) {jsOp} {Eq.Long}({right}))";
        }

        // Lifted Nullable<T> operators for primitive-numeric T (int/double/short/… — decimal and
        // long/ulong are handled by their own branches above). .NET evaluates both operands, then:
        // arithmetic -> null if either is null; relational -> FALSE if either is null. Naive JS would
        // coerce null to 0 (so `null < 5` is `true`), diverging — route through the runtime lift.
        if (op != "&&" && op != "||")
        {
            var nlt = context.SemanticHelper.GetType(binary.Left);
            var nrt = context.SemanticHelper.GetType(binary.Right);
            if (nlt.IsNullablePrimitiveNumeric() || nrt.IsNullablePrimitiveNumeric())
            {
                if (op is "<" or ">" or "<=" or ">=")
                {
                    context.UsedHelpers.Add(Eq.Import);
                    return $"{Eq.LiftCmp}({left}, {right}, (a, b) => a {op} b)";
                }
                if (op is "+" or "-" or "*" or "/" or "%")
                {
                    context.UsedHelpers.Add(Eq.Import);
                    // Integer division/remainder truncates toward zero in C#; preserve it inside the lift.
                    var body = (op is "/" or "%") && context.SemanticHelper.GetType(binary).IsIntegral()
                        ? (op == "/" ? "Math.trunc(a / b)" : "(a % b)")
                        : $"a {op} b";
                    return $"{Eq.LiftArith}({left}, {right}, (a, b) => {body})";
                }
                // == != fall through: strict ===/!== already match .NET nullable equality
                // (null===null is true; value===null is false).
            }
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

        // Records, structs and value tuples compare by VALUE in C# (not reference). Route ==/!= to the
        // structural helper. (Null comparisons fall through to the loose ==/!= below — correct, since
        // `record == null` is a plain null check.)
        if ((op == "==" || op == "!=") && left != "null" && right != "null"
            && (context.SemanticHelper.GetType(binary.Left).IsStructuralValueType()
                || context.SemanticHelper.GetType(binary.Right).IsStructuralValueType()))
        {
            context.UsedHelpers.Add(Eq.Import);
            return op == "==" ? $"{Eq.Equals}({left}, {right})" : $"!{Eq.Equals}({left}, {right})";
        }

        // C# integer division truncates toward zero; JS `/` is always float division.
        // When the result type is integral, emit Math.trunc to preserve C# semantics
        // (7 / 2 == 3, not 3.5). Chained divisions nest correctly.
        if (op == "/" && context.SemanticHelper.GetType(binary).IsIntegral())
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
        bool lDt = lt.IsNamed(dt), rDt = rt.IsNamed(dt);
        bool lTs = lt.IsNamed(ts), rTs = rt.IsNamed(ts);

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

        // DateTimeOffset: like DateTime — DTO - DTO -> TimeSpan, DTO ± TimeSpan -> DTO, comparisons by instant.
        bool lDto = lt.IsNamed("System.DateTimeOffset"), rDto = rt.IsNamed("System.DateTimeOffset");
        if (lDto && rDto)
        {
            return op switch
            {
                "-" => $"{left}.diff({right})",
                "==" => $"{left}.equals({right})",
                "!=" => $"!{left}.equals({right})",
                "<" => $"({left}.compareTo({right}) < 0)",
                ">" => $"({left}.compareTo({right}) > 0)",
                "<=" => $"({left}.compareTo({right}) <= 0)",
                ">=" => $"({left}.compareTo({right}) >= 0)",
                _ => null,
            };
        }
        if (lDto && rTs)
        {
            return op switch { "+" => $"{left}.add({right})", "-" => $"{left}.subtract({right})", _ => null };
        }

        // DateOnly/TimeOnly: comparisons + equality (no operator arithmetic modelled here).
        bool lDo = lt.IsNamed("System.DateOnly"), rDo = rt.IsNamed("System.DateOnly");
        bool lTo = lt.IsNamed("System.TimeOnly"), rTo = rt.IsNamed("System.TimeOnly");
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
