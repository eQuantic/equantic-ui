using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary>
/// A C# label (<c>label: stmt;</c>) is only meaningful as a <c>goto</c> target — and <c>goto</c> itself
/// is a build error here. Drop the label and emit the inner statement so non-goto code (e.g. a label
/// left over after refactoring) still transpiles.
/// </summary>
public class LabeledStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context) => node is LabeledStatementSyntax;

    public string Convert(StatementSyntax node, ConversionContext context) =>
        context.Converter.ConvertStatement(((LabeledStatementSyntax)node).Statement);

    public int Priority => 10;
}
