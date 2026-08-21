using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary><c>continue;</c>, and C# 15's labeled <c>continue label;</c> — a JavaScript label 1:1.</summary>
public class ContinueStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is ContinueStatementSyntax;
    }

    public JsStatement Convert(StatementSyntax node, ConversionContext context)
    {
        var continueStatement = (ContinueStatementSyntax)node;
        return JsStatement.Continue(continueStatement.Name?.Identifier.Text);
    }

    public int Priority => 0;
}
