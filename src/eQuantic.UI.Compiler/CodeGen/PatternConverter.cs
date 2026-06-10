using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// Shared C#-pattern → JavaScript conversion, used by switch expressions, switch statements (rewritten to
/// an if/else chain) and <c>is</c> patterns. Produces two things for a pattern matched against a value
/// reachable at <c>access</c>:
/// <list type="bullet">
/// <item><see cref="BuildCondition"/> — the boolean test (constant/relational/type/property/positional/
///   list/<c>and</c>/<c>or</c>/<c>not</c>).</item>
/// <item><see cref="CollectBindings"/> — every variable the pattern binds (<c>var</c>/<c>Type t</c>),
///   anywhere in it, with the JS access path to read it.</item>
/// </list>
/// Positional subpatterns resolve their access from the matched type: a tuple is indexed (<c>[i]</c>),
/// while a record/struct uses its <c>Deconstruct</c> element names (<c>.x</c>, <c>.y</c>) — a record is a
/// plain object at runtime, so index access would read <c>undefined</c>.
/// </summary>
public static class PatternConverter
{
    public static string BuildCondition(PatternSyntax pattern, string access, ConversionContext context,
        ITypeSymbol? accessType = null)
    {
        switch (pattern)
        {
            case ConstantPatternSyntax constant:
                return $"{access} === {context.Converter.ConvertExpression(constant.Expression)}";

            case RelationalPatternSyntax relational:
                return $"{access} {relational.OperatorToken.Text} {context.Converter.ConvertExpression(relational.Expression)}";

            case DeclarationPatternSyntax declaration:
                return TypeCheck(declaration.Type.ToString(), access);

            case RecursivePatternSyntax recursive:
                return BuildRecursive(recursive, access, context, accessType);

            case ListPatternSyntax list:
                return BuildList(list, access, context);

            case UnaryPatternSyntax unary when unary.OperatorToken.IsKind(SyntaxKind.NotKeyword):
                return $"!({BuildCondition(unary.Pattern, access, context, accessType)})";

            case BinaryPatternSyntax binary:
                var l = BuildCondition(binary.Left, access, context, accessType);
                var r = BuildCondition(binary.Right, access, context, accessType);
                var op = binary.OperatorToken.IsKind(SyntaxKind.OrKeyword) ? "||" : "&&";
                return $"({l} {op} {r})";

            case ParenthesizedPatternSyntax paren:
                return BuildCondition(paren.Pattern, access, context, accessType);

            case DiscardPatternSyntax:
            case VarPatternSyntax:
                return "true";

            case SlicePatternSyntax:
                return "true"; // length is enforced by the containing list pattern

            default:
                return "false";
        }
    }

    public static void CollectBindings(PatternSyntax pattern, string access, ConversionContext context,
        List<(string Name, string Access)> bindings, ITypeSymbol? accessType = null)
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
                {
                    var names = PositionalNames(accessType);
                    for (int i = 0; i < recursive.PositionalPatternClause.Subpatterns.Count; i++)
                        CollectBindings(recursive.PositionalPatternClause.Subpatterns[i].Pattern,
                            PositionalAccess(access, i, names), context, bindings);
                }
                if (recursive.PropertyPatternClause != null)
                    foreach (var sp in recursive.PropertyPatternClause.Subpatterns)
                    {
                        var propName = sp.NameColon?.Name.ToString() ?? sp.ExpressionColon?.Expression.ToString();
                        if (propName != null)
                            CollectBindings(sp.Pattern, $"{access}.{Camel(propName)}", context, bindings);
                    }
                break;

            case ListPatternSyntax list:
                CollectListBindings(list, access, context, bindings);
                break;

            case ParenthesizedPatternSyntax paren:
                CollectBindings(paren.Pattern, access, context, bindings, accessType);
                break;

            case BinaryPatternSyntax binary when binary.OperatorToken.IsKind(SyntaxKind.AndKeyword):
                CollectBindings(binary.Left, access, context, bindings, accessType);
                CollectBindings(binary.Right, access, context, bindings, accessType);
                break;
        }
    }

    private static string BuildRecursive(RecursivePatternSyntax recursive, string access,
        ConversionContext context, ITypeSymbol? accessType)
    {
        var checks = new List<string>();
        if (recursive.Type != null || recursive.PropertyPatternClause != null
            || recursive.PositionalPatternClause != null)
            checks.Add($"{access} != null");

        if (recursive.PositionalPatternClause != null)
        {
            var names = PositionalNames(accessType);
            for (int i = 0; i < recursive.PositionalPatternClause.Subpatterns.Count; i++)
            {
                var sub = BuildCondition(recursive.PositionalPatternClause.Subpatterns[i].Pattern,
                    PositionalAccess(access, i, names), context);
                if (sub != "true") checks.Add(sub);
            }
        }
        if (recursive.PropertyPatternClause != null)
            foreach (var sp in recursive.PropertyPatternClause.Subpatterns)
            {
                var propName = sp.NameColon?.Name.ToString() ?? sp.ExpressionColon?.Expression.ToString();
                if (propName == null) continue;
                var sub = BuildCondition(sp.Pattern, $"{access}.{Camel(propName)}", context);
                if (sub != "true") checks.Add(sub);
            }

        return checks.Count > 0 ? "(" + string.Join(" && ", checks) + ")" : $"{access} != null";
    }

    private static string BuildList(ListPatternSyntax list, string access, ConversionContext context)
    {
        var (before, after, sliceIndex) = SliceShape(list);
        var checks = new List<string>
        {
            $"Array.isArray({access})",
            sliceIndex < 0 ? $"{access}.length === {list.Patterns.Count}" : $"{access}.length >= {before + after}",
        };
        for (int i = 0; i < before; i++)
        {
            var sub = BuildCondition(list.Patterns[i], $"{access}[{i}]", context);
            if (sub != "true") checks.Add(sub);
        }
        for (int j = 0; j < after; j++)
        {
            var sub = BuildCondition(list.Patterns[sliceIndex + 1 + j], $"{access}[{access}.length - {after - j}]", context);
            if (sub != "true") checks.Add(sub);
        }
        return "(" + string.Join(" && ", checks) + ")";
    }

    private static void CollectListBindings(ListPatternSyntax list, string access,
        ConversionContext context, List<(string Name, string Access)> bindings)
    {
        var (before, after, sliceIndex) = SliceShape(list);
        for (int i = 0; i < before; i++)
            CollectBindings(list.Patterns[i], $"{access}[{i}]", context, bindings);
        if (sliceIndex >= 0 && list.Patterns[sliceIndex] is SlicePatternSyntax { Pattern: { } slicePat })
            CollectBindings(slicePat, $"{access}.slice({before}, {access}.length - {after})", context, bindings);
        for (int j = 0; j < after; j++)
            CollectBindings(list.Patterns[sliceIndex + 1 + j], $"{access}[{access}.length - {after - j}]", context, bindings);
    }

    private static (int Before, int After, int SliceIndex) SliceShape(ListPatternSyntax list)
    {
        for (int i = 0; i < list.Patterns.Count; i++)
            if (list.Patterns[i] is SlicePatternSyntax)
                return (i, list.Patterns.Count - i - 1, i);
        return (list.Patterns.Count, 0, -1);
    }

    /// <summary>Deconstruct element names of a non-tuple type (record/struct) for positional access, or
    /// <c>null</c> to fall back to index access (tuples, or unknown types).</summary>
    private static IReadOnlyList<string>? PositionalNames(ITypeSymbol? type)
        => type is { IsTupleType: false } ? type.DeconstructElementNames() : null;

    private static string PositionalAccess(string access, int i, IReadOnlyList<string>? names)
        => names != null && i < names.Count ? $"{access}.{names[i]}" : $"{access}[{i}]";

    private static string TypeCheck(string type, string access) => type switch
    {
        "string" => $"typeof {access} === 'string'",
        "int" or "double" or "float" or "long" or "decimal" or "number" => $"typeof {access} === 'number'",
        "bool" or "boolean" => $"typeof {access} === 'boolean'",
        _ => $"{access} != null",
    };

    private static string Camel(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name.Substring(1);
}
