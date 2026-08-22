using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;

/// <summary>
/// The static surface of the primitive types themselves — <c>double.IsNaN</c>, <c>int.Clamp</c>,
/// <c>char.IsAsciiLetter</c>, <c>double.MaxValue</c> — which .NET 7+ made the idiomatic home of
/// what used to live only on <c>Math</c>. Symbol-first and TABLE-driven: a member is translated
/// only when the table names it AND the bound symbol's containing type is the primitive, so a
/// user method that merely shares a name never routes here, and everything outside the table
/// stays visibly fenced in the BCL audit baseline instead of silently guessed.
/// <para>
/// The table admits only translations that are FAITHFUL and evaluate every argument exactly once:
/// char predicates go through single-evaluation regexes, template parts the writer binds to
/// temps, and what needs bit access or exact argument reduction goes through a runtime helper
/// (<c>$eq.math.sinPi</c> is 0 at integers where <c>Math.sin(Math.PI)</c> is 1.22e-16;
/// <c>$eq.bits.rotateLeft64</c> rotates the long's two's-complement bits). One generated
/// conformance case per entry proves each mapping against .NET (NumericBclConformanceTests).
/// What stays fenced is impossible BY CONSTRUCTION or deliberately out of scope:
/// <c>Int64.BigMul</c> returns an Int128 (a type with no twin), <c>Char.GetNumericValue</c>
/// needs the Unicode numeric-value table (data, not a function), <c>Char.GetUnicodeCategory</c>
/// would need thirty <c>\p{…}</c> classes mapped to the enum (derivable — parked until someone
/// needs it), <c>String.IsInterned</c> asks about an intern pool JavaScript does not have, and
/// the <c>ReciprocalEstimate</c> pair answers with the PLATFORM's hardware estimate (.NET on
/// ARM64 uses FRECPE — there is no number this side could faithfully produce).
/// <c>Parse</c>/<c>TryParse</c> stay with <see cref="NumberMethodStrategy"/>.
/// </para>
/// </summary>
public class PrimitiveStaticStrategy : IExpressionIrStrategy
{
    public int Priority => 12;

    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        switch (node)
        {
            case InvocationExpressionSyntax invocation:
                return context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol
                {
                    IsStatic: true, ContainingType: { } home
                } method && MethodTable(home.SpecialType, method.Name, invocation.ArgumentList.Arguments.Count) is not null;

            case MemberAccessExpressionSyntax access:
                return context.SemanticHelper.GetSymbol(access) is { IsStatic: true, ContainingType: { } owner } member
                    && member is IFieldSymbol or IPropertySymbol
                    && ConstantTable(owner.SpecialType, member.Name) is not null;

            default:
                return false;
        }
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        if (node is MemberAccessExpressionSyntax access)
        {
            var member = context.SemanticHelper.GetSymbol(access)!;
            return ConstantTable(member.ContainingType!.SpecialType, member.Name)!;
        }

        var invocation = (InvocationExpressionSyntax)node;
        var method = (IMethodSymbol)context.SemanticHelper.GetSymbol(invocation)!;
        var args = invocation.ArgumentList.Arguments
            .Select(a => context.Converter.ConvertIr(a.Expression))
            .ToArray();

        // Templates say what they compute; the writer decides what to evaluate once.
        var emit = MethodTable(method.ContainingType.SpecialType, method.Name, args.Length)!;
        if (emit.Contains("$eq.")) context.UsedHelpers.Add(Eq.Import);
        return JsExpr.Template(emit, args, context.TypeAnnotations);
    }

    private static bool IsSmallInteger(SpecialType type) => type is SpecialType.System_Int32
        or SpecialType.System_Int16 or SpecialType.System_Byte or SpecialType.System_SByte
        or SpecialType.System_UInt16;

    private static bool IsFloating(SpecialType type) =>
        type is SpecialType.System_Double or SpecialType.System_Single;

    /// <summary>Emission template ({0}, {1}, … are the converted arguments), or null = fenced.</summary>
    private static string? MethodTable(SpecialType home, string name, int argCount)
    {
        if (IsFloating(home))
        {
            var shared = SharedNumeric(name, argCount);
            if (shared is not null) return shared;
            return name switch
            {
                // The Math surface, on the type where .NET 7 put it.
                "Acos" => "Math.acos({0})",
                "Acosh" => "Math.acosh({0})",
                "Asin" => "Math.asin({0})",
                "Asinh" => "Math.asinh({0})",
                "Atan" => "Math.atan({0})",
                "Atan2" => "Math.atan2({0}, {1})",
                "Atanh" => "Math.atanh({0})",
                "Cbrt" => "Math.cbrt({0})",
                "Ceiling" => "Math.ceil({0})",
                "Cos" => "Math.cos({0})",
                "Cosh" => "Math.cosh({0})",
                "Exp" => "Math.exp({0})",
                "ExpM1" => "Math.expm1({0})",
                "Exp2" => "Math.pow(2, {0})",
                "Exp10" => "Math.pow(10, {0})",
                "Floor" => "Math.floor({0})",
                "Hypot" => "Math.hypot({0}, {1})",
                "Log" when argCount == 1 => "Math.log({0})",
                "Log" when argCount == 2 => "(Math.log({0}) / Math.log({1}))",
                "Log10" => "Math.log10({0})",
                "Log2" => "Math.log2({0})",
                "LogP1" => "Math.log1p({0})",
                "Pow" => "Math.pow({0}, {1})",
                // Banker's rounding is .NET's default — JS Math.round is not it (see MathStrategy).
                "Round" when argCount == 1 => $"{Eq.Round}({{0}})",
                "Round" when argCount == 2 => $"{Eq.Round}({{0}}, {{1}})",
                "Sin" => "Math.sin({0})",
                "Sinh" => "Math.sinh({0})",
                "Sqrt" => "Math.sqrt({0})",
                "Tan" => "Math.tan({0})",
                "Tanh" => "Math.tanh({0})",
                "Truncate" => "Math.trunc({0})",
                "DegreesToRadians" => "({0} * (Math.PI / 180))",
                "RadiansToDegrees" => "({0} * (180 / Math.PI))",
                // Classification predicates with faithful single-evaluation forms.
                "IsNaN" => "Number.isNaN({0})",
                "IsFinite" => "Number.isFinite({0})",
                "IsInfinity" => "(Math.abs({0}) === Infinity)",
                "IsPositiveInfinity" => "({0} === Infinity)",
                "IsNegativeInfinity" => "({0} === -Infinity)",
                "IsInteger" => "Number.isInteger({0})",
                // The *Pi family reduces its argument EXACTLY before the plain function — the
                // point of these members is SinPi(1) being 0 where Math.sin(Math.PI) is 1.22e-16.
                "SinPi" => "$eq.math.sinPi({0})",
                "CosPi" => "$eq.math.cosPi({0})",
                "TanPi" => "$eq.math.tanPi({0})",
                "AcosPi" => "(Math.acos({0}) / Math.PI)",
                "AsinPi" => "(Math.asin({0}) / Math.PI)",
                "AtanPi" => "(Math.atan({0}) / Math.PI)",
                "Atan2Pi" => "(Math.atan2({0}, {1}) / Math.PI)",
                // Tuples cross as arrays; the shared argument binds once (JsTemplate).
                "SinCos" => "[Math.sin({0}), Math.cos({0})]",
                "SinCosPi" => "[$eq.math.sinPi({0}), $eq.math.cosPi({0})]",
                // Composed exponentials and logs.
                "Exp10M1" => "(Math.pow(10, {0}) - 1)",
                "Exp2M1" => "(Math.pow(2, {0}) - 1)",
                "Log10P1" => "Math.log10(1 + {0})",
                "Log2P1" => "Math.log2(1 + {0})",
                // Sign and classification over the double's ACTUAL semantics: the sign BIT decides
                // for ±0 (CopySign(3.5, -0.0) is -3.5). NaN's sign bit stays out of reach — the
                // one corner these forms concede.
                "CopySign" => "(({1} < 0 || Object.is({1}, -0)) ? -Math.abs({0}) : Math.abs({0}))",
                "IsNegative" => "({0} < 0 || Object.is({0}, -0))",
                "IsPositive" => "(!({0} < 0) && !Object.is({0}, -0))",
                "IsEvenInteger" => "(Number.isInteger({0}) && {0} % 2 === 0)",
                "IsOddInteger" => "(Number.isInteger({0}) && Math.abs({0} % 2) === 1)",
                "IsNormal" => "(Number.isFinite({0}) && {0} !== 0 && Math.abs({0}) >= 2.2250738585072014e-308)",
                "IsSubnormal" => "({0} !== 0 && Math.abs({0}) < 2.2250738585072014e-308)",
                "IsRealNumber" => "(!Number.isNaN({0}))",
                // A power of two round-trips through its own log — exact for every power,
                // subnormals included, and off-by-anything for every non-power.
                "IsPow2" => "({0} > 0 && Number.isFinite({0}) && Math.pow(2, Math.round(Math.log2({0}))) === {0})",
                // The bit-adjacent surface rides the runtime's Float64 view.
                "BitIncrement" => "$eq.math.bitIncrement({0})",
                "BitDecrement" => "$eq.math.bitDecrement({0})",
                "ILogB" => "$eq.math.ilogb({0})",
                // One rounding, per contract: FMA via TwoProduct/TwoSum; the ESTIMATE member's
                // contract allows any of its implementations, and the fused one matches .NET
                // wherever the host has hardware FMA (this Mac does).
                "FusedMultiplyAdd" => "$eq.math.fma({0}, {1}, {2})",
                "MultiplyAddEstimate" => "$eq.math.fma({0}, {1}, {2})",
                // .NET's IEEE remainder is DEFINED as this expression, half-to-even included —
                // $eq.math.round is banker's already.
                "Ieee754Remainder" => $"({{0}} - {{1}} * {Eq.Round}({{0}} / {{1}}))",
                // The documented .NET formula, verbatim.
                "Lerp" => "(({0} * (1 - {2})) + ({1} * {2}))",
                "ScaleB" => "({0} * Math.pow(2, {1}))",
                "RootN" => "$eq.math.rootN({0}, {1})",
                // Native = the platform's plain comparison; no NaN promises to keep.
                "MaxNative" => "Math.max({0}, {1})",
                "MinNative" => "Math.min({0}, {1})",
                "ClampNative" => "Math.min(Math.max({0}, {1}), {2})",
                // The tie-and-NaN rule set lives once, in the runtime.
                "MaxMagnitude" => "$eq.math.maxMagnitude({0}, {1})",
                "MinMagnitude" => "$eq.math.minMagnitude({0}, {1})",
                "MaxMagnitudeNumber" => "$eq.math.maxMagnitudeNumber({0}, {1})",
                "MinMagnitudeNumber" => "$eq.math.minMagnitudeNumber({0}, {1})",
                "MaxNumber" => "$eq.math.maxNumber({0}, {1})",
                "MinNumber" => "$eq.math.minNumber({0}, {1})",
                _ => null,
            };
        }

        if (IsSmallInteger(home))
        {
            var shared = SharedNumeric(name, argCount);
            if (shared is not null) return shared;

            // The BIT surface is width-specific — a short rotates 16 bits, not 32 — so only
            // Int32's members are admitted; the narrower widths stay fenced until someone needs
            // them with their own masks.
            if (home == SpecialType.System_Int32)
            {
                var int32 = name switch
                {
                    // Exact past 2^32: the product IS a long, so it computes as one.
                    "BigMul" when argCount == 2 => $"({Eq.Long}({{0}}) * {Eq.Long}({{1}}))",
                    "IsPow2" => "({0} > 0 && ({0} & ({0} - 1)) === 0)",
                    "LeadingZeroCount" => "Math.clz32({0})",
                    "Log2" => "({0} === 0 ? 0 : 31 - Math.clz32({0}))",
                    "PopCount" => "$eq.bits.popCount32({0})",
                    // JS masks shift counts to 5 bits exactly as the IL does, so the count wraps
                    // for free, and `|` lands the result back in signed int32.
                    "RotateLeft" when argCount == 2 => "(({0} << {1}) | ({0} >>> (32 - {1})))",
                    "RotateRight" when argCount == 2 => "(({0} >>> {1}) | ({0} << (32 - {1})))",
                    "TrailingZeroCount" => "({0} === 0 ? 32 : 31 - Math.clz32({0} & -{0}))",
                    _ => null,
                };
                if (int32 is not null) return int32;
            }

            return name switch
            {
                // Small ints have no -0 and no NaN, so the plain comparisons are exact.
                "IsPositive" => "({0} >= 0)",
                "IsNegative" => "({0} < 0)",
                "IsEvenInteger" => "({0} % 2 === 0)",
                "IsOddInteger" => "(Math.abs({0} % 2) === 1)",
                // Width-agnostic: the magnitude fits every small width without wrapping.
                "CopySign" when argCount == 2 => "({1} < 0 ? -Math.abs({0}) : Math.abs({0}))",
                // A tuple crosses as an array; both parts of the division bind once.
                "DivRem" when argCount == 2 => "[Math.trunc({0} / {1}), {0} % {1}]",
                // Ties: the larger magnitude wins; an exact tie goes to the greater value for
                // Max and the lesser for Min — which is what max/min of the pair says.
                "MaxMagnitude" when argCount == 2 =>
                    "(Math.abs({0}) > Math.abs({1}) ? {0} : Math.abs({0}) < Math.abs({1}) ? {1} : Math.max({0}, {1}))",
                "MinMagnitude" when argCount == 2 =>
                    "(Math.abs({0}) > Math.abs({1}) ? {1} : Math.abs({0}) < Math.abs({1}) ? {0} : Math.min({0}, {1}))",
                _ => null,
            };
        }

        if (home == SpecialType.System_Int64)
        {
            // A long IS a BigInt on this side, so the shared Math.* table cannot serve — BigInt
            // has no Math. Templates keep every argument single-evaluation; Sign answers a NUMBER
            // (C#'s long.Sign returns int). The 64-bit bit surface rides $eq.bits, which counts
            // and rotates the two's-complement bits .NET sees. BigMul stays fenced: it returns an
            // Int128, a type with no twin.
            return name switch
            {
                "Abs" => "({0} < 0n ? -{0} : {0})",
                "Max" when argCount == 2 => "({0} > {1} ? {0} : {1})",
                "Min" when argCount == 2 => "({0} < {1} ? {0} : {1})",
                "Clamp" when argCount == 3 => "({0} < {1} ? {1} : {0} > {2} ? {2} : {0})",
                "Sign" => "({0} < 0n ? -1 : {0} > 0n ? 1 : 0)",
                "IsPositive" => "({0} >= 0n)",
                "IsNegative" => "({0} < 0n)",
                "IsEvenInteger" => "({0} % 2n === 0n)",
                "IsOddInteger" => "({0} % 2n !== 0n)",
                "CopySign" when argCount == 2 => "({1} < 0n ? ({0} > 0n ? -{0} : {0}) : ({0} < 0n ? -{0} : {0}))",
                "DivRem" when argCount == 2 => "[{0} / {1}, {0} % {1}]",
                "IsPow2" => "({0} > 0n && ({0} & ({0} - 1n)) === 0n)",
                "LeadingZeroCount" => "$eq.bits.leadingZeroCount64({0})",
                "Log2" => "$eq.bits.log2Of64({0})",
                "PopCount" => "$eq.bits.popCount64({0})",
                "RotateLeft" when argCount == 2 => "$eq.bits.rotateLeft64({0}, {1})",
                "RotateRight" when argCount == 2 => "$eq.bits.rotateRight64({0}, {1})",
                "TrailingZeroCount" => "$eq.bits.trailingZeroCount64({0})",
                "MaxMagnitude" when argCount == 2 => "$eq.math.maxMagnitude({0}, {1})",
                "MinMagnitude" when argCount == 2 => "$eq.math.minMagnitude({0}, {1})",
                _ => null,
            };
        }

        if (home == SpecialType.System_Char)
        {
            return name switch
            {
                // Single-evaluation regexes: `c >= '0' && c <= '9'` would evaluate the char twice.
                "IsAsciiDigit" => "/^[0-9]$/.test({0})",
                "IsAsciiLetter" => "/^[A-Za-z]$/.test({0})",
                "IsAsciiLetterLower" => "/^[a-z]$/.test({0})",
                "IsAsciiLetterUpper" => "/^[A-Z]$/.test({0})",
                "IsAsciiLetterOrDigit" => "/^[0-9A-Za-z]$/.test({0})",
                "IsAsciiHexDigit" => "/^[0-9A-Fa-f]$/.test({0})",
                "IsAsciiHexDigitLower" => "/^[0-9a-f]$/.test({0})",
                "IsAsciiHexDigitUpper" => "/^[0-9A-F]$/.test({0})",
                "IsHighSurrogate" when argCount == 1 => "/^[\\uD800-\\uDBFF]$/.test({0})",
                "IsLowSurrogate" when argCount == 1 => "/^[\\uDC00-\\uDFFF]$/.test({0})",
                "IsSurrogate" when argCount == 1 => "/^[\\uD800-\\uDFFF]$/.test({0})",
                // A char IS a one-character string on this side already.
                "ToString" when argCount == 1 => "{0}",
                "IsBetween" when argCount == 3 => "({1} <= {0} && {0} <= {2})",
                // Parse keeps its contract, throw included — silently accepting "ab" would be a lie.
                "Parse" when argCount == 1 =>
                    "(($s) => { if ($s.length !== 1) throw new Error('String must be exactly one character long.'); return $s; })({0})",
                "ConvertFromUtf32" when argCount == 1 => "String.fromCodePoint({0})",
                // The surrogate pair IS the code point: concatenate the halves and read it back.
                "ConvertToUtf32" when argCount == 2 => "({0} + {1}).codePointAt(0)",
                // Out-of-range indexes read NaN from charCodeAt, and every comparison says no.
                "IsSurrogatePair" when argCount == 2 =>
                    "({0}.charCodeAt({1}) >= 0xD800 && {0}.charCodeAt({1}) <= 0xDBFF"
                    + " && {0}.charCodeAt({1} + 1) >= 0xDC00 && {0}.charCodeAt({1} + 1) <= 0xDFFF)",
                _ => null,
            };
        }

        if (home == SpecialType.System_String)
        {
            return name switch
            {
                // Ordinal by definition — the one string comparison with an exact JS twin.
                "CompareOrdinal" when argCount == 2 => "({0} < {1} ? -1 : {0} > {1} ? 1 : 0)",
                // The intern pool is an allocation concern; the string itself is the answer.
                "Intern" when argCount == 1 => "{0}",
                _ => null,
            };
        }

        return null;
    }

    /// <summary>Numeric members whose emission is identical for floats and small ints.</summary>
    private static string? SharedNumeric(string name, int argCount) => name switch
    {
        "Abs" => "Math.abs({0})",
        "Max" when argCount == 2 => "Math.max({0}, {1})",
        "Min" when argCount == 2 => "Math.min({0}, {1})",
        "Sign" => "Math.sign({0})",
        "Clamp" when argCount == 3 => "Math.min(Math.max({0}, {1}), {2})",
        _ => null,
    };

    /// <summary>Static constants, per primitive: null = fenced.</summary>
    private static string? ConstantTable(SpecialType home, string name) => home switch
    {
        SpecialType.System_Double or SpecialType.System_Single => name switch
        {
            "MaxValue" when home == SpecialType.System_Double => "1.7976931348623157e308",
            "MinValue" when home == SpecialType.System_Double => "-1.7976931348623157e308",
            // float constants as their EXACT double values — (double)float.MaxValue, not the
            // shortest-round-trip "3.4028235E+38" a display would show.
            "MaxValue" => "3.4028234663852886e38",
            "MinValue" => "-3.4028234663852886e38",
            "Epsilon" when home == SpecialType.System_Double => "5e-324",
            "NaN" => "NaN",
            "PositiveInfinity" => "Infinity",
            "NegativeInfinity" => "-Infinity",
            "Pi" => "Math.PI",
            "E" => "Math.E",
            "Tau" => "(Math.PI * 2)",
            _ => null,
        },
        SpecialType.System_Int32 => name switch
        {
            "MaxValue" => "2147483647",
            "MinValue" => "-2147483648",
            _ => null,
        },
        SpecialType.System_Int16 => name switch
        {
            "MaxValue" => "32767",
            "MinValue" => "-32768",
            _ => null,
        },
        SpecialType.System_Int64 => name switch
        {
            "MaxValue" => "9223372036854775807n",
            "MinValue" => "-9223372036854775808n",
            _ => null,
        },
        SpecialType.System_Byte => name switch
        {
            "MaxValue" => "255",
            "MinValue" => "0",
            _ => null,
        },
        SpecialType.System_Char => name switch
        {
            "MaxValue" => "'\\uffff'",
            "MinValue" => "'\\u0000'",
            _ => null,
        },
        SpecialType.System_Boolean => name switch
        {
            "TrueString" => "'True'",
            "FalseString" => "'False'",
            _ => null,
        },
        _ => null,
    };
}
