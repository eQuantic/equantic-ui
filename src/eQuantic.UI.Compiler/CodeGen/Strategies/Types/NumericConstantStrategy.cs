using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Maps numeric type constants (int.MaxValue, double.Epsilon, etc.) to their literal JS value.
/// Without this they are emitted verbatim (e.g. `int.MaxValue`) and crash at runtime.
/// </summary>
/// <remarks>
/// long/ulong/decimal MaxValue/MinValue exceed JS Number precision and are intentionally not mapped
/// here — they belong to the BigInt/Decimal compat work (see DOTNET-COVERAGE-PROGRAM.md).
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
