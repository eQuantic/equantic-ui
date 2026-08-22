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

        // The ELEMENT converts to the loop variable's type, one item at a time — `foreach (long l
        // in ints)` makes each int a BigInt, `foreach (int code in chars)` each char its code unit,
        // `foreach (Money m in ints)` calls the type's conversion. The syntax shows none of it; the
        // bound tree reports the conversion (ForEachStatementInfo), and ValueFlow's table applies
        // it. A conversion that changes nothing on this side keeps the plain loop.
        if (ElementConversion(foreachStmt, item, context) is { } converted)
        {
            var statements = new List<JsStatement> { JsStatement.Const(item, converted) };
            statements.AddRange(body is JsBlock block ? block.Statements : new[] { body });
            return JsStatement.Headed($"{loopType} (const ${item} of {collection})", JsStatement.Block(statements));
        }
        return JsStatement.Headed($"{loopType} (const {item} of {collection})", body);
    }

    private static JsExpr? ElementConversion(ForEachStatementSyntax foreachStmt, string item, ConversionContext context)
    {
        if (context.SemanticHelper.ForEachInfo(foreachStmt) is not { } info) return null;
        var conversion = info.ElementConversion;
        if (!conversion.Exists || conversion.IsIdentity) return null;
        if (context.SemanticHelper.GetDeclaredSymbol(foreachStmt) is not ILocalSymbol variable) return null;

        var element = JsExpr.Identifier("$" + item);
        var applied = ValueFlow.Apply(conversion, info.ElementType, variable.Type, null, null, element, context);
        return JsExprWriter.Write(applied) == "$" + item ? null : applied;
    }

    public int Priority => 0;
}
