using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// `out` and `ref`, which JavaScript does not have.
/// <para>
/// The argument `out var caret` used to emit the bare name `caret` — never declared, never
/// assigned. A module is always strict, so the first execution is a ReferenceError, and the build
/// said nothing. Found by transpiling a document model whose `Replace` hands back where the caret
/// ended up: it "compiled", and could not perform one edit.
/// </para>
/// </summary>
public class OutParameterTests
{
    private static string Convert(string members) => TestHelper.ConvertClass(members);

    [Fact]
    public void AMethodWithAnOutParameterReturnsItAlongsideItsValue()
    {
        var js = Convert("""
            public string Head(string text, int at, out int rest)
            {
                rest = text.Length - at;
                return text.Substring(0, at);
            }
            """);

        js.Should().Contain("head(text: string, at: number)", "an out parameter is not passed IN");
        js.Should().Contain("let rest: any;");
        js.Should().Contain("return { $: $r, rest };", "the value and the out come back together");
    }

    /// <summary>
    /// An out is declared in TypeScript by its type where the type has one to say, as an out var is (#1865), and
    /// <c>any</c> otherwise, definitely assigned either way: the body assigns it inside the arrow it runs in, which
    /// TypeScript cannot follow. Left untyped, an out assigned an empty list read as an implicit <c>any[]</c>, which
    /// fails the runtime's type check (TS7005, #1034), and declared <c>any</c>, a list walked through it inferred
    /// <c>unknown</c> elements (TS18046).
    /// </summary>
    [Fact]
    public void AnOutInTypeScript_IsDeclaredByItsType_SoAnEmptyListTypeChecks()
    {
        var js = Convert("""
            public bool Take(out List<int> taken, out int count)
            {
                taken = [];
                count = 0;
                return true;
            }
            """);

        js.Should().Contain("let taken!: number[], count: any;");
    }

    [Fact]
    public void AndTheCallSiteUnwrapsThemBOTH()
    {
        var js = Convert("""
            public string Head(string text, int at, out int rest)
            {
                rest = text.Length - at;
                return text.Substring(0, at);
            }

            public string Use(string text)
            {
                var head = Head(text, 2, out var rest);
                return head + rest;
            }
            """);

        js.Should().Contain("let rest: any;", "the variable exists before the call assigns it");
        js.Should().Contain("($o => (rest = $o.rest, $o.$))(this.head(text, 2))");
    }

    /// <summary>
    /// The reason for the arrow rather than a rewritten statement: `if (TryX(out var v))` is where
    /// half of all `out` in C# lives, and an expression is the only thing that fits inside an `if`.
    /// </summary>
    [Fact]
    public void ItWorksWhereOutIsMostUsED_InsideACondition()
    {
        var js = Convert("""
            public bool TryHead(string text, out string head)
            {
                head = text;
                return text.Length > 0;
            }

            public string Use(string text)
            {
                if (TryHead(text, out var head)) return head;
                return "";
            }
            """);

        js.Should().Contain("if (($o => (head = $o.head, $o.$))(this.tryHead(text)))");
    }

    [Fact]
    public void ADiscardAssignsNothing()
    {
        var js = Convert("""
            public bool TryHead(string text, out string head)
            {
                head = text;
                return text.Length > 0;
            }

            public bool Use(string text) => TryHead(text, out _);
            """);

        js.Should().Contain("($o => ($o.$))(this.tryHead(text))");
        js.Should().NotContain("_ = $o.", "there is nothing to assign a discard to");
    }

    /// <summary>`ref` is read before it is written, so unlike `out` it stays an argument too.</summary>
    [Fact]
    public void RefIsPassedInAndCarriedBack()
    {
        var js = Convert("""
            public void Bump(ref int count)
            {
                count = count + 1;
            }

            public int Use()
            {
                var total = 0;
                Bump(ref total);
                return total;
            }
            """);

        js.Should().Contain("bump(count: number)");
        js.Should().Contain("return { $: $r, count };");
        js.Should().Contain("($o => (total = $o.count, $o.$))(this.bump(total))");
    }

    /// <summary>Every `return` in the body still means what it meant: the body runs as a closure,
    /// so no return statement had to be rewritten and none could be missed.</summary>
    [Fact]
    public void EveryReturnPathStillReturns()
    {
        var js = Convert("""
            public int Find(string text, char c, out bool found)
            {
                found = false;
                for (var i = 0; i < text.Length; i++)
                {
                    if (text[i] == c) { found = true; return i; }
                }
                return -1;
            }
            """);

        js.Should().Contain("const $r = (() => {");
        js.Should().Contain("return i;", "the inner returns are untouched");
        js.Should().Contain("return { $: $r, found };");
    }
}
