using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

public class SwitchStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is SwitchStatementSyntax;
    }

    public string Convert(StatementSyntax node, ConversionContext context)
    {
        var switchStmt = (SwitchStatementSyntax)node;
        var expr = context.Converter.ConvertExpression(switchStmt.Expression);
        
        var sb = new StringBuilder();
        sb.Append($"switch ({expr}) {{");
        
        foreach (var section in switchStmt.Sections)
        {
            foreach (var label in section.Labels)
            {
                if (label is CaseSwitchLabelSyntax caseLabel)
                {
                    var caseValue = context.Converter.ConvertExpression(caseLabel.Value);
                    sb.Append($" case {caseValue}:");
                }
                else if (label is DefaultSwitchLabelSyntax)
                {
                    sb.Append(" default:");
                }
            }
            
            // Convert statements in the section
            // Note: C# switch sections contain a list of statements. 
            // We should put them in a block or just append them if JS allows.
            // JS switch cases fall through unless break; is present. C# enforces break/return.
            // We will just emit the statements.
            
            foreach (var stmt in section.Statements)
            {
                 sb.Append(" " + context.Converter.Convert(stmt));
            }
        }
        
        sb.Append(" }");
        return sb.ToString();
    }

    public int Priority => 0;
}
