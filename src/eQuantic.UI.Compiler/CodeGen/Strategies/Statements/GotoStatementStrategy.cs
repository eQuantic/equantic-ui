using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary>
/// <c>goto</c> (incl. <c>goto case</c>/<c>goto default</c>) has no JavaScript equivalent — JS has no
/// arbitrary jumps. Rather than emit <c>goto …</c> verbatim (a bundle-time syntax error), raise a clear
/// build error so the developer restructures with loops/conditionals. Low priority so a real strategy
/// (should one ever exist) would outrank this.
/// </summary>
public class GotoStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context) => node is GotoStatementSyntax;

    public string Convert(StatementSyntax node, ConversionContext context)
    {
        context.Report(node, ConversionSeverity.Error, "EQ2002",
            "C# 'goto' cannot be transpiled to JavaScript — it has no equivalent. Restructure the logic with loops/conditionals.");
        return "/* goto unsupported */";
    }

    public int Priority => 2;
}
