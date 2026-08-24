using System.Linq;
using eQuantic.UI.Compiler;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A user-defined operator belongs to the twin of ANY type that declares it, not only a record's.
/// <para>
/// The call site lowers `a + b` on two in-source objects to <c>T.opAdd(a, b)</c> whatever kind of
/// type T is — JavaScript cannot overload an operator, so there is nothing else it could do. A
/// record's emitter writes those methods; a plain class's emitter did not, and nothing failed the
/// build: the page compiled, the server rendered it, and the browser answered "T.opAdd is not a
/// function". Three of them on one page, for a class with two operators and a conversion.
/// </para>
/// <para>
/// The fixtures put a record and a class SIDE BY SIDE deliberately. That pairing is what surfaced
/// this, and it is what stops the two emitters drifting apart again.
/// </para>
/// </summary>
public class PlainClassOperatorTests
{
    private static string Source(string keyword) => $$"""
        using System.Collections.Generic;
        using eQuantic.UI.Core;
        using eQuantic.UI.Primitives;

        public sealed {{keyword}} Vec
        {
            public int X { get; init; }

            public static Vec operator +(Vec a, Vec b) => new() { X = a.X + b.X };
            public static Vec operator -(Vec a) => new() { X = -a.X };
            public static implicit operator Vec(int v) => new() { X = v };
            public static explicit operator int(Vec v) => v.X;
        }

        [Page("/vec")]
        public sealed class VecPage : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context)
                => new Text("x", TypeRole.BodyM);
        }
        """;

    private static string Twin(string keyword) => new ComponentCompiler()
        .CompileSource(Source(keyword), "Vec.cs")
        .Single(r => r.ComponentName == "Vec").TypeScript;

    [Theory]
    [InlineData("record")]
    [InlineData("class")]
    public void EveryUserDefinedOperatorReachesTheTwin(string keyword)
    {
        var twin = Twin(keyword);

        twin.Should().Contain("static opAdd(", "the call site lowers `a + b` to it");
        twin.Should().Contain("static opNegate(", "a unary operator is named by its arity");
        twin.Should().Contain("static fromInt(", "an implicit conversion is named by its direction");
        twin.Should().Contain("static toInt(", "and so is an explicit one");
    }

    [Fact]
    public void TheTwoEmittersNameTheOperatorsIdentically()
    {
        // Not just "both emit something" — the SAME names, because one call-site lowering serves
        // both and it has only one spelling to offer.
        var names = new[] { "opAdd", "opNegate", "fromInt", "toInt" };
        var record = Twin("record");
        var plain = Twin("class");

        names.Where(n => record.Contains($"static {n}("))
            .Should().BeEquivalentTo(names.Where(n => plain.Contains($"static {n}(")));
    }

    private static string OutVarSource(string keyword) => $$"""
        using eQuantic.UI.Core;
        using eQuantic.UI.Primitives;

        public sealed {{keyword}} Tag
        {
            public string Label { get; init; } = "";

            public static Tag operator +(Tag a, Tag b)
            {
                int.TryParse(a.Label, out var n);
                return new Tag { Label = (n + 1).ToString() };
            }
        }

        [Page("/tag")]
        public sealed class TagPage : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context)
                => new Text("x", TypeRole.BodyM);
        }
        """;

    [Theory]
    [InlineData("record")]
    [InlineData("class")]
    public void AnOperatorBodyDeclaresItsOutVariables(string keyword)
    {
        var twin = new ComponentCompiler()
            .CompileSource(OutVarSource(keyword), "Tag.cs")
            .Single(r => r.ComponentName == "Tag").TypeScript;

        // `out var n` emitted `n = parseInt(…)` with nothing declaring n. An ES module is strict, so
        // the operator threw a ReferenceError the first time it ran rather than returning a value —
        // and both emitters had it, which is why both are pinned here.
        var opAdd = twin[twin.IndexOf("opAdd", StringComparison.Ordinal)..];
        opAdd[..opAdd.IndexOf("return", StringComparison.Ordinal)]
            .Should().Contain("let n", "the hoisted declaration comes before the body that assigns it");
    }
}
