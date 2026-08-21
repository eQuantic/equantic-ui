using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary><c>break;</c>, and C# 15's labeled <c>break label;</c> — a JavaScript label 1:1.</summary>
public class BreakStatementStrategy : IStatementIrStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is BreakStatementSyntax;
    }

    public JsStatement ConvertIr(StatementSyntax node, ConversionContext context)
    {
        var breakStatement = (BreakStatementSyntax)node;
        return JsStatement.Break(breakStatement.Name?.Identifier.Text);
    }

    public int Priority => 0;
}
