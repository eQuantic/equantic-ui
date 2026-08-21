using eQuantic.UI.Compiler.CodeGen.Ir;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.CodeGen;

/// <summary>
/// The structural nodes — members, indexes, calls, the author's parentheses — and the rules that
/// come with them: a receiver must be call-shaped, a bare number needs parentheses before a dot,
/// and a group is kept where the writer cannot see the surroundings and re-derived where it can.
/// </summary>
public class JsExprWriterStructureTests
{
    private static JsExpr Name(string n) => JsExpr.Identifier(n);
    private static string Write(JsExpr e) => JsExprWriter.Write(e);

    [Fact]
    public void MembersIndexesAndCalls_RenderAsChains()
    {
        var chain = JsExpr.Call(
            JsExpr.Member(JsExpr.Index(JsExpr.ThisMember("items"), JsExpr.Literal("0")), "toString"));
        Write(chain).Should().Be("this.items[0].toString()");
        Write(JsExpr.Call(Name("f"), Name("a"), JsExpr.Binary(Name("b"), "+", Name("c"))))
            .Should().Be("f(a, b + c)");
    }

    [Fact]
    public void ALooseReceiver_IsFenced()
    {
        Write(JsExpr.Member(JsExpr.Binary(Name("a"), "+", Name("b")), "length"))
            .Should().Be("(a + b).length");
        Write(JsExpr.Call(JsExpr.Member(JsExpr.Conditional(Name("c"), Name("x"), Name("y")), "run")))
            .Should().Be("(c ? x : y).run()");
        Write(JsExpr.Index(JsExpr.Prefix("-", Name("a")), JsExpr.Literal("0")))
            .Should().Be("(-a)[0]");
    }

    [Fact]
    public void ANumberBeforeADot_IsParenthesized()
    {
        // `1.toString()` reads the dot as a decimal point and does not parse.
        Write(JsExpr.Call(JsExpr.Member(JsExpr.Literal("1"), "toString"))).Should().Be("(1).toString()");
        Write(JsExpr.Call(JsExpr.Member(JsExpr.Literal("'s'"), "trim"))).Should().Be("'s'.trim()");
    }

    [Fact]
    public void AuthorParentheses_AreKeptWhereTheSurroundingsAreUnknown()
    {
        // Standing alone the text may be spliced anywhere by an unmigrated consumer, so the
        // author's parentheses stay exactly as written — this is what keeps the string world
        // byte-identical while the migration is in flight.
        Write(JsExpr.Group(JsExpr.Binary(Name("a"), "+", Name("b")))).Should().Be("(a + b)");
        Write(JsExpr.Group(JsExpr.Conditional(Name("c"), Name("a"), Name("b")))).Should().Be("(c ? a : b)");
    }

    [Fact]
    public void AuthorParentheses_AroundSomethingSelfDelimiting_GoEvenAtTheSeam()
    {
        // A name, a chain, a call or a string reads the same in every position — except a bare
        // number in front of a dot, which keeps its fence.
        Write(JsExpr.Group(Name("a"))).Should().Be("a");
        Write(JsExpr.Group(JsExpr.Group(Name("a")))).Should().Be("a");
        Write(JsExpr.Group(JsExpr.Call(Name("f"), Name("x")))).Should().Be("f(x)");
        Write(JsExpr.Group(JsExpr.Literal("'s'"))).Should().Be("'s'");
        Write(JsExpr.Group(JsExpr.Literal("1"))).Should().Be("(1)");
        Write(JsExpr.Group(JsExpr.Opaque("x"))).Should().Be("(x)");
    }

    [Fact]
    public void AuthorParentheses_AreRederivedWhereTheWriterCanSee()
    {
        // Inside a migrated parent the group is just its inside, re-fenced by the normal rule.
        Write(JsExpr.Binary(JsExpr.Group(Name("a")), "+", Name("b"))).Should().Be("a + b");
        Write(JsExpr.Binary(JsExpr.Group(JsExpr.Binary(Name("a"), "+", Name("b"))), "*", Name("c")))
            .Should().Be("(a + b) * c");
        Write(JsExpr.Member(JsExpr.Group(Name("list")), "length")).Should().Be("list.length");
        Write(JsExpr.Call(Name("f"), JsExpr.Group(JsExpr.Binary(Name("a"), "+", Name("b")))))
            .Should().Be("f(a + b)");
        Write(JsExpr.Binary(Name("a"), "??", JsExpr.Group(JsExpr.Binary(Name("b"), "&&", Name("c")))))
            .Should().Be("a ?? (b && c)");
    }

    [Fact]
    public void AuthorParentheses_AroundOpaqueText_AreNeverDropped()
    {
        // The writer cannot know how opaque text binds, so the author's fence around it stays.
        Write(JsExpr.Binary(JsExpr.Group(JsExpr.Opaque("a ? b : c")), "+", Name("d")))
            .Should().Be("(a ? b : c) + d");
        Write(JsExpr.Member(JsExpr.Group(JsExpr.Opaque("x")), "y")).Should().Be("(x).y");
    }

    [Fact]
    public void AStringIsOpaque_AndANodePrintsAsItsJavaScript()
    {
        JsExpr fromText = "a + b";
        fromText.Should().BeOfType<JsOpaque>();
        JsExpr.Binary(Name("a"), "*", Name("b")).ToString().Should().Be("a * b");
    }
}
