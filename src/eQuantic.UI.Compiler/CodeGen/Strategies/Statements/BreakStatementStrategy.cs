using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary>
/// Strategy for break statements: plain <c>break;</c>, and C# 15's labeled form
/// (<c>break outer;</c>) — JavaScript has the identical construct, so the label rides through
/// verbatim (the label itself is emitted by <see cref="LabeledStatementStrategy"/>).
/// </summary>
public class BreakStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is BreakStatementSyntax;
    }

    public string Convert(StatementSyntax node, ConversionContext context)
    {
        var breakStatement = (BreakStatementSyntax)node;
        return breakStatement.Name is { } label ? $"break {label.Identifier.Text};" : "break;";
    }

    public int Priority => 0;
}
