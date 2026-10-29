using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
            var input = context.Converter.ConvertIr(isPattern.Expression);
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
            // The test is written over a name for the input, which C# reads ONCE, and the writer decides
            // how the input reaches it (Read).
            var subject = $"$v{isPattern.SpanStart}";
            // The test is ONE pattern-matching operation: a Deconstruct the app wrote is called once for the
            // value, however many alternatives read its parts (`t is (1, _) or (_, 4)`), and held where the
            // test's own arrow declares it (MatchParts, Holding).
            var ((condition, bindings), held) = context.MatchParts.In(isPattern.SpanStart, () =>
            {
                var built = PatternConverter.BuildCondition(pattern, subject, context, exprType);
                var collected = new List<(string Name, string Access)>();
                PatternConverter.CollectBindings(pattern, subject, context, collected, exprType);
                return (built, collected);
            });

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
            if (bindings.Count == 1 && bindings[0].Access == subject && IsPresenceOnly(pattern))
            {
                var assigned = $"({bindings[0].Name} = {subject}) != null";
                return Read(negated ? $"!({assigned})" : assigned, subject, input);
            }

            var bound = bindings.Count == 0
                ? condition
                : $"({condition} && ({string.Concat(bindings.Select(b => $"{b.Name} = {b.Access}, "))}true))";
            var tested = negated ? $"!({bound})" : bound;
            return Read(held.Count > 0 ? Holding(tested, held, context) : tested, subject, input);
        }

        if (node is BinaryExpressionSyntax binary)
        {
            var input = context.Converter.ConvertIr(binary.Left);
            // `x is Type` asks the SAME question a type pattern does, so it gets the same answer
            // from the same place. It used to carry a copy of the rule that stopped at the
            // primitives and answered `!= null` for everything else — so `node is Icon` was true for
            // any non-null node, silently.
            // …unless it BINDS as a constant: `x is Limits.Max` parses as the type test and binds as a
            // constant pattern over the const, and was answered `x != null` (#451).
            var subject = $"$v{binary.SpanStart}";
            string tested;
            if (context.SemanticHelper.GetOperation(binary) is IIsPatternOperation { Pattern: IConstantPatternOperation constant })
                tested = PatternConverter.ConstantTest(subject, ConstantOf(constant.Value, binary.Right, context), constant.Value, context);
            else if (binary.Right is TypeSyntax typeSyntax)
                tested = PatternConverter.TypeCheck(typeSyntax, subject, context);
            else
                tested = $"{subject} != null";
            return Read(tested, subject, input);
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
            return Types.EnumStrategy.MemberLiteral(member);
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
    /// A test written over <paramref name="subject"/>, a name no C# identifier can take, with the
    /// <paramref name="input"/> it stands for read ONCE, as C# reads it, whatever the pattern's shape.
    /// The test becomes a template whose hole is each place its CODE names the subject, and the writer
    /// decides single evaluation for it as for any template (<see cref="JsExprWriter"/>): the input is
    /// bound once where the test reads it more than once, and a plain name or a literal is written in
    /// its place. This strategy kept a rule of its own, which took a path of names (<c>c.Value</c>,
    /// <c>this.value</c>) for a read nobody could observe and read it again at each mention, where a
    /// property's getter runs each time (#1149).
    /// <para>
    /// An input the test never names is evaluated all the same, as C# evaluates it: <c>f() is var _</c>
    /// called nothing. The template is written here, as the text this strategy returns, which every
    /// consumer splices as it is.
    /// </para>
    /// </summary>
    private static string Read(string tested, string subject, JsExpr input)
    {
        var code = JsExprWriter.CodeOf(tested);
        var named = Placeholder.Matches(code).Where(name => name.Value == subject).ToList();
        // A name or a literal left out reads nothing anybody can observe, and TypeScript refuses one
        // left of a comma (TS2695): a text strategy's name is one too, though the writer cannot tell.
        if (named.Count == 0)
            return JsExprWriter.IsInlinable(input) || input is JsOpaque { Text: var text } && Name.IsMatch(text)
                ? tested
                : JsExprWriter.Write(JsExpr.Template("({0}, " + tested + ")", input));

        // The writer names what it binds `$0`, and the hole is in the test's own code: a function the
        // test defined with a `$0` of its own would take the hole's reads for its own. None does (the
        // arrow that holds a Deconstruct's parts declares `$p…`), and one that did would be a miscompile
        // nobody sees, so it stops the build instead.
        if (JsExprWriter.DeclaredBy(code).Contains("$0"))
            throw new InvalidOperationException(
                $"A pattern's test declares `$0`, the name the writer binds its input to: `{tested}`.");

        var holed = new StringBuilder(tested.Length);
        var from = 0;
        foreach (var name in named)
        {
            holed.Append(tested, from, name.Index - from).Append("{0}");
            from = name.Index + name.Length;
        }
        holed.Append(tested, from, tested.Length - from);
        return JsExprWriter.Write(JsExpr.Template(holed.ToString(), input));
    }

    /// <summary>A name the test is written over, <c>$v</c> and the span it starts at: a whole name,
    /// never the head of a longer one.</summary>
    private static readonly Regex Placeholder = new(@"(?<![\w$])\$v\d+(?![\w$])", RegexOptions.Compiled);

    /// <summary>A plain name, as a text strategy writes one.</summary>
    private static readonly Regex Name = new(@"^[A-Za-z_$][\w$]*$", RegexOptions.Compiled);

    /// <summary>
    /// A test whose operation holds the parts a <c>Deconstruct</c> the app wrote hands back
    /// (<see cref="MatchParts"/>), in an arrow of its own that declares where it holds them, fresh each
    /// time the test runs, so it needs no statement around it: an initializer called the
    /// <c>Deconstruct</c> once per part where no statement could declare a temporary (Copilot's second
    /// review of #696). The input it reads is read once outside it (<see cref="Read"/>), and an
    /// <c>await</c> in the input stays in the function it is written in.
    /// </summary>
    private static string Holding(string tested, IReadOnlyList<string> held, ConversionContext context)
    {
        var declared = ExpressionVariableScanner.Declarations(held, context.TypeAnnotations);
        return $"(() => {{ {declared}return {tested}; }})()";
    }

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
