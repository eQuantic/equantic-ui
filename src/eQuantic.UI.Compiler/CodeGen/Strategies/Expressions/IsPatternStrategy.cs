using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for 'is' pattern expressions (<c>x is string s</c>, <c>x is { Prop: var p }</c>,
/// <c>x is [var a, ..]</c>). The match condition and bindings come from the shared
/// <see cref="PatternConverter"/>; the <c>x is Type</c> binary form is a plain type check.
/// </summary>
public class IsPatternStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is IsPatternExpressionSyntax || 
               (node is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.IsExpression));
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        if (node is IsPatternExpressionSyntax isPattern)
        {
            var expr = context.Converter.ConvertExpression(isPattern.Expression);
            var exprType = context.SemanticHelper.GetType(isPattern.Expression);

            // Peel top-level `not`s FIRST: bindings live in the inner pattern and must assign when
            // the INNER pattern matches — C#'s definite-assignment-when-false, the guard idiom
            // (`if (x is not T t) return;` leaves t assigned after the guard). Negating the whole
            // bound form (`!(match && (t = x, true))`) keeps that: inner match → t assigned →
            // expression false; no match → t untouched → expression true. Negating BEFORE the
            // assigns would short-circuit past them exactly when C# guarantees the assignment.
            var pattern = isPattern.Pattern;
            var negated = false;
            while (pattern is UnaryPatternSyntax unary && unary.OperatorToken.IsKind(SyntaxKind.NotKeyword))
            {
                negated = !negated;
                pattern = unary.Pattern;
            }

            // Condition + bindings come from the shared PatternConverter (same logic as the switch forms:
            // Deconstruct-aware positional access, list patterns, nested var bindings). A bound `is` pattern
            // assigns its variables — to slots IfStatementStrategy hoisted (`let x;`) — inside the condition
            // via a comma sequence, guarded by `&&` so the reads only run once the pattern has matched.
            // A subject that is not a plain path (a call, an element, an await) is read ONCE, as C#
            // reads it (see Once).
            var once = IsPath(expr) ? null : $"$v{isPattern.SpanStart}";
            var access = once ?? expr;
            var condition = PatternConverter.BuildCondition(pattern, access, context, exprType);
            var bindings = new List<(string Name, string Access)>();
            PatternConverter.CollectBindings(pattern, access, context, bindings, exprType);

            // `x is { } y` over a CALL — the shape that reads "if this returns something, name it".
            // The subject appears in the condition and again in the binding, so it used to run
            // TWICE: wasted work when the call is pure, and a different answer when it is not.
            // Assigning inside the test runs it once, and gives TypeScript the narrowing too.
            // Only the NULL test: `x is string s` must not assign when the type does not match, and
            // `x is { } y` has nothing to check but presence, so assigning IS the test.
            // Asked of the PATTERN, not of the condition's text. The string comparison this
            // replaces expected `x != null` and BuildCondition produces `(x != null)`, so the
            // optimisation never once ran — the call kept being evaluated twice, which is wasted
            // work when it is pure and a different answer when it is not.
            if (bindings.Count == 1 && bindings[0].Access == access && IsPresenceOnly(pattern))
            {
                var assigned = $"({bindings[0].Name} = {expr}) != null";
                return negated ? $"!({assigned})" : assigned;
            }

            var bound = bindings.Count == 0
                ? condition
                : $"({condition} && ({string.Concat(bindings.Select(b => $"{b.Name} = {b.Access}, "))}true))";
            var tested = negated ? $"!({bound})" : bound;
            return once is null ? tested : Once(tested, once, expr);
        }

        if (node is BinaryExpressionSyntax binary)
        {
            var expr = context.Converter.ConvertExpression(binary.Left);
            // `x is Type` asks the SAME question a type pattern does, so it gets the same answer
            // from the same place. It used to carry a copy of the rule that stopped at the
            // primitives and answered `!= null` for everything else — so `node is Icon` was true for
            // any non-null node, silently.
            // …unless it BINDS as a constant: `x is Limits.Max` parses as the type test and binds as a
            // constant pattern over the const, and was answered `x != null` (#451).
            var once = IsPath(expr) ? null : $"$v{binary.SpanStart}";
            var access = once ?? expr;
            string tested;
            if (context.SemanticHelper.GetOperation(binary) is IIsPatternOperation { Pattern: IConstantPatternOperation constant })
                tested = PatternConverter.ConstantTest(access, ConstantOf(constant.Value, binary.Right, context), constant.Value, context);
            else if (binary.Right is TypeSyntax typeSyntax)
                tested = PatternConverter.TypeCheck(typeSyntax, access, context);
            else
                tested = $"{access} != null";
            return once is null ? tested : Once(tested, once, expr);
        }

        throw new InvalidOperationException($"Invalid node type for IsPatternStrategy: {node.GetType().Name}");
    }

    /// <summary>
    /// A constant a pattern names, as JavaScript writes it, from its bound value: an enum's member as
    /// the value the twin holds (its camelCase name, or a flags enum's number), a decimal as the
    /// runtime's Decimal, and any other constant as its literal. The name parsed as a TYPE
    /// (<c>Limits.Max</c> is a qualified name there), so converting the syntax wrote it as it was
    /// spelled, a class nothing defines.
    /// </summary>
    private static string ConstantOf(IOperation converted, ExpressionSyntax spelled, ConversionContext context)
    {
        // The constant as the pattern compares it is the CONVERTED one: `long x; x is Limits.Max`
        // over an int const compares with 5L, a BigInt, where the const's own value wrote 5. What it
        // converts from names an enum's member.
        var value = converted;
        while (value is IConversionOperation conversion) value = conversion.Operand;
        if (value is IFieldReferenceOperation { Field: { ContainingType.TypeKind: TypeKind.Enum, HasConstantValue: true } member })
        {
            return member.ContainingType.IsFlagsEnum()
                ? System.Convert.ToDecimal(member.ConstantValue, System.Globalization.CultureInfo.InvariantCulture)
                    .ToString(System.Globalization.CultureInfo.InvariantCulture)
                : $"'{member.Name.ToCamelCase()}'";
        }
        // An enum's value reached otherwise (`x is (Level)1`) is the member that holds it.
        if (value.Type is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType && !enumType.IsFlagsEnum()
            && value.ConstantValue is { HasValue: true } enumValue
            && enumType.GetMembers().OfType<IFieldSymbol>().FirstOrDefault(field =>
                field.HasConstantValue && Equals(field.ConstantValue, enumValue.Value)) is { } named)
            return $"'{named.Name.ToCamelCase()}'";
        var constant = converted.ConstantValue.HasValue ? converted.ConstantValue : value.ConstantValue;
        if (constant is { HasValue: true, Value: decimal exact })
        {
            context.UsedHelpers.Add(Eq.Import);
            return $"{Eq.Dec}({JsStringLiteral.Quote(exact.ToString(System.Globalization.CultureInfo.InvariantCulture))})";
        }
        if (constant is { HasValue: true } known && ConstantLiteral.Write(known.Value, null) is { } literal)
            return literal;
        return context.Converter.ConvertExpression(spelled);
    }

    public int Priority => 10;

    /// <summary>
    /// A test written over <paramref name="subject"/>, a placeholder for <paramref name="expr"/>,
    /// with the subject read ONCE, as C# reads it: bound by an arrow when the test, a subpattern or a
    /// binding names it more than once (`Read() is char` called Read twice, and the second answer
    /// could be another one), and written in its place when it names it once.
    /// </summary>
    private static string Once(string tested, string subject, string expr)
    {
        var uses = System.Text.RegularExpressions.Regex.Matches(tested,
            System.Text.RegularExpressions.Regex.Escape(subject) + @"(?![\w$])").Count;
        return uses > 1 ? $"(({subject}) => {tested})({expr})" : tested.Replace(subject, expr);
    }

    /// <summary>A name, or names joined by dots (<c>this.items</c>): read again, it reads the same
    /// thing, as the conversion has always assumed of a path.</summary>
    private static bool IsPath(string converted) =>
        System.Text.RegularExpressions.Regex.IsMatch(converted, @"^[A-Za-z_$][\w$]*(\.[A-Za-z_$][\w$]*)*$");

    /// <summary>
    /// Whether the pattern is `{ } name` — no type, no positional or property subpatterns, one
    /// binding. Presence is the whole test, so assigning IS the test. `x is string s` is not this:
    /// it must not assign when the type does not match.
    /// </summary>
    private static bool IsPresenceOnly(PatternSyntax pattern) =>
        pattern is RecursivePatternSyntax
        {
            Type: null,
            PositionalPatternClause: null,
            Designation: SingleVariableDesignationSyntax,
        } recursive
        && (recursive.PropertyPatternClause is null || recursive.PropertyPatternClause.Subpatterns.Count == 0);
}
