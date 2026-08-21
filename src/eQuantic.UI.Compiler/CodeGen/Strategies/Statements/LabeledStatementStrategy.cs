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
        return JsStatement.Headed($"{labeled.Identifier.Text}:", context.Converter.ConvertStatementIr(labeled.Statement));
    }

    public int Priority => 10;
}
