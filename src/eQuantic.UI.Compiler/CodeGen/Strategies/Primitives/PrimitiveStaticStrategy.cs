using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
/// char predicates go through single-evaluation regexes, <c>IsInfinity</c> through
/// <c>Math.abs(x) === Infinity</c>. Members whose semantics JS cannot honour on those terms
/// (sign-bit <c>IsNegative</c> on double, <c>SinPi</c>, <c>FusedMultiplyAdd</c>, the Int64
/// surface riding on BigInt) are deliberately absent. <c>Parse</c>/<c>TryParse</c> stay with
/// <see cref="NumberMethodStrategy"/>.
/// </para>
/// </summary>
public class PrimitiveStaticStrategy : IConversionStrategy
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

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        if (node is MemberAccessExpressionSyntax access)
        {
            var member = context.SemanticHelper.GetSymbol(access)!;
            return ConstantTable(member.ContainingType!.SpecialType, member.Name)!;
        }

        var invocation = (InvocationExpressionSyntax)node;
        var method = (IMethodSymbol)context.SemanticHelper.GetSymbol(invocation)!;
        var args = invocation.ArgumentList.Arguments
            .Select(a => context.Converter.ConvertExpression(a.Expression))
            .ToArray();

        var emit = MethodTable(method.ContainingType.SpecialType, method.Name, args.Length)!;
        if (emit.Contains(Eq.Round)) context.UsedHelpers.Add(Eq.Import);
        return TemplateFill.With(emit, args);
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
                _ => null,
            };
        }

        if (IsSmallInteger(home))
        {
            var shared = SharedNumeric(name, argCount);
            if (shared is not null) return shared;
            return name switch
            {
                // Small ints have no -0 and no NaN, so the plain comparisons are exact.
                "IsPositive" => "({0} >= 0)",
                "IsNegative" => "({0} < 0)",
                "IsEvenInteger" => "({0} % 2 === 0)",
                "IsOddInteger" => "(Math.abs({0} % 2) === 1)",
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
                "IsBetween" when argCount == 3 => "(($c, $lo, $hi) => $lo <= $c && $c <= $hi)({0}, {1}, {2})",
                // Parse keeps its contract, throw included — silently accepting "ab" would be a lie.
                "Parse" when argCount == 1 =>
                    "(($s) => { if ($s.length !== 1) throw new Error('String must be exactly one character long.'); return $s; })({0})",
                "ConvertFromUtf32" when argCount == 1 => "String.fromCodePoint({0})",
                _ => null,
            };
        }

        if (home == SpecialType.System_String)
        {
            return name switch
            {
                // Ordinal by definition — the one string comparison with an exact JS twin.
                "CompareOrdinal" when argCount == 2 => "(($a, $b) => $a < $b ? -1 : $a > $b ? 1 : 0)({0}, {1})",
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
