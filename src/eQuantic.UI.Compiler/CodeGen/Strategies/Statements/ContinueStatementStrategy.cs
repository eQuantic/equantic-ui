using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary>
/// Strategy for continue statements: plain <c>continue;</c>, and C# 15's labeled form
/// (<c>continue outer;</c>) — JavaScript has the identical construct, so the label rides through
/// verbatim (the label itself is emitted by <see cref="LabeledStatementStrategy"/>).
/// </summary>
public class ContinueStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is ContinueStatementSyntax;
    }

    public string Convert(StatementSyntax node, ConversionContext context)
    {
        var continueStatement = (ContinueStatementSyntax)node;
        return continueStatement.Name is { } label ? $"continue {label.Identifier.Text};" : "continue;";
    }

    public int Priority => 0;
}
