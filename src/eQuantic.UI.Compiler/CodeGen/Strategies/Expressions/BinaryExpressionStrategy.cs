using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for binary expressions (operators).
/// Handles:
/// - == -> === (strict)
/// - != -> !==
/// - &&, || pass through
/// </summary>
public class BinaryExpressionStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is BinaryExpressionSyntax;
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var binary = (BinaryExpressionSyntax)node;
        // Both forms of each operand: the IR for the composition at the tail (where the writer
        // decides the punctuation) and its text for the branches that splice it into a template.
        var leftIr = context.Converter.ConvertIr(binary.Left);
        var rightIr = context.Converter.ConvertIr(binary.Right);
        var left = JsExprWriter.Write(leftIr);
        var right = JsExprWriter.Write(rightIr);
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
            return JsExpr.Callish($"{declaring.Name}.{operatorMethod}({left}, {right})");
        }

        // CHAR ARITHMETIC. A C# char in `+ - * / %` promotes to int and computes on the CODE
        // UNIT; the transpiled char is a 1-length string, so the same expression concatenated
        // ('A' + col produced "A0…") or went NaN (text[i] - 'A'). When the RESULT type is numeric
        // and an operand is a char, that operand becomes its code unit — a constant char literal
        // folds to the number, anything else asks charCodeAt(0). `char + string` stays concat:
        // its result type is string, so this branch never sees it.
        if (op is "+" or "-" or "*" or "/" or "%"
            && context.SemanticHelper.GetType(binary) is { SpecialType: not SpecialType.System_String })
        {
            var leftIsChar = context.SemanticHelper.GetType(binary.Left) is { SpecialType: SpecialType.System_Char };
            var rightIsChar = context.SemanticHelper.GetType(binary.Right) is { SpecialType: SpecialType.System_Char };
            if (leftIsChar) { left = CharCode(binary.Left, left, context); leftIr = JsExpr.Callish(left); }
            if (rightIsChar) { right = CharCode(binary.Right, right, context); rightIr = JsExpr.Callish(right); }
        }

        // decimal is an exact base-10 type implemented by the runtime Decimal class; route its
        // operators to method calls. (Null comparisons fall through to the loose-equality logic.)
        if (left != "null" && right != "null"
            && (context.SemanticHelper.GetType(binary.Left).IsDecimal()
                || context.SemanticHelper.GetType(binary.Right).IsDecimal()))
        {
            var decResult = ConvertDecimal(left, right, op, binary, context);
            if (decResult != null) return JsExpr.Opaque(decResult);
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
            var longResult = JsExpr.Opaque($"({Eq.Long}({left}) {jsOp} {Eq.Long}({right}))");
            // A 64-bit result settles like any fixed-width one: checked throws, an explicit
            // `unchecked` wraps (BigInt does not on its own), the default keeps counting.
            if (op is "+" or "-" or "*" or "<<")
            {
                var arithmetic = ArithmeticContext.Of(binary, context);
                return IntegerWidth.Settle(longResult, context.SemanticHelper.GetType(binary),
                    arithmetic.IsChecked, arithmetic.ExplicitUnchecked, context);
            }
            return longResult;
        }

        // Lifted Nullable<T> operators for primitive-numeric T (int/double/short/… — decimal and
        // long/ulong are handled by their own branches above). .NET evaluates both operands, then:
        // arithmetic -> null if either is null; relational -> FALSE if either is null. Naive JS would
        // coerce null to 0 (so `null < 5` is `true`), diverging — route through the runtime lift.
        // (Not for a string CONCATENATION with a nullable operand — that is a ToString, below.)
        if (op != "&&" && op != "||"
            && context.SemanticHelper.GetType(binary) is not { SpecialType: SpecialType.System_String })
        {
            var nlt = context.SemanticHelper.GetType(binary.Left);
            var nrt = context.SemanticHelper.GetType(binary.Right);
            if (nlt.IsNullablePrimitiveNumeric() || nrt.IsNullablePrimitiveNumeric())
            {
                if (op is "<" or ">" or "<=" or ">=")
                {
                    context.UsedHelpers.Add(Eq.Import);
                    return JsExpr.Callish($"{Eq.LiftCmp}({left}, {right}, (a, b) => a {op} b)");
                }
                if (op is "+" or "-" or "*" or "/" or "%")
                {
                    context.UsedHelpers.Add(Eq.Import);
                    // Integer division/remainder truncates toward zero in C#; preserve it inside the lift.
                    var body = (op is "/" or "%") && context.SemanticHelper.GetType(binary).IsIntegral()
                        ? (op == "/" ? "Math.trunc(a / b)" : "(a % b)")
                        : $"a {op} b";
                    return JsExpr.Callish($"{Eq.LiftArith}({left}, {right}, (a, b) => {body})");
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
            // Several of these put an operand in RECEIVER position (`{left}.diff(…)`), where a
            // loose operand rebinds silently: `a + b.diff(c)` is not `(a + b).diff(c)`.
            var dtResult = ConvertDateTimeOrTimeSpan(
                JsExprWriter.WriteIn(leftIr, JsPrecedence.Call),
                JsExprWriter.WriteIn(rightIr, JsPrecedence.Call), op, lt, rt);
            if (dtResult != null) return JsExpr.Opaque(dtResult);
        }

        // Records, structs and value tuples compare by VALUE in C# (not reference). Route ==/!= to the
        // structural helper. (Null comparisons fall through to the loose ==/!= below — correct, since
        // `record == null` is a plain null check.)
        if ((op == "==" || op == "!=") && left != "null" && right != "null"
            && (context.SemanticHelper.GetType(binary.Left).IsStructuralValueType()
                || context.SemanticHelper.GetType(binary.Right).IsStructuralValueType()))
        {
            context.UsedHelpers.Add(Eq.Import);
            return JsExpr.Opaque(op == "==" ? $"{Eq.Equals}({left}, {right})" : $"!{Eq.Equals}({left}, {right})");
        }

        // C# integer division truncates toward zero; JS `/` is always float division.
        // When the result type is integral, emit Math.trunc to preserve C# semantics
        // (7 / 2 == 3, not 3.5). Chained divisions nest correctly.
        if (op == "/" && context.SemanticHelper.GetType(binary).IsIntegral())
        {
            return JsExpr.Callish($"Math.trunc({left} / {right})");
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

        // ENUM ARITHMETIC: an enum crosses as its member NAME, so `day + 1` needs the value behind
        // the name and, when the result is the enum again, the name behind the value.
        var resultType = context.SemanticHelper.GetType(binary);
        if (op is "+" or "-" or "*" or "/" or "%" or "<" or ">" or "<=" or ">="
            && (EnumOperand(binary.Left, context) is not null || EnumOperand(binary.Right, context) is not null))
        {
            leftIr = EnumValue(binary.Left, leftIr, context);
            rightIr = EnumValue(binary.Right, rightIr, context);
            var computed = JsExpr.Binary(leftIr, op, rightIr);
            return resultType is INamedTypeSymbol { TypeKind: TypeKind.Enum } resultEnum && !resultEnum.IsFlagsEnum()
                ? JsExpr.Callish($"({CastExpressionStrategy.BuildValueToNameMap(resultEnum)})[{JsExprWriter.Write(computed)}]")
                : computed;
        }

        // A FIXED-WIDTH result settles by its type (IntegerWidth): sub-int widths and uint wrap,
        // int and long wrap under an explicit `unchecked`, a checked context throws. An int
        // product goes through Math.imul, which wraps exactly where a double would lose bits.
        if (op is "+" or "-" or "*" or "<<" && IntegerWidth.Of(resultType) is { } width)
        {
            var arithmetic = ArithmeticContext.Of(binary, context);
            var settles = arithmetic.IsChecked || arithmetic.ExplicitUnchecked || IntegerWidth.WrapsByDefault(width);
            if (settles && op == "*" && width.Bits == 32)
            {
                var product = JsExpr.Callish($"Math.imul({left}, {right})");
                return arithmetic.IsChecked
                    ? IntegerWidth.Checked(JsExpr.Binary(leftIr, "*", rightIr), width, context)
                    : width.Unsigned ? IntegerWidth.Wrap(product, width) : product;
            }
            return IntegerWidth.Settle(JsExpr.Binary(leftIr, op, rightIr), resultType,
                arithmetic.IsChecked, arithmetic.ExplicitUnchecked, context);
        }

        // A FLOAT operand of a COMPARISON is rounded to single precision where it was computed:
        // `a + b == 0.3f` holds in C# because both sides are singles. Float arithmetic itself stays
        // a double here — ECMA-335 lets an intermediate carry more precision, and rounds at the
        // STORE (FloatStore: declarations, assignments) — so a layout line is not a chain of frounds.
        if (op is "===" or "!==" or "==" or "!=" or "<" or ">" or "<=" or ">=")
        {
            leftIr = FloatStore.Settle(binary.Left, leftIr, context);
            rightIr = FloatStore.Settle(binary.Right, rightIr, context);
        }

        // STRING CONCATENATION converts each operand the way C# does, not the way JavaScript
        // does: `"a" + null` is "a" (not "anull"), `"v=" + flag` is "v=True", a null `int?` is
        // nothing. One rule, shared with interpolation (StringConversion).
        if (op == "+" && context.SemanticHelper.GetType(binary) is { SpecialType: SpecialType.System_String })
        {
            leftIr = StringConversion.ToDotNetString(binary.Left, leftIr, context);
            rightIr = StringConversion.ToDotNetString(binary.Right, rightIr, context);
        }

        return JsExpr.Binary(leftIr, op, rightIr);
    }

    /// <summary>The enum type of an operand, for a non-flags enum (a flags enum is numeric already).</summary>
    private static INamedTypeSymbol? EnumOperand(ExpressionSyntax operand, ConversionContext context) =>
        context.SemanticHelper.GetType(operand) is INamedTypeSymbol { TypeKind: TypeKind.Enum } type && !type.IsFlagsEnum()
            ? type : null;

    /// <summary>An enum operand as its underlying value; anything else as itself.</summary>
    internal static JsExpr EnumValue(ExpressionSyntax operand, JsExpr converted, ConversionContext context) =>
        EnumOperand(operand, context) is { } type
            ? JsExpr.Callish($"({CastExpressionStrategy.BuildNameToValueMap(type)})[{JsExprWriter.Write(converted)}]")
            : converted;

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

    /// <summary>A char operand's CODE UNIT: constant literals fold to the number, expressions
    /// read charCodeAt(0). The parentheses keep a compound operand intact.</summary>
    private static string CharCode(ExpressionSyntax operand, string emitted, ConversionContext context)
    {
        if (context.SemanticHelper.TryGetConstantValue(operand, out var constant) && constant is char ch)
            return ((int)ch).ToString();
        return $"({emitted}).charCodeAt(0)";
    }

    public int Priority => 0;
}
