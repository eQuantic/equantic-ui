using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;
using eQuantic.UI.Compiler.CodeGen.Extensions;

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
        var item = foreachStmt.Identifier.Text.ToJsIdentifier();
        var collection = context.Converter.ConvertExpression(foreachStmt.Expression);
        // See ForEachVariableStatementStrategy: dictionaries enumerate through $eq.entries.
        if (context.SemanticHelper.GetType(foreachStmt.Expression).IsDictionaryLike(out var numericKey))
        {
            context.UsedHelpers.Add(Eq.Import);
            collection = $"$eq.entries({collection}, {(numericKey ? "true" : "false")})";
        }
        var body = context.Converter.Convert(foreachStmt.Statement);
        
        var isAsync = foreachStmt.AwaitKeyword.Value != null;
        var loopType = isAsync ? "for await" : "for";

        return $"{loopType} (const {item} of {collection}) {body}";
    }

    public int Priority => 0;
}
