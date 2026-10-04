using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// <c>checked(expr)</c> and <c>unchecked(expr)</c> are their operand. The context they set is the
/// bound tree's, and each operation in the operand already settles under it, where C# applies it: an
/// arithmetic, an increment, a compound assignment and a cast read <c>IsChecked</c> and an explicit
/// <c>unchecked</c> (<see cref="ArithmeticContext"/>, <see cref="IntegerWidth"/>, <see cref="ValueFlow"/>),
/// and throw or wrap one by one.
/// <para>
/// This wrapped the operand again, in an arrow invoked in place that checked the result against
/// int's range and threw an Error whose message was the word "OverflowException": a second check of
/// what the operations had already checked, and an <c>await</c> in the operand landed in that arrow,
/// which is not async, so <c>checked(await F() + 1)</c> did not parse (#539).
/// </para>
/// </summary>
public class CheckedExpressionStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context) => node is CheckedExpressionSyntax;

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context) =>
        context.Converter.ConvertIr(((CheckedExpressionSyntax)node).Expression);

    public int Priority => 10;
}
