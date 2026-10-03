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
        if (forStmt.Declaration is not null && CapturesALoopVariable(forStmt, context))
            return HoistedLoop(forStmt, context);
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
    /// <c>for (int i = 0; …)</c> declares ONE variable for the whole loop, and a closure made in the
    /// body reads that variable, so after the loop every one of them sees its last value. A head's
    /// <c>let</c> is copied into each iteration in JavaScript, so each closure kept its own: three
    /// closures over <c>i</c> answered 0, 1 and 2 where .NET answers 3, 3 and 3 (#476). When a closure
    /// captures one, the declaration moves in front of the loop, in a block of its own so a second
    /// loop declaring the same name stays legal. What its initializers declare (<c>out var n</c>)
    /// goes with it, declared first as the head declares it, being one for the whole loop as well.
    /// The head keeps what its condition and incrementors declare, which .NET does give a fresh
    /// variable each time round.
    /// </summary>
    private static JsStatement HoistedLoop(ForStatementSyntax forStmt, ConversionContext context)
    {
        var declaration = forStmt.Declaration!;
        var once = declaration.Variables
            .Select(v => v.Initializer?.Value)
            .SelectMany(ExpressionVariableScanner.Names)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var variables = declaration.Variables.Select(v =>
            $"{v.Identifier.Text.ToJsIdentifier()} = "
            + (v.Initializer != null ? context.Converter.ConvertExpression(v.Initializer.Value) : "undefined"));
        var hoisted = once.Count == 0
            ? variables
            : variables.Prepend(ExpressionVariableScanner.List(once, context.TypeAnnotations));
        var eachTime = Declared(forStmt).Except(once, StringComparer.Ordinal).ToList();
        var head = eachTime.Count == 0 ? "" : $"let {ExpressionVariableScanner.List(eachTime, context.TypeAnnotations)}";
        var condition = forStmt.Condition != null ? context.Converter.ConvertExpression(forStmt.Condition) : "";
        var incrementors = string.Join(", ", forStmt.Incrementors.Select(i => context.Converter.ConvertExpression(i)));
        var body = context.Converter.ConvertStatementIr(forStmt.Statement);
        return JsStatement.Block(
        [
            JsStatement.Raw($"let {string.Join(", ", hoisted)};"),
            JsStatement.Headed($"for ({head}; {condition}; {incrementors})", body),
        ]);
    }

    /// <summary>Whether a lambda, an anonymous method or a local function anywhere in the loop reads
    /// a variable its declaration declares, which the bound tree answers by symbol.</summary>
    private static bool CapturesALoopVariable(ForStatementSyntax forStmt, ConversionContext context)
    {
        var declaration = forStmt.Declaration!;
        var loopVariables = declaration.Variables
            .Select(v => context.SemanticHelper.GetDeclaredSymbol(v))
            .Where(symbol => symbol is not null)
            .ToHashSet(SymbolEqualityComparer.Default);
        if (loopVariables.Count == 0) return false;
        // Only a name spelled like one of them can read one, so the model is asked about those alone.
        var names = declaration.Variables.Select(v => v.Identifier.ValueText).ToHashSet(StringComparer.Ordinal);
        return forStmt.DescendantNodes()
            .Where(n => n is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
            .SelectMany(function => function.DescendantNodes().OfType<IdentifierNameSyntax>())
            .Where(name => names.Contains(name.Identifier.ValueText))
            .Any(name => context.SemanticHelper.GetSymbol(name) is { } read && loopVariables.Contains(read));
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
                    // The name every reference reads (ToJsIdentifier), `@class` renamed.
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
            var converted = forStmt.Initializers.Select(i => context.Converter.ConvertExpression(i)).ToList();
            if (declared.Count == 0) return string.Join(", ", converted);

            // A head holds a declaration OR expressions, never both, and the names need its `let`.
            // A deconstruction that declares itself (`var (i, j) = (0, 3)`) comes back as a `let` of
            // its own, which is one more declarator of the head's.
            var deconstructions = forStmt.Initializers
                .Select((initializer, i) => converted[i].StartsWith(LetPrefix, StringComparison.Ordinal)
                    ? DeclaredDeconstruction(initializer)
                    : null)
                .ToList();
            if (deconstructions.All(designation => designation is not null))
                return $"let {names}, {string.Join(", ", converted.Select(text => text[LetPrefix.Length..]))}";

            // Otherwise the initializers become the initializer of one more binding, which runs them
            // once, in order, after the names exist: a deconstruction among them assigns names the
            // head declares. `$` cannot begin a C# identifier, so the binding shadows nothing.
            var assigned = declared
                .Concat(deconstructions.Where(d => d is not null).SelectMany(d => ExpressionVariableScanner.Designated(d!)))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var expressions = converted.Select((text, i) => deconstructions[i] is null ? text : $"({text[LetPrefix.Length..]})");
            return $"let {ExpressionVariableScanner.List(assigned, context.TypeAnnotations)}, $init = void ({string.Join(", ", expressions)})";
        }

        return declared.Count == 0 ? "" : $"let {names}";
    }

    /// <summary>What a deconstruction declaration's assignment strategy writes in front of it.</summary>
    private const string LetPrefix = "let ";

    /// <summary>The designation of an initializer that is a deconstruction declaring its own names
    /// (<c>var (i, j) = (0, 3)</c>), which converts to <c>let [i, j] = …</c>; null for any other.</summary>
    private static VariableDesignationSyntax? DeclaredDeconstruction(ExpressionSyntax initializer) =>
        initializer is AssignmentExpressionSyntax
        {
            Left: DeclarationExpressionSyntax { Designation: ParenthesizedVariableDesignationSyntax designation },
        }
            ? designation
            : null;

    public int Priority => 0;
}
