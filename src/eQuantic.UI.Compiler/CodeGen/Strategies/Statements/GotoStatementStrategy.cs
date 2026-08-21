using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary><c>goto</c> has no JavaScript equivalent: a build error, never a guess.</summary>
public class GotoStatementStrategy : IStatementIrStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context) => node is GotoStatementSyntax;

    public JsStatement ConvertIr(StatementSyntax node, ConversionContext context)
    {
        context.Report(node, ConversionSeverity.Error, "EQ2002",
            "C# 'goto' cannot be transpiled to JavaScript — it has no equivalent. Restructure the logic with loops/conditionals.");
        return JsStatement.Raw("/* goto unsupported */");
    }

    public int Priority => 2;
}
