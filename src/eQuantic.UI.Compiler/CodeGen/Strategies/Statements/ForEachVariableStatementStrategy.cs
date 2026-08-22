using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary>
/// <c>foreach (var (k, v) in pairs)</c> — a deconstructing loop becomes <c>for (const [k, v] of …)</c>.
/// A transpiled Dictionary is a plain object — not iterable — so it enumerates through
/// <c>$eq.entries</c>, which yields pairs that destructure AND answer <c>.key</c>/<c>.value</c>,
/// with numeric keys restored as numbers.
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
        var collection = context.Converter.ConvertExpression(foreachStmt.Expression);

        if (context.SemanticHelper.GetType(foreachStmt.Expression).IsDictionaryLike(out var keyForm))
        {
            context.UsedHelpers.Add(Eq.Import);
            collection = $"$eq.entries({collection}, {keyForm})";
        }

        var body = context.Converter.ConvertStatementIr(foreachStmt.Statement);
        var loopType = foreachStmt.AwaitKeyword.Value != null ? "for await" : "for";
        return JsStatement.Headed($"{loopType} (const {pattern} of {collection})", body);
    }

    private static string ConvertDesignation(ExpressionSyntax variable) => variable switch
    {
        DeclarationExpressionSyntax { Designation: ParenthesizedVariableDesignationSyntax parenthesized } =>
            "[" + string.Join(", ", parenthesized.Variables.Select(ConvertVariableDesignation)) + "]",
        DeclarationExpressionSyntax declaration => ConvertVariableDesignation(declaration.Designation),
        TupleExpressionSyntax tuple =>
            "[" + string.Join(", ", tuple.Arguments.Select(a => ConvertDesignation(a.Expression))) + "]",
        _ => variable.ToString(),
    };

    private static string ConvertVariableDesignation(VariableDesignationSyntax designation) => designation switch
    {
        SingleVariableDesignationSyntax single => single.Identifier.Text,
        ParenthesizedVariableDesignationSyntax nested =>
            "[" + string.Join(", ", nested.Variables.Select(ConvertVariableDesignation)) + "]",
        // A discard still needs a binding name in JS; it is scoped to the loop body and unused.
        DiscardDesignationSyntax => "_",
        _ => designation.ToString(),
    };

    public int Priority => 0;
}
