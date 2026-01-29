using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary>
/// Strategy for classic for loop statements.
/// Handles:
/// - for (int i = 0; i &lt; n; i++) { ... }
/// </summary>
public class ForStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is ForStatementSyntax;
    }

    public string Convert(StatementSyntax node, ConversionContext context)
    {
        var forStmt = (ForStatementSyntax)node;

        // Convert declaration or initializers
        var declaration = ConvertDeclaration(forStmt, context);

        // Convert condition
        var condition = forStmt.Condition != null
            ? context.Converter.ConvertExpression(forStmt.Condition)
            : "";

        // Convert incrementors
        var incrementors = string.Join(", ",
            forStmt.Incrementors.Select(i => context.Converter.ConvertExpression(i)));

        // Convert body
        var body = context.Converter.Convert(forStmt.Statement);

        return $"for ({declaration}; {condition}; {incrementors}) {body}";
    }

    private string ConvertDeclaration(ForStatementSyntax forStmt, ConversionContext context)
    {
        // Handle variable declaration: for (int i = 0; ...)
        if (forStmt.Declaration != null)
        {
            var variables = forStmt.Declaration.Variables
                .Select(v =>
                {
                    var name = v.Identifier.Text;
                    var initializer = v.Initializer != null
                        ? context.Converter.ConvertExpression(v.Initializer.Value)
                        : "undefined";
                    return $"{name} = {initializer}";
                });
            return $"let {string.Join(", ", variables)}";
        }

        // Handle initializer expressions: for (i = 0; ...)
        if (forStmt.Initializers.Count > 0)
        {
            return string.Join(", ",
                forStmt.Initializers.Select(i => context.Converter.ConvertExpression(i)));
        }

        return "";
    }

    public int Priority => 0;
}
