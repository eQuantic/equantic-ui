using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary><c>for (init; condition; incrementors) body</c>, 1:1.</summary>
public class ForStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is ForStatementSyntax;
    }

    public JsStatement Convert(StatementSyntax node, ConversionContext context)
    {
        var forStmt = (ForStatementSyntax)node;
        var declaration = ConvertDeclaration(forStmt, Declared(forStmt), context);
        var condition = forStmt.Condition != null
            ? context.Converter.ConvertExpression(forStmt.Condition)
            : "";
        var incrementors = string.Join(", ",
            forStmt.Incrementors.Select(i => context.Converter.ConvertExpression(i)));
        var body = context.Converter.ConvertStatementIr(forStmt.Statement);
        return JsStatement.Headed($"for ({declaration}; {condition}; {incrementors})", body);
    }

    /// <summary>
    /// The variables the head's expressions declare — <c>for (…; int.TryParse(xs[i], out var n); …)</c>.
    /// They go in the head's own <c>let</c>, which is the only place with the loop's scope (Roslyn
    /// keeps them inside the statement) and the only one JavaScript copies for each iteration, as
    /// .NET gives the condition a fresh variable every time round: a closure made in the body keeps
    /// its own iteration's value (.NET 12; one slot in front of the loop answered 22).
    /// </summary>
    private static IReadOnlyList<string> Declared(ForStatementSyntax forStmt) =>
        (forStmt.Declaration?.Variables.Select(v => v.Initializer?.Value) ?? [])
            .Concat(forStmt.Initializers)
            .Append(forStmt.Condition)
            .Concat(forStmt.Incrementors)
            .SelectMany(ExpressionVariableScanner.Names)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static string ConvertDeclaration(ForStatementSyntax forStmt, IReadOnlyList<string> declared,
        ConversionContext context)
    {
        // Declared FIRST, so a declarator or an initializer that assigns one of them finds it bound
        // rather than in its temporal dead zone.
        var names = declared.Count == 0 ? "" : ExpressionVariableScanner.List(declared, context.TypeAnnotations);

        // for (int i = 0; ...)
        if (forStmt.Declaration != null)
        {
            var variables = forStmt.Declaration.Variables
                .Select(v =>
                {
                    var name = v.Identifier.Text.ToJsIdentifier();
                    var initializer = v.Initializer != null
                        ? context.Converter.ConvertExpression(v.Initializer.Value)
                        : "undefined";
                    return $"{name} = {initializer}";
                });
            return $"let {string.Join(", ", declared.Count == 0 ? variables : variables.Prepend(names))}";
        }

        // for (i = 0; ...)
        if (forStmt.Initializers.Count > 0)
        {
            var initializers = string.Join(", ",
                forStmt.Initializers.Select(i => context.Converter.ConvertExpression(i)));
            // A head holds a declaration OR expressions, never both: the expressions become the
            // initializer of one more binding, which runs them once, after the names exist.
            // `$` cannot begin a C# identifier, so the binding shadows nothing the author wrote.
            return declared.Count == 0 ? initializers : $"let {names}, $init = void ({initializers})";
        }

        return declared.Count == 0 ? "" : $"let {names}";
    }

    public int Priority => 0;
}
