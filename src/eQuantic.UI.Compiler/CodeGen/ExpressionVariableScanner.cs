using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// The variables a C# expression DECLARES, which the emitted JavaScript assigns without declaring:
/// what C# calls expression variables. A pattern binds them (<c>x is T t</c>, <c>{ Y: var y }</c>, a
/// list or positional designation), an <c>out var n</c> argument declares one where the call's
/// result lands, and each element of a deconstruction does (<c>(var c, var d) = (3, 4)</c> is written
/// <c>[c, d] = [3, 4]</c>). <c>var (a, b) = …</c> and a foreach's own variables declare themselves
/// (<c>let [a, b] = …</c>, <c>for (const [a, b] of …)</c>), so they are left to their statement.
/// <para>
/// ONE owner, and whoever writes the code the expression runs in asks it, with C#'s scope: a
/// statement whose variables live on in the enclosing block (an expression statement, an
/// <c>if</c>, a <c>return</c>, a declaration, a <c>switch</c>, a <c>lock</c>) declares them in front
/// of itself, or leaves them to the switch whose section it stands in; a loop or a <c>using</c>,
/// whose variables Roslyn keeps inside the statement, declares them inside; an initializer, which
/// runs outside any statement, declares them in its own arrow.
/// Before this there were four answers. Pattern variables were declared by the statement; an
/// <c>out var</c> by a <c>let</c> at the top of a METHOD, so every iteration of a loop and every
/// call of a recursive local function shared one slot (.NET 12, JavaScript 22); a deconstruction's
/// elements by nobody; and the harness declared them itself, which is why none of it showed.
/// </para>
/// <para>
/// A lambda, an anonymous method and a local function are not walked: their bodies are their own
/// scope and their own statements declare what they bind, per call. Nor is a switch expression's
/// ARM, whose scope is the arrow that strategy emits: it asks for each arm itself. Nor is a query's
/// body: C# scopes a clause's variables to the clause, and each clause becomes an arrow that
/// declares its own, so two queries in one block may bind the same name.
/// </para>
/// </summary>
public static class ExpressionVariableScanner
{
    /// <summary>
    /// <c>let a: any; let b: any; </c> for every variable <paramref name="expression"/> declares, or
    /// an empty string when there is none.
    /// <para>
    /// <paramref name="typeAnnotations"/> has no default on purpose. The same converter also emits
    /// plain <c>.js</c> — for the conformance harness, the playground and the design host — and
    /// there <c>let x: any;</c> is a SYNTAX error that costs the whole module, not a type that gets
    /// ignored. A default would mean the next call site gets it wrong silently. In TypeScript the
    /// annotation is required: these are assigned inside the condition or the arrow that binds
    /// them, TypeScript cannot see through that, and an untyped slot reads as "implicitly any in
    /// some locations", which is an error, where an explicit one is a decision.
    /// </para>
    /// </summary>
    public static string Declarations(ExpressionSyntax? expression, bool typeAnnotations) =>
        Declarations(Names(expression), typeAnnotations);

    /// <summary>As <see cref="Declarations(ExpressionSyntax?, bool)"/>, for names gathered from more
    /// than one expression into one scope — a switch expression's arms, a loop's head.</summary>
    public static string Declarations(IEnumerable<string> names, bool typeAnnotations)
    {
        var slot = typeAnnotations ? ": any" : "";
        var declared = names.Distinct(StringComparer.Ordinal).Select(name => $"let {name}{slot};").ToList();
        return declared.Count == 0 ? "" : string.Join(" ", declared) + " ";
    }

    /// <summary>The same names as a declaration LIST, <c>a: any, b: any</c>, for a head that already
    /// has its own <c>let</c>: a <c>for</c> declares them there so that each iteration has its own, as
    /// .NET gives a loop's condition a fresh variable every time round.</summary>
    public static string List(IEnumerable<string> names, bool typeAnnotations)
    {
        var slot = typeAnnotations ? ": any" : "";
        return string.Join(", ", names.Distinct(StringComparer.Ordinal).Select(name => name + slot));
    }

    /// <summary>
    /// The declarations a statement writes in FRONT of itself for <paramref name="expression"/>, one
    /// of the expressions whose variables C# puts in the enclosing block (see
    /// <see cref="BlockNames"/>). Nothing when the statement stands directly in a switch section: C#
    /// scopes those to the whole switch block, which a later section can assign, so the switch
    /// declares them once for all of its sections. A <c>let</c> in the section it was written in
    /// was in its temporal dead zone for every other one.
    /// </summary>
    public static string InFrontOf(StatementSyntax statement, ExpressionSyntax? expression, bool typeAnnotations) =>
        statement.Parent is SwitchSectionSyntax ? "" : Declarations(expression, typeAnnotations);

    /// <summary>
    /// The names a statement declares into the block it stands in, which Roslyn measured for each
    /// kind: an expression statement's, an <c>if</c>'s condition's, a <c>return</c>'s, a
    /// <c>throw</c>'s, a <c>yield return</c>'s, a declaration's initializers', a <c>lock</c>'s and a
    /// <c>switch</c>'s governing expression's. A loop's and a <c>using</c>'s stay inside the
    /// statement, so they are none of the block's.
    /// </summary>
    public static IReadOnlyList<string> BlockNames(StatementSyntax statement)
    {
        IEnumerable<ExpressionSyntax?> expressions = statement switch
        {
            ExpressionStatementSyntax expressionStatement => [expressionStatement.Expression],
            IfStatementSyntax ifStatement => [ifStatement.Condition],
            ReturnStatementSyntax returnStatement => [returnStatement.Expression],
            ThrowStatementSyntax throwStatement => [throwStatement.Expression],
            YieldStatementSyntax yieldStatement => [yieldStatement.Expression],
            LocalDeclarationStatementSyntax declaration =>
                declaration.Declaration.Variables.Select(variable => variable.Initializer?.Value),
            LockStatementSyntax lockStatement => [lockStatement.Expression],
            SwitchStatementSyntax switchStatement => [switchStatement.Expression],
            _ => [],
        };
        return expressions.SelectMany(Names).Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// <paramref name="converted"/>, the JavaScript of an expression that runs OUTSIDE any statement
    /// — a field's or a property's initializer, a prop's default — in an arrow that declares what
    /// <paramref name="expression"/> declares, or unchanged when it declares nothing:
    /// <c>(() => { let n; return (n = …) ? n : 0; })()</c>. Nothing else is in a position to declare
    /// them, and each initializer is its own C# scope, so two of them may bind the same name: the
    /// constructor they are written into cannot take both as siblings. An arrow keeps <c>this</c>.
    /// </summary>
    public static string Scoped(ExpressionSyntax? expression, string converted, bool typeAnnotations)
    {
        var declared = Declarations(expression, typeAnnotations);
        return declared.Length == 0 ? converted : $"(() => {{ {declared}return {converted}; }})()";
    }

    /// <summary>The JavaScript names of the variables <paramref name="expression"/> declares, in the
    /// order they are written, each once. A name goes through
    /// <see cref="StringExtensions.ToJsIdentifier"/>, as every reference to it does.</summary>
    public static IReadOnlyList<string> Names(ExpressionSyntax? expression)
    {
        if (expression is null) return [];
        // A lambda is its OWN scope, and Walk only skips lambdas it finds as children — handed one
        // directly it walked straight into the body and hoisted a binding out of the lambda that
        // owns it, into the enclosing method.
        if (expression is LambdaExpressionSyntax or AnonymousFunctionExpressionSyntax) return [];
        var names = new List<string>();
        // The expression ITSELF, then its children: Walk only inspects children, so a condition
        // that IS the pattern (`if (next is Accordion fresh)`) bound a name nobody declared.
        Visit(expression, names);
        Walk(expression, names);
        return names.Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>Every name <paramref name="designation"/> binds, nested ones included, as
    /// <see cref="Names"/> spells them; a discard binds none.</summary>
    internal static IReadOnlyList<string> Designated(VariableDesignationSyntax designation)
    {
        var names = new List<string>();
        Designate(designation, names);
        return names;
    }

    /// <summary>
    /// The binding a designation WRITES where it declares itself — <c>var (a, (b, _))</c> in a
    /// deconstruction or a foreach — spelled exactly as <see cref="Names"/> and every reference spell
    /// it: <c>[a, [b, ]]</c>. A discard is a hole: a name there would be a binding, and two of them a
    /// duplicate declaration. A nested designation is a nested pattern, where the deconstruction
    /// once wrote a hole for it and left both its names undeclared.
    /// </summary>
    public static string BindingPattern(VariableDesignationSyntax designation) => designation switch
    {
        SingleVariableDesignationSyntax single => single.Identifier.Text.ToJsIdentifier(),
        ParenthesizedVariableDesignationSyntax parenthesized =>
            "[" + string.Join(", ", parenthesized.Variables.Select(BindingPattern)) + "]",
        _ => "",
    };

    private static void Walk(SyntaxNode node, List<string> names)
    {
        foreach (var child in node.ChildNodes())
        {
            if (child is LambdaExpressionSyntax or AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
                continue;
            // A switch expression's ARMS are the IIFE's scope and it declares them itself; its
            // GOVERNING expression is not, so the arm is skipped rather than the whole switch. A
            // query's clauses are arrows of their own, and its source is not, so the body is skipped.
            if (child is SwitchExpressionArmSyntax or QueryBodySyntax) continue;
            Visit(child, names);
            Walk(child, names);
        }
    }

    private static void Visit(SyntaxNode node, List<string> names)
    {
        switch (node)
        {
            case IsPatternExpressionSyntax isPattern:
                Collect(isPattern.Pattern, names);
                break;
            // `var (a, b) = …` is written `let [a, b] = …` by the assignment itself.
            case DeclarationExpressionSyntax { Designation: ParenthesizedVariableDesignationSyntax, Parent: AssignmentExpressionSyntax assignment } declaration
                when assignment.Left == declaration:
                break;
            case DeclarationExpressionSyntax declaration:
                Designate(declaration.Designation, names);
                break;
        }
    }

    private static void Collect(PatternSyntax pattern, List<string> names)
    {
        switch (pattern)
        {
            case DeclarationPatternSyntax { Designation: var designation }:
                Designate(designation, names);
                break;
            case VarPatternSyntax { Designation: var designation }:
                Designate(designation, names);
                break;
            case RecursivePatternSyntax recursive:
                if (recursive.Designation is { } own) Designate(own, names);
                if (recursive.PositionalPatternClause is { } positional)
                    foreach (var sub in positional.Subpatterns) Collect(sub.Pattern, names);
                if (recursive.PropertyPatternClause is { } property)
                    foreach (var sub in property.Subpatterns) Collect(sub.Pattern, names);
                break;
            case ListPatternSyntax list:
                foreach (var element in list.Patterns) Collect(element, names);
                if (list.Designation is { } whole) Designate(whole, names);
                break;
            case SlicePatternSyntax { Pattern: { } slice }:
                Collect(slice, names);
                break;
            case BinaryPatternSyntax binary:
                Collect(binary.Left, names);
                Collect(binary.Right, names);
                break;
            case UnaryPatternSyntax unary:
                Collect(unary.Pattern, names);
                break;
            case ParenthesizedPatternSyntax parenthesized:
                Collect(parenthesized.Pattern, names);
                break;
        }
    }

    /// <summary>Every name a designation binds, nested ones included; a discard binds none.</summary>
    private static void Designate(VariableDesignationSyntax designation, List<string> names)
    {
        switch (designation)
        {
            case SingleVariableDesignationSyntax single:
                names.Add(single.Identifier.Text.ToJsIdentifier());
                break;
            case ParenthesizedVariableDesignationSyntax parenthesized:
                foreach (var variable in parenthesized.Variables) Designate(variable, names);
                break;
        }
    }
}
