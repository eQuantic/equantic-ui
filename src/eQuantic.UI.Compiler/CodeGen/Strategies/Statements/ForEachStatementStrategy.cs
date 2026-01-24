using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

public class ForEachStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is ForEachStatementSyntax;
    }

    public string Convert(StatementSyntax node, ConversionContext context)
    {
        var foreachStmt = (ForEachStatementSyntax)node;
        var item = foreachStmt.Identifier.Text;
        var collection = context.Converter.ConvertExpression(foreachStmt.Expression);
        var body = context.Converter.Convert(foreachStmt.Statement);
        
        return $"for (const {item} of {collection}) {body}";
    }

    public int Priority => 0;
}
