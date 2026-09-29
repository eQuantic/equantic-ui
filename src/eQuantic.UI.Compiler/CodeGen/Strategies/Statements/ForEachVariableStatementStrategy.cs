using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary>
/// <c>foreach (var (k, v) in pairs)</c> — a deconstructing loop becomes <c>for (const [k, v] of …)</c>.
/// A dictionary is iterable as it is: the pairs its runtime class enumerates destructure AND answer
/// <c>.key</c>/<c>.value</c>, each key in its own type.
/// </summary>
public class ForEachVariableStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is ForEachVariableStatementSyntax;
    }

    public JsStatement Convert(StatementSyntax node, ConversionContext context)
    {
        var foreachStmt = (ForEachVariableStatementSyntax)node;
        var pattern = ConvertDesignation(foreachStmt.Variable);
        var declared = ExpressionVariableScanner.Declarations(foreachStmt.Expression, context.TypeAnnotations);
        var collection = context.Converter.ConvertExpression(foreachStmt.Expression);

        var body = context.Converter.ConvertStatementIr(foreachStmt.Statement);
        var loopType = foreachStmt.AwaitKeyword.Value != null ? "for await" : "for";
        var loop = JsStatement.Headed($"{loopType} (const {pattern} of {collection})", body);
        // What the collection expression declares is the loop's own (see ForEachStatementStrategy).
        return declared.Length == 0 ? loop : JsStatement.Block([JsStatement.Raw(declared.TrimEnd()), loop]);
    }

    private static string ConvertDesignation(ExpressionSyntax variable) => variable switch
    {
        DeclarationExpressionSyntax declaration => ExpressionVariableScanner.BindingPattern(declaration.Designation),
        TupleExpressionSyntax tuple =>
            "[" + string.Join(", ", tuple.Arguments.Select(a => ConvertDesignation(a.Expression))) + "]",
        _ => variable.ToString(),
    };

    public int Priority => 0;
}
