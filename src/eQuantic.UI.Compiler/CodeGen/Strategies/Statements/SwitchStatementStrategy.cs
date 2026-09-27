using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary>
/// <c>switch</c> statements. Constant labels only → a native JavaScript <c>switch</c>. Any
/// pattern label (<c>case int n when n > 3:</c>) → an if/else chain over the subject bound once
/// (<c>const $s = …</c>), because JavaScript's switch has no patterns; the pattern's bindings are
/// hoisted once for the whole chain and assigned inside each arm's condition.
/// </summary>
public class SwitchStatementStrategy : IStatementStrategy
{
    /// <summary>The subject's name, the switch expression's own: a `$` no variable a section
    /// declares can hold (SwitchExpressionStrategy.Subject).</summary>
    private const string Subject = Expressions.SwitchExpressionStrategy.Subject;

    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is SwitchStatementSyntax;
    }

    public JsStatement Convert(StatementSyntax node, ConversionContext context)
    {
        var switchStmt = (SwitchStatementSyntax)node;
        // What the governing expression declares lives on after the switch — Roslyn scopes it to
        // the enclosing block, like an if's condition — so it is declared in front, outside the
        // block the if-chain form opens.
        var declared = ExpressionVariableScanner.InFrontOf(switchStmt, switchStmt.Expression, context.TypeAnnotations);
        var expr = context.Converter.ConvertIr(switchStmt.Expression);
        var usesPatterns = switchStmt.Sections
            .SelectMany(s => s.Labels)
            .Any(l => l is CasePatternSwitchLabelSyntax);

        return JsStatement.Hoisted(declared, usesPatterns
            ? ConvertAsIfChain(switchStmt, expr, context)
            : ConvertAsNativeSwitch(switchStmt, context, expr));
    }

    /// <summary>
    /// What the statements of the sections declare into their block (<c>case 1: Parse(s, out var
    /// n);</c>): C# scopes it to the whole switch block, so another section can assign and read it,
    /// and those statements leave it to the switch (ExpressionVariableScanner.InFrontOf). Declared
    /// in the section that wrote it, it was in its temporal dead zone for every other section.
    /// </summary>
    private static IReadOnlyList<string> SectionNames(SwitchStatementSyntax switchStmt) =>
        switchStmt.Sections
            .SelectMany(section => section.Statements)
            .SelectMany(ExpressionVariableScanner.BlockNames)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static JsStatement ConvertAsNativeSwitch(SwitchStatementSyntax switchStmt, ConversionContext context, JsExpr expr)
    {
        // The switch block's own declarations, in a block that is the switch's scope and no wider.
        var declared = ExpressionVariableScanner.Declarations(SectionNames(switchStmt), context.TypeAnnotations);
        // A section's statements sit two levels in, one more inside that block: converted there, so
        // what they lay out (a lambda's block) indents as they do (found in review, #384).
        var cases = Deeper(context, declared.Length == 0 ? 2 : 3, () => switchStmt.Sections.Select(section => new JsCase(
            section.Labels.Select(label => label switch
            {
                CaseSwitchLabelSyntax caseLabel => $"case {context.Converter.ConvertExpression(caseLabel.Value)}",
                _ => "default",
            }).ToList(),
            section.Statements.Select(context.Converter.ConvertStatementIr).ToList())).ToList());
        var switchStatement = JsStatement.Switch(expr, cases);
        return declared.Length == 0 ? switchStatement : JsStatement.Block([JsStatement.Raw(declared.TrimEnd()), switchStatement]);
    }

    /// <summary><paramref name="convert"/> run <paramref name="levels"/> levels deeper than the switch.</summary>
    private static T Deeper<T>(ConversionContext context, int levels, Func<T> convert)
    {
        context.Depth += levels;
        try
        {
            return convert();
        }
        finally
        {
            context.Depth -= levels;
        }
    }

    private static JsStatement ConvertAsIfChain(SwitchStatementSyntax switchStmt, JsExpr expr, ConversionContext context)
    {
        var governingType = context.SemanticHelper.GetType(switchStmt.Expression);
        var arms = new List<(string Condition, JsStatement Body, SwitchSectionSyntax Section)>();
        var hoist = new List<string>();   // distinct bound names, hoisted once for the whole chain
        var seen = new HashSet<string>();
        // What the sections' statements declare belongs to the switch block too (see SectionNames).
        foreach (var name in SectionNames(switchStmt))
            if (seen.Add(name)) hoist.Add(name);
        SwitchSectionSyntax? defaultSection = null;

        foreach (var section in switchStmt.Sections)
        {
            if (section.Labels.Any(l => l is DefaultSwitchLabelSyntax))
            {
                defaultSection = section;
                continue;
            }

            var labelConditions = new List<string>();
            foreach (var label in section.Labels)
            {
                switch (label)
                {
                    case CaseSwitchLabelSyntax constant:
                        labelConditions.Add($"{Subject} === {context.Converter.ConvertExpression(constant.Value)}");
                        break;

                    case CasePatternSwitchLabelSyntax pat:
                        var cond = PatternConverter.BuildCondition(pat.Pattern, Subject, context, governingType);
                        var bindings = new List<(string Name, string Access)>();
                        PatternConverter.CollectBindings(pat.Pattern, Subject, context, bindings, governingType);
                        foreach (var b in bindings) if (seen.Add(b.Name)) hoist.Add(b.Name);

                        // Assign the pattern's bindings AND evaluate the when-clause inside the condition (a
                        // comma sequence, guarded by `&&` so it only runs when the pattern matched): this
                        // puts the bound variables in scope for `when`, and a failing `when` makes the whole
                        // condition false so control falls to the next arm — exactly the C# semantics.
                        var whenExpr = pat.WhenClause != null
                            ? context.Converter.ConvertExpression(pat.WhenClause.Condition)
                            : null;
                        // What the guard itself declares (`when int.TryParse(s, out var n)`) belongs
                        // to the section, and the chain's one declaration covers every section.
                        foreach (var name in ExpressionVariableScanner.Names(pat.WhenClause?.Condition))
                            if (seen.Add(name)) hoist.Add(name);
                        if (bindings.Count > 0 || whenExpr != null)
                        {
                            var assigns = string.Concat(bindings.Select(b => $"{b.Name} = {b.Access}, "));
                            cond = $"({cond} && ({assigns}{whenExpr ?? "true"}))";
                        }
                        labelConditions.Add(cond);
                        break;
                }
            }

            arms.Add((string.Join(" || ", labelConditions), ConvertSectionBody(section, context), section));
        }

        // The chain, innermost first: the default is the last else, each arm an `else if` above it.
        // An arm's test is its section's labels, so its line maps to them (#293): the switch's own
        // line would answer for every arm, and a `when` that throws names the case it guards.
        JsStatement? chain = defaultSection is null ? null : ConvertSectionBody(defaultSection, context);
        for (var i = arms.Count - 1; i >= 0; i--)
            chain = JsStatement.If(JsExpr.Opaque(arms[i].Condition), arms[i].Body, chain) with { Origin = arms[i].Section };

        var statements = new List<JsStatement>();
        // Annotated in TypeScript, as every declaration the scanner writes is: a section's `out var`
        // may be assigned inside an arrow (a dictionary's TryGetValue), which TypeScript cannot follow.
        if (hoist.Count > 0) statements.Add(JsStatement.Raw($"let {ExpressionVariableScanner.List(hoist, context.TypeAnnotations)};"));
        statements.Add(JsStatement.Const(Subject, expr));
        if (chain is not null) statements.Add(chain);
        return JsStatement.Block(statements);
    }

    /// <summary>A section's statements as a block — minus the `break` that only C# needs — converted
    /// where they sit, two levels in: the block of the chain, then the branch's own.</summary>
    private static JsStatement ConvertSectionBody(SwitchSectionSyntax section, ConversionContext context) =>
        JsStatement.Block(Deeper(context, 2, () => section.Statements
            .Where(stmt => stmt is not BreakStatementSyntax)
            .Select(context.Converter.ConvertStatementIr)
            .ToList()));

    public int Priority => 0;
}
