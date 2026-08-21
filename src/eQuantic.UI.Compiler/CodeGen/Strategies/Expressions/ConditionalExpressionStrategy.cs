using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// The ternary <c>c ? t : f</c>. C# and JavaScript agree on where it binds, so the node exists to
/// let the writer protect it when a MIGRATED operator wraps it — a ternary spliced raw into
/// arithmetic regroups silently.
/// </summary>
public class ConditionalExpressionStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is ConditionalExpressionSyntax;
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var conditional = (ConditionalExpressionSyntax)node;
        return JsExpr.Conditional(
            context.Converter.ConvertIr(conditional.Condition),
            context.Converter.ConvertIr(conditional.WhenTrue),
            context.Converter.ConvertIr(conditional.WhenFalse));
    }

    public int Priority => 10;
}
