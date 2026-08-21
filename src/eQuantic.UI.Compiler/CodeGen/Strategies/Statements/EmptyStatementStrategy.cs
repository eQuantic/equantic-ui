using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary>The empty statement <c>;</c> emits nothing at all.</summary>
public class EmptyStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context) => node is EmptyStatementSyntax;

    public JsStatement Convert(StatementSyntax node, ConversionContext context) => JsStatement.Empty;

    public int Priority => 0;
}
