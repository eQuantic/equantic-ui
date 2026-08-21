using eQuantic.UI.Compiler.CodeGen.Ir;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.CodeGen;

/// <summary>
/// The single writer's contract, asserted on hand-built IR — no Roslyn, no strategies. Every
/// parenthesis in migrated output is decided here, so this is where the rules are pinned.
/// </summary>
public class JsExprWriterTests
{
    private static JsExpr Name(string n) => JsExpr.Opaque(n);

    private static string Write(JsExpr e) => JsExprWriter.Write(e);

    [Fact]
    public void TighterChild_NeedsNoParentheses()
    {
        Write(JsExpr.Binary(Name("a"), "+", JsExpr.Binary(Name("b"), "*", Name("c"))))
            .Should().Be("a + b * c");
    }

    [Fact]
    public void LooserChild_IsParenthesized()
    {
        Write(JsExpr.Binary(JsExpr.Binary(Name("a"), "+", Name("b")), "*", Name("c")))
            .Should().Be("(a + b) * c");
    }

    [Fact]
    public void Associativity_ProtectsTheSideTheOperatorGroupsAwayFrom()
    {
        // Same precedence on both sides, but only one grouping is the author's.
        Write(JsExpr.Binary(Name("a"), "-", JsExpr.Binary(Name("b"), "-", Name("c"))))
            .Should().Be("a - (b - c)");
        Write(JsExpr.Binary(JsExpr.Binary(Name("a"), "-", Name("b")), "-", Name("c")))
            .Should().Be("a - b - c");
    }

    [Fact]
    public void Exponent_GroupsRightAndFencesItsLeft()
    {
        Write(JsExpr.Binary(Name("a"), "**", JsExpr.Binary(Name("b"), "**", Name("c"))))
            .Should().Be("a ** b ** c");
        Write(JsExpr.Binary(JsExpr.Binary(Name("a"), "**", Name("b")), "**", Name("c")))
            .Should().Be("(a ** b) ** c");
    }

    [Theory]
    // THE bug this IR was built for: C# needs no parentheses here (&& binds tighter than ??),
    // and JavaScript refuses the bare mix — the file does not parse at all.
    [InlineData("??", "&&", "a ?? (b && c)")]
    [InlineData("??", "||", "a ?? (b || c)")]
    [InlineData("&&", "??", "a && (b ?? c)")]
    [InlineData("||", "??", "a || (b ?? c)")]
    public void NullishBesideLogical_IsAlwaysFenced(string outer, string inner, string expected)
    {
        Write(JsExpr.Binary(Name("a"), outer, JsExpr.Binary(Name("b"), inner, Name("c"))))
            .Should().Be(expected);
    }

    [Fact]
    public void NullishBesideNullish_NeedsNothing()
    {
        Write(JsExpr.Binary(JsExpr.Binary(Name("a"), "??", Name("b")), "??", Name("c")))
            .Should().Be("a ?? b ?? c");
    }

    [Fact]
    public void NullishChain_KeepsTheGroupingCSharpActuallyWrote()
    {
        // C#'s ?? groups to the RIGHT and JavaScript's to the LEFT, so `a ?? b ?? c` means
        // different trees in the two languages. It happens to yield the same value — ?? is
        // associative — but the writer transcribes the tree it was GIVEN rather than relying on
        // that: what it emits always parses back to the IR it came from.
        Write(JsExpr.Binary(Name("a"), "??", JsExpr.Binary(Name("b"), "??", Name("c"))))
            .Should().Be("a ?? (b ?? c)");
    }

    [Fact]
    public void Ternary_AsAnOperand_IsParenthesized()
    {
        Write(JsExpr.Binary(JsExpr.Conditional(Name("a"), Name("b"), Name("c")), "+", Name("d")))
            .Should().Be("(a ? b : c) + d");
    }

    [Fact]
    public void Ternary_TakesAConditionThatBindsTighterThanItself()
    {
        Write(JsExpr.Conditional(JsExpr.Binary(Name("a"), "??", Name("b")), Name("t"), Name("f")))
            .Should().Be("a ?? b ? t : f");
    }

    [Fact]
    public void NegatedNegation_DoesNotWeldIntoDecrement()
    {
        // `- -x` written as `--x` is not a negation any more, it is a mutation.
        Write(JsExpr.Prefix("-", JsExpr.Prefix("-", Name("x")))).Should().Be("-(-x)");
        Write(JsExpr.Prefix("+", JsExpr.Prefix("+", Name("x")))).Should().Be("+(+x)");
    }

    [Fact]
    public void PrefixOperator_FencesALooserOperand_AndSpacesWordOperators()
    {
        Write(JsExpr.Prefix("!", JsExpr.Binary(Name("a"), "===", Name("b")))).Should().Be("!(a === b)");
        Write(JsExpr.Prefix("typeof", Name("x"))).Should().Be("typeof x");
        Write(JsExpr.Prefix("!", Name("flag"))).Should().Be("!flag");
    }

    [Fact]
    public void ReceiverPosition_FencesAnythingLooserThanACall()
    {
        JsExprWriter.WriteIn(JsExpr.Binary(Name("a"), "+", Name("b")), JsPrecedence.Call)
            .Should().Be("(a + b)");
        JsExprWriter.WriteIn(JsExpr.Callish("f(x)"), JsPrecedence.Call).Should().Be("f(x)");
    }

    [Fact]
    public void OpaqueText_IsSplicedVerbatim_TheStranglerBoundary()
    {
        // Text from a strategy that has not migrated governs itself: the writer adds nothing,
        // which is precisely what the string world did. This is a LIMIT, recorded as a rule —
        // the fragment carries its own parentheses or is call-shaped already.
        Write(JsExpr.Binary(JsExpr.Opaque("a ? b : c"), "+", Name("d"))).Should().Be("a ? b : c + d");
        JsExprWriter.WriteIn(JsExpr.Opaque("a + b"), JsPrecedence.Call).Should().Be("a + b");
    }
}
