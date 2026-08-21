using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary><c>checked</c>/<c>unchecked</c> blocks: JavaScript has no overflow context, so the
/// block alone.</summary>
public class CheckedStatementStrategy : IStatementIrStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is CheckedStatementSyntax;
    }

    public JsStatement ConvertIr(StatementSyntax node, ConversionContext context) =>
        context.Converter.ConvertBlockIr(((CheckedStatementSyntax)node).Block);

    public int Priority => 10;
}
