using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary>A labeled statement — the target of C# 15's labeled <c>break</c>/<c>continue</c> —
/// is a JavaScript label 1:1.</summary>
public class LabeledStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context) => node is LabeledStatementSyntax;

    public JsStatement Convert(StatementSyntax node, ConversionContext context)
    {
        var labeled = (LabeledStatementSyntax)node;
        var label = $"{labeled.Identifier.Text}:";
        var inner = context.Converter.ConvertStatementIr(labeled.Statement);
        // A foreach whose collection declares a variable comes back as a BLOCK that declares it and
        // then runs the loop (ForEachStatementStrategy), since the variable is the loop's own. The
        // label goes on the loop, inside that block: on the block, a `continue label` is a
        // SyntaxError that costs the module, and a `break label` means the same either way, the loop
        // being the block's last statement.
        if (labeled.Statement is CommonForEachStatementSyntax
            && inner is JsBlock { Statements.Count: > 1 } scoped)
            return JsStatement.Block([.. scoped.Statements.Take(scoped.Statements.Count - 1),
                JsStatement.Headed(label, scoped.Statements[^1])]);
        return JsStatement.Headed(label, inner);
    }

    public int Priority => 10;
}
