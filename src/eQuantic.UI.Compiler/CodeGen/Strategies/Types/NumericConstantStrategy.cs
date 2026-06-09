using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Maps numeric type constants (int.MaxValue, double.Epsilon, etc.) to their literal JS value.
/// Without this they are emitted verbatim (e.g. `int.MaxValue`) and crash at runtime.
/// </summary>
/// <remarks>
/// long/ulong limits are emitted as BigInt literals (exact). decimal MaxValue/MinValue are not mapped
/// here — they belong to the Decimal compat type (see DOTNET-COVERAGE-PROGRAM.md).
/// </remarks>
public class NumericConstantStrategy : IConversionStrategy
{
    private static readonly Dictionary<string, string> Constants = new()
    {
        ["int.MaxValue"] = "2147483647",
        ["int.MinValue"] = "-2147483648",
        ["Int32.MaxValue"] = "2147483647",
        ["Int32.MinValue"] = "-2147483648",
        ["uint.MaxValue"] = "4294967295",
        ["uint.MinValue"] = "0",
        ["UInt32.MaxValue"] = "4294967295",
        ["UInt32.MinValue"] = "0",
        ["short.MaxValue"] = "32767",
        ["short.MinValue"] = "-32768",
        ["Int16.MaxValue"] = "32767",
        ["Int16.MinValue"] = "-32768",
        ["ushort.MaxValue"] = "65535",
        ["ushort.MinValue"] = "0",
        ["UInt16.MaxValue"] = "65535",
        ["UInt16.MinValue"] = "0",
        ["byte.MaxValue"] = "255",
        ["byte.MinValue"] = "0",
        ["Byte.MaxValue"] = "255",
        ["Byte.MinValue"] = "0",
        ["sbyte.MaxValue"] = "127",
        ["sbyte.MinValue"] = "-128",
        ["SByte.MaxValue"] = "127",
        ["SByte.MinValue"] = "-128",
        ["double.MaxValue"] = "1.7976931348623157e308",
        ["double.MinValue"] = "-1.7976931348623157e308",
        ["double.Epsilon"] = "5e-324",
        ["Double.MaxValue"] = "1.7976931348623157e308",
        ["Double.MinValue"] = "-1.7976931348623157e308",
        ["Double.Epsilon"] = "5e-324",
        ["double.NaN"] = "NaN",
        ["Double.NaN"] = "NaN",
        ["double.PositiveInfinity"] = "Infinity",
        ["Double.PositiveInfinity"] = "Infinity",
        ["double.NegativeInfinity"] = "-Infinity",
        ["Double.NegativeInfinity"] = "-Infinity",
        ["float.MaxValue"] = "3.4028234663852886e38",
        ["float.MinValue"] = "-3.4028234663852886e38",
        ["Single.MaxValue"] = "3.4028234663852886e38",
        ["Single.MinValue"] = "-3.4028234663852886e38",
        // 64-bit limits as BigInt literals (exact; they exceed JS Number precision).
        ["long.MaxValue"] = "9223372036854775807n",
        ["long.MinValue"] = "-9223372036854775808n",
        ["Int64.MaxValue"] = "9223372036854775807n",
        ["Int64.MinValue"] = "-9223372036854775808n",
        ["ulong.MaxValue"] = "18446744073709551615n",
        ["ulong.MinValue"] = "0n",
        ["UInt64.MaxValue"] = "18446744073709551615n",
        ["UInt64.MinValue"] = "0n",
    };

    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is MemberAccessExpressionSyntax memberAccess && Constants.ContainsKey(memberAccess.ToString());
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        return Constants[((MemberAccessExpressionSyntax)node).ToString()];
    }

    // Above the generic MemberAccessStrategy (Priority 0) so these win.
    public int Priority => 20;
}
