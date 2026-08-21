using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary><c>fixed</c> pins memory, which has no meaning here: the body alone, marked.</summary>
public class FixedStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is FixedStatementSyntax;
    }

    public JsStatement Convert(StatementSyntax node, ConversionContext context) =>
        JsStatement.Sequence(
            JsStatement.Raw("/* fixed statement unwrapped */"),
            context.Converter.ConvertStatementIr(((FixedStatementSyntax)node).Statement));

    public int Priority => 10;
}
