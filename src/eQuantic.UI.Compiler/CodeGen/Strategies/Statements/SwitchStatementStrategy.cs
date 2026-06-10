using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary>
/// Converts a <c>switch</c> statement. A switch over constant cases keeps the native JS <c>switch</c>
/// (which supports fall-through). A switch that uses PATTERN labels (<c>case { … }:</c>, <c>case Type t:</c>,
/// <c>case … when …:</c>) has no JS equivalent, so it is rewritten to an <c>if/else</c> chain in a block:
/// the value is bound once to <c>_s</c>, each section becomes an arm whose condition is the OR of its
/// labels (built by the shared <see cref="PatternConverter"/>), the variables a pattern binds are declared
/// at the top of that arm, and the <c>default</c> section becomes the trailing <c>else</c>. A block (not an
/// IIFE) is used so a <c>return</c> inside a case still returns from the enclosing method; <c>break</c> just
/// exits the chain so it is dropped.
/// </summary>
public class SwitchStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is SwitchStatementSyntax;
    }

    public string Convert(StatementSyntax node, ConversionContext context)
    {
        var switchStmt = (SwitchStatementSyntax)node;
        var expr = context.Converter.ConvertExpression(switchStmt.Expression);

        var usesPatterns = switchStmt.Sections
            .SelectMany(s => s.Labels)
            .Any(l => l is CasePatternSwitchLabelSyntax);

        return usesPatterns
            ? ConvertAsIfChain(switchStmt, expr, context)
            : ConvertAsNativeSwitch(switchStmt, context, expr);
    }

    private static string ConvertAsNativeSwitch(SwitchStatementSyntax switchStmt, ConversionContext context, string expr)
    {
        var sb = new StringBuilder();
        sb.Append($"switch ({expr}) {{");
        foreach (var section in switchStmt.Sections)
        {
            foreach (var label in section.Labels)
            {
                if (label is CaseSwitchLabelSyntax caseLabel)
                    sb.Append($" case {context.Converter.ConvertExpression(caseLabel.Value)}:");
                else if (label is DefaultSwitchLabelSyntax)
                    sb.Append(" default:");
            }
            foreach (var stmt in section.Statements)
                sb.Append(" " + context.Converter.Convert(stmt));
        }
        sb.Append(" }");
        return sb.ToString();
    }

    private static string ConvertAsIfChain(SwitchStatementSyntax switchStmt, string expr, ConversionContext context)
    {
        var governingType = context.SemanticHelper.GetType(switchStmt.Expression);

        var arms = new List<(string Condition, string Body)>();
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

        var sb = new StringBuilder();
        sb.Append("{ ");
        if (hoist.Count > 0) sb.Append($"let {string.Join(", ", hoist)}; ");
        sb.Append($"const _s = {expr};");
        for (var i = 0; i < arms.Count; i++)
            sb.Append($" {(i == 0 ? "if" : "else if")} ({arms[i].Condition}) {{ {arms[i].Body}}}");
        if (defaultSection != null)
            sb.Append($" else {{ {ConvertSectionBody(defaultSection, context)}}}");
        sb.Append(" }");
        return sb.ToString();
    }

    /// <summary>Emits a section's statements, dropping <c>break;</c> (the if/else arms are mutually
    /// exclusive, so an arm's body simply ends and control leaves the chain — there is nothing to break
    /// out of). Pattern bindings are assigned in the arm condition, so the body just uses them.</summary>
    private static string ConvertSectionBody(SwitchSectionSyntax section, ConversionContext context)
    {
        var sb = new StringBuilder();
        foreach (var stmt in section.Statements)
        {
            if (stmt is BreakStatementSyntax) continue;
            sb.Append(context.Converter.Convert(stmt) + " ");
        }
        return sb.ToString();
    }

    public int Priority => 0;
}
