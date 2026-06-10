using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;
using Microsoft.CodeAnalysis.CSharp;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

public class SwitchExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is SwitchExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var switchExpr = (SwitchExpressionSyntax)node;
        var governingExpr = context.Converter.ConvertExpression(switchExpr.GoverningExpression);

        var sb = new StringBuilder();

        // Wrap in IIFE to allow complex logic blocks
        sb.Append("(() => {");
        sb.Append($" const _s = {governingExpr};");

        foreach (var arm in switchExpr.Arms)
        {
            if (arm.Pattern is DiscardPatternSyntax)
            {
                var result = context.Converter.ConvertExpression(arm.Expression);
                sb.Append($" return {result};");
                // Discard matches everything, so we stop here
                break;
            }

            var condition = ConvertPattern(arm.Pattern, "_s", context);

            // Collect EVERY variable a pattern binds, anywhere in it, with the access path to reach it:
            // top-level (`var v`/`Type t`), nested in property/positional subpatterns (`{ X: var x }`,
            // `(0, var y)`), and list elements/slices (`[var a, .. var rest]`). Each is declared before the
            // when-clause and result so both can reference it.
            var bindings = new List<(string Name, string Access)>();
            CollectBindings(arm.Pattern, "_s", bindings, context);
            var armResult = context.Converter.ConvertExpression(arm.Expression);

            if (bindings.Count > 0)
            {
                var declare = string.Concat(bindings.Select(b => $"const {b.Name} = {b.Access}; "));
                var inner = $"return {armResult};";
                if (arm.WhenClause != null)
                {
                    var whenCond = context.Converter.ConvertExpression(arm.WhenClause.Condition);
                    inner = $"if ({whenCond}) return {armResult};";
                }
                sb.Append($" if ({condition}) {{ {declare}{inner} }}");
            }
            else
            {
                if (arm.WhenClause != null)
                {
                    var whenCond = context.Converter.ConvertExpression(arm.WhenClause.Condition);
                    condition = $"({condition}) && ({whenCond})";
                }
                sb.Append($" if ({condition}) return {armResult};");
            }
        }

        // Default fallback if no discard pattern exists
        if (!switchExpr.Arms.Any(a => a.Pattern is DiscardPatternSyntax))
        {
            sb.Append(" return null;");
        }

        sb.Append(" })()");
        return sb.ToString();
    }

    private static string Camel(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name.Substring(1);

    /// <summary>
    /// Walks a pattern collecting every variable it binds together with the JS access path to reach it,
    /// so the switch arm can declare them before the when-clause/result. Covers top-level designations
    /// (<c>var v</c> / <c>Type t</c> / <c>Type(..) p</c>), nested property (<c>{ X: var x }</c>) and
    /// positional (<c>(0, var y)</c>) subpatterns, list elements / slice (<c>[var a, .. var rest]</c>), and
    /// the bind-able half of <c>and</c> patterns.
    /// </summary>
    private static void CollectBindings(PatternSyntax pattern, string access,
        List<(string Name, string Access)> bindings, ConversionContext context)
    {
        switch (pattern)
        {
            case VarPatternSyntax { Designation: SingleVariableDesignationSyntax v }:
                bindings.Add((v.Identifier.Text, access));
                break;

            case DeclarationPatternSyntax { Designation: SingleVariableDesignationSyntax d }:
                bindings.Add((d.Identifier.Text, access));
                break;

            case RecursivePatternSyntax recursive:
                if (recursive.Designation is SingleVariableDesignationSyntax r)
                    bindings.Add((r.Identifier.Text, access));
                if (recursive.PositionalPatternClause != null)
                    for (int i = 0; i < recursive.PositionalPatternClause.Subpatterns.Count; i++)
                        CollectBindings(recursive.PositionalPatternClause.Subpatterns[i].Pattern,
                            $"{access}[{i}]", bindings, context);
                if (recursive.PropertyPatternClause != null)
                    foreach (var sp in recursive.PropertyPatternClause.Subpatterns)
                    {
                        var propName = sp.NameColon?.Name.ToString() ?? sp.ExpressionColon?.Expression.ToString();
                        if (propName != null)
                            CollectBindings(sp.Pattern, $"{access}.{Camel(propName)}", bindings, context);
                    }
                break;

            case ListPatternSyntax list:
                CollectListBindings(list, access, bindings, context);
                break;

            case ParenthesizedPatternSyntax paren:
                CollectBindings(paren.Pattern, access, bindings, context);
                break;

            case BinaryPatternSyntax { } binary when binary.OperatorToken.IsKind(SyntaxKind.AndKeyword):
                // `> 0 and var x` binds via the right side; `or` can't bind reliably so it's skipped.
                CollectBindings(binary.Left, access, bindings, context);
                CollectBindings(binary.Right, access, bindings, context);
                break;
        }
    }

    private static void CollectListBindings(ListPatternSyntax list, string access,
        List<(string Name, string Access)> bindings, ConversionContext context)
    {
        var (before, after, sliceIndex) = SliceShape(list);
        for (int i = 0; i < before; i++)
            CollectBindings(list.Patterns[i], $"{access}[{i}]", bindings, context);

        if (sliceIndex >= 0 && list.Patterns[sliceIndex] is SlicePatternSyntax { Pattern: { } slicePat })
            // `.. var rest` binds the middle slice.
            CollectBindings(slicePat, $"{access}.slice({before}, {access}.length - {after})", bindings, context);

        for (int j = 0; j < after; j++)
            CollectBindings(list.Patterns[sliceIndex + 1 + j], $"{access}[{access}.length - {after - j}]", bindings, context);
    }

    /// <summary>(elementsBeforeSlice, elementsAfterSlice, sliceIndex) — sliceIndex is -1 for a fixed-length
    /// list pattern (no <c>..</c>).</summary>
    private static (int Before, int After, int SliceIndex) SliceShape(ListPatternSyntax list)
    {
        for (int i = 0; i < list.Patterns.Count; i++)
            if (list.Patterns[i] is SlicePatternSyntax)
                return (i, list.Patterns.Count - i - 1, i);
        return (list.Patterns.Count, 0, -1);
    }

    private string ConvertPattern(PatternSyntax pattern, string varName, ConversionContext context)
    {
        switch (pattern)
        {
            case ConstantPatternSyntax constant:
                var val = context.Converter.ConvertExpression(constant.Expression);
                return $"{varName} === {val}";

            case RelationalPatternSyntax relational:
                var op = relational.OperatorToken.Text;
                var right = context.Converter.ConvertExpression(relational.Expression);
                return $"{varName} {op} {right}";

            case DeclarationPatternSyntax declaration:
                var type = declaration.Type.ToString();
                // Simple type check for primitives
                if (type == "string") return $"typeof {varName} === 'string'";
                if (type == "int" || type == "double") return $"typeof {varName} === 'number'";
                if (type == "bool") return $"typeof {varName} === 'boolean'";
                // For classes, we might check instance? But in JS everything is object.
                // Assuming truthy check for object presence
                return $"{varName} != null";

            case RecursivePatternSyntax recursive:
                var checks = new List<string>();

                // Type check if present
                if (recursive.Type != null)
                {
                    checks.Add($"{varName} != null");
                }

                // Positional patterns (a, b)
                if (recursive.PositionalPatternClause != null)
                {
                    for (int i = 0; i < recursive.PositionalPatternClause.Subpatterns.Count; i++)
                    {
                        var sub = recursive.PositionalPatternClause.Subpatterns[i];
                        // Assuming target is an array or has indexed properties
                        var subVar = $"{varName}[{i}]";
                        checks.Add(ConvertPattern(sub.Pattern, subVar, context));
                    }
                }

                // Property patterns { Prop: 1 }
                if (recursive.PropertyPatternClause != null)
                {
                    foreach (var subPattern in recursive.PropertyPatternClause.Subpatterns)
                    {
                        var propName = subPattern.NameColon?.Name.ToString();
                        if (propName != null)
                        {
                            var subVar = $"{varName}.{char.ToLowerInvariant(propName[0])}{propName.Substring(1)}";
                            checks.Add(ConvertPattern(subPattern.Pattern, subVar, context));
                        }
                    }
                }

                return checks.Count > 0 ? string.Join(" && ", checks) : $"{varName} != null";

            case UnaryPatternSyntax unary:
                if (unary.OperatorToken.IsKind(SyntaxKind.NotKeyword))
                {
                    return $"!({ConvertPattern(unary.Pattern, varName, context)})";
                }
                return "false";

            case ListPatternSyntax list:
                return ConvertListPattern(list, varName, context);

            case VarPatternSyntax _:
                return "true"; // Always matches

            case BinaryPatternSyntax binary:
                 var left = ConvertPattern(binary.Left, varName, context);
                 var rightPattern = ConvertPattern(binary.Right, varName, context);
                 var logicOp = binary.OperatorToken.IsKind(SyntaxKind.OrKeyword) ? "||" : "&&";
                 return $"({left} {logicOp} {rightPattern})";

            case ParenthesizedPatternSyntax paren:
                 return ConvertPattern(paren.Pattern, varName, context);

            default:
                return "false";
        }
    }

    /// <summary>
    /// List pattern → length check plus element checks. Fixed length (<c>[a, b]</c>) asserts an exact
    /// length and matches each element by index; a single slice (<c>[a, .., z]</c>) asserts a minimum
    /// length and matches the leading elements from the start and trailing ones from the end. The slice
    /// itself imposes no element check (any middle); its binding is handled by <see cref="CollectBindings"/>.
    /// </summary>
    private string ConvertListPattern(ListPatternSyntax list, string varName, ConversionContext context)
    {
        var (before, after, sliceIndex) = SliceShape(list);
        var checks = new List<string>();

        checks.Add(sliceIndex < 0
            ? $"{varName}.length === {list.Patterns.Count}"
            : $"{varName}.length >= {before + after}");

        for (int i = 0; i < before; i++)
        {
            var sub = ConvertPattern(list.Patterns[i], $"{varName}[{i}]", context);
            if (sub != "true") checks.Add(sub);
        }
        for (int j = 0; j < after; j++)
        {
            var sub = ConvertPattern(list.Patterns[sliceIndex + 1 + j],
                $"{varName}[{varName}.length - {after - j}]", context);
            if (sub != "true") checks.Add(sub);
        }

        return "(" + string.Join(" && ", checks) + ")";
    }

    public int Priority => 0;
}
