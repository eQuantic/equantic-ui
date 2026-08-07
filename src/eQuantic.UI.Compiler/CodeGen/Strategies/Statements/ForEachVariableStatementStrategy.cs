using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;
using eQuantic.UI.Compiler.CodeGen.Extensions;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary>
/// <c>foreach (var (a, b) in pairs)</c> — DECONSTRUCTING foreach (Roslyn's
/// <see cref="ForEachVariableStatementSyntax"/>, a different node type from the plain
/// <see cref="ForEachStatementSyntax"/>). Tuples transpile to JS arrays, so the C# deconstruction
/// pattern maps exactly onto JS array destructuring: <c>for (const [a, b] of pairs)</c>.
///
/// Nesting works because the emitter is recursive over the designation tree —
/// <c>foreach (var (a, (b, c)) in …)</c> becomes <c>for (const [a, [b, c]] of …)</c>. A discard
/// (<c>_</c>) stays a named binding: JS has no discard, and the name is unobservable outside the
/// loop body.
/// </summary>
public class ForEachVariableStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is ForEachVariableStatementSyntax;
    }

    public string Convert(StatementSyntax node, ConversionContext context)
    {
        var foreachStmt = (ForEachVariableStatementSyntax)node;
        var pattern = ConvertDesignation(foreachStmt.Variable);
        var collection = context.Converter.ConvertExpression(foreachStmt.Expression);
        // A transpiled Dictionary is a plain object — not iterable. $eq.entries yields pairs that
        // destructure AND answer .key/.value, with numeric keys restored as numbers.
        if (context.SemanticHelper.GetType(foreachStmt.Expression).IsDictionaryLike(out var numericKey))
        {
            context.UsedHelpers.Add(Eq.Import);
            collection = $"$eq.entries({collection}, {(numericKey ? "true" : "false")})";
        }
        var body = context.Converter.Convert(foreachStmt.Statement);

        var isAsync = foreachStmt.AwaitKeyword.Value != null;
        var loopType = isAsync ? "for await" : "for";

        return $"{loopType} (const {pattern} of {collection}) {body}";
    }

    /// <summary>
    /// The loop variable as a JS destructuring pattern. Roslyn models
    /// <c>var (a, b)</c> as a DeclarationExpression carrying a ParenthesizedVariableDesignation,
    /// and <c>(var a, var b)</c> as a TupleExpression of DeclarationExpressions — both spellings
    /// are legal C# for the same thing, so both are handled.
    /// </summary>
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
