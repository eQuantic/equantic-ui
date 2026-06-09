using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Maps the <c>checked(expr)</c> / <c>unchecked(expr)</c> expressions to faithful 32-bit integer
/// overflow behavior (JS numbers are float64 and don't wrap on their own):
/// <list type="bullet">
/// <item><c>unchecked</c> — wrap to the type width (<c>(expr) | 0</c> for signed, <c>(expr) &gt;&gt;&gt; 0</c> for unsigned).</item>
/// <item><c>checked</c> — throw <c>OverflowException</c> when the result leaves the type's range.</item>
/// </list>
/// Only 32-bit integer expressions are affected — <c>long</c>/<c>ulong</c> are exact BigInt (no JS
/// overflow) and floating/other types don't overflow to an exception, so they pass through unchanged.
/// (Block-level <c>checked { }</c> contexts are not enforced; use the expression form.)
/// </summary>
public class CheckedExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context) => node is CheckedExpressionSyntax;

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var expr = (CheckedExpressionSyntax)node;
        var inner = context.Converter.ConvertExpression(expr.Expression);
        var special = context.SemanticHelper.GetType(expr.Expression).UnwrapNullable()?.SpecialType;

        var (min, max, wrap) = special switch
        {
            SpecialType.System_Int32 or SpecialType.System_Int16 or SpecialType.System_SByte
                => ("-2147483648", "2147483647", "| 0"),
            SpecialType.System_UInt32 or SpecialType.System_UInt16 or SpecialType.System_Byte
                => ("0", "4294967295", ">>> 0"),
            _ => (null, null, null),
        };
        if (wrap == null) return inner; // not a 32-bit integer expression — nothing to wrap/check

        if (node.IsKind(SyntaxKind.CheckedExpression))
        {
            return $"(() => {{ const _v = {inner}; " +
                   $"if (_v > {max} || _v < {min}) throw new Error('OverflowException'); return _v; }})()";
        }

        // unchecked: wrap to the integer width.
        return $"(({inner}) {wrap})";
    }

    public int Priority => 10;
}
