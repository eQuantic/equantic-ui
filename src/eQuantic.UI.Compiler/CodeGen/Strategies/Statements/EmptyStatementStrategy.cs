using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary>
/// A lone <c>;</c>. It means nothing, and the faithful translation of nothing is nothing —
/// before this it raised EQ1002 and FAILED the build, which turned a stray semicolon into a
/// transpilation error. Surfaced by the syntax-surface enumeration.
/// </summary>
public class EmptyStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context) => node is EmptyStatementSyntax;

    public string Convert(StatementSyntax node, ConversionContext context) => "";

    public int Priority => 0;
}
