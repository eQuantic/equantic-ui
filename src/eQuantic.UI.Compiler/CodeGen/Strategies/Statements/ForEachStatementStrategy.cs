using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary><c>foreach (var x in xs)</c> → <c>for (const x of xs)</c>; <c>await foreach</c> →
/// <c>for await</c>. A dictionary enumerates through <c>$eq.entries</c>, since a transpiled
/// Dictionary is a plain object and not iterable.</summary>
public class ForEachStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is ForEachStatementSyntax;
    }

    public JsStatement Convert(StatementSyntax node, ConversionContext context)
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

        var body = context.Converter.ConvertStatementIr(foreachStmt.Statement);
        var loopType = foreachStmt.AwaitKeyword.Value != null ? "for await" : "for";
        return JsStatement.Headed($"{loopType} (const {item} of {collection})", body);
    }

    public int Priority => 0;
}
