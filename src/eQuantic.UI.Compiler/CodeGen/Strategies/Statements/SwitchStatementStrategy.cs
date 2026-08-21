using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary>
/// <c>switch</c> statements. Constant labels only → a native JavaScript <c>switch</c>. Any
/// pattern label (<c>case int n when n > 3:</c>) → an if/else chain over the subject bound once
/// (<c>const _s = …</c>), because JavaScript's switch has no patterns; the pattern's bindings are
/// hoisted once for the whole chain and assigned inside each arm's condition.
/// </summary>
public class SwitchStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is SwitchStatementSyntax;
    }

    public JsStatement Convert(StatementSyntax node, ConversionContext context)
    {
        var switchStmt = (SwitchStatementSyntax)node;
        var expr = context.Converter.ConvertIr(switchStmt.Expression);
        var usesPatterns = switchStmt.Sections
            .SelectMany(s => s.Labels)
            .Any(l => l is CasePatternSwitchLabelSyntax);

        return usesPatterns
            ? ConvertAsIfChain(switchStmt, expr, context)
            : ConvertAsNativeSwitch(switchStmt, context, expr);
    }

    private static JsStatement ConvertAsNativeSwitch(SwitchStatementSyntax switchStmt, ConversionContext context, JsExpr expr)
    {
        var cases = switchStmt.Sections.Select(section => new JsCase(
            section.Labels.Select(label => label switch
            {
                CaseSwitchLabelSyntax caseLabel => $"case {context.Converter.ConvertExpression(caseLabel.Value)}",
                _ => "default",
            }).ToList(),
            section.Statements.Select(context.Converter.ConvertStatementIr).ToList())).ToList();
        return JsStatement.Switch(expr, cases);
    }

    private static JsStatement ConvertAsIfChain(SwitchStatementSyntax switchStmt, JsExpr expr, ConversionContext context)
    {
        var governingType = context.SemanticHelper.GetType(switchStmt.Expression);
        var arms = new List<(string Condition, JsStatement Body)>();
        var hoist = new List<string>();   // distinct bound names, hoisted once for the whole chain
        var seen = new HashSet<string>();
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
                        labelConditions.Add($"_s === {context.Converter.ConvertExpression(constant.Value)}");
                        break;

                    case CasePatternSwitchLabelSyntax pat:
                        var cond = PatternConverter.BuildCondition(pat.Pattern, "_s", context, governingType);
                        var bindings = new List<(string Name, string Access)>();
                        PatternConverter.CollectBindings(pat.Pattern, "_s", context, bindings, governingType);
                        foreach (var b in bindings) if (seen.Add(b.Name)) hoist.Add(b.Name);

                        // Assign the pattern's bindings AND evaluate the when-clause inside the condition (a
                        // comma sequence, guarded by `&&` so it only runs when the pattern matched): this
                        // puts the bound variables in scope for `when`, and a failing `when` makes the whole
                        // condition false so control falls to the next arm — exactly the C# semantics.
                        var whenExpr = pat.WhenClause != null
                            ? context.Converter.ConvertExpression(pat.WhenClause.Condition)
                            : null;
                        if (bindings.Count > 0 || whenExpr != null)
                        {
                            var assigns = string.Concat(bindings.Select(b => $"{b.Name} = {b.Access}, "));
                            cond = $"({cond} && ({assigns}{whenExpr ?? "true"}))";
                        }
                        labelConditions.Add(cond);
                        break;
                }
            }

            arms.Add((string.Join(" || ", labelConditions), ConvertSectionBody(section, context)));
        }

        // The chain, innermost first: the default is the last else, each arm an `else if` above it.
        JsStatement? chain = defaultSection is null ? null : ConvertSectionBody(defaultSection, context);
        for (var i = arms.Count - 1; i >= 0; i--)
            chain = JsStatement.If(JsExpr.Opaque(arms[i].Condition), arms[i].Body, chain);

        var statements = new List<JsStatement>();
        if (hoist.Count > 0) statements.Add(JsStatement.Raw($"let {string.Join(", ", hoist)};"));
        statements.Add(JsStatement.Const("_s", expr));
        if (chain is not null) statements.Add(chain);
        return JsStatement.Block(statements);
    }

    /// <summary>A section's statements as a block — minus the `break` that only C# needs.</summary>
    private static JsStatement ConvertSectionBody(SwitchSectionSyntax section, ConversionContext context) =>
        JsStatement.Block(section.Statements
            .Where(stmt => stmt is not BreakStatementSyntax)
            .Select(context.Converter.ConvertStatementIr)
            .ToList());

    public int Priority => 0;
}
