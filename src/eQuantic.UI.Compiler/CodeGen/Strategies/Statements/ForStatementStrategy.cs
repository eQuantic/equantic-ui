using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary><c>for (init; condition; incrementors) body</c>, 1:1.</summary>
public class ForStatementStrategy : IStatementIrStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is ForStatementSyntax;
    }

    public JsStatement ConvertIr(StatementSyntax node, ConversionContext context)
    {
        var forStmt = (ForStatementSyntax)node;
        var declaration = ConvertDeclaration(forStmt, context);
        var condition = forStmt.Condition != null
            ? context.Converter.ConvertExpression(forStmt.Condition)
            : "";
        var incrementors = string.Join(", ",
            forStmt.Incrementors.Select(i => context.Converter.ConvertExpression(i)));
        var body = context.Converter.ConvertStatementIr(forStmt.Statement);
        return JsStatement.Headed($"for ({declaration}; {condition}; {incrementors})", body);
    }

    private static string ConvertDeclaration(ForStatementSyntax forStmt, ConversionContext context)
    {
        // for (int i = 0; ...)
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

        // for (i = 0; ...)
        if (forStmt.Initializers.Count > 0)
        {
            return string.Join(", ",
                forStmt.Initializers.Select(i => context.Converter.ConvertExpression(i)));
        }

        return "";
    }

    public int Priority => 0;
}
