using System.Linq;
using eQuantic.UI.Compiler;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// Iterator methods. A C# iterator is LAZY; the emitted one MATERIALISES into an array, because
/// every sequence in the emitted world is an array and a JS generator would read as undefined the
/// moment a LINQ operator touched it. The trade is invisible for the sequences UI code builds — and
/// impossible for one that never ends, which is what EQ2005 is for.
/// </summary>
public class IteratorTests
{
    private const string Header =
        "using System; using System.Collections.Generic; using System.Linq; " +
        "using eQuantic.UI.Core; using eQuantic.UI.Components; namespace App; ";

    private static CompilationResult Compile(string members) =>
        new ComponentCompiler().CompileSource(
            Header + "public class C : StatelessComponent { " + members +
            " public override IComponent Build(RenderContext c) => new Text(\"x\"); }")
            .Single(r => r.ComponentName == "C");

    private static string Ts(string members) => Compile(members).TypeScript;

    [Fact]
    public void AYieldingMethod_FillsABufferAndReturnsIt()
    {
        var ts = Ts("private IEnumerable<int> Evens(int n) { " +
                    "  for (var i = 0; i < n; i++) { if (i % 2 == 0) yield return i; } }");

        ts.Should().Contain("const _seq = []")
          .And.Contain("_seq.push(i)")
          .And.Contain("return _seq")
          .And.NotContain("yield ", "a generator would read as undefined the moment LINQ touched it");
    }

    [Fact]
    public void YieldBreak_ReturnsWhatWasCollectedSoFar()
    {
        var ts = Ts("private IEnumerable<string> Upto(List<string> src) { " +
                    "  foreach (var s in src) { if (s == \"stop\") yield break; yield return s; } }");

        ts.Should().Contain("if (s === 'stop') return _seq;");
    }

    [Fact]
    public void ALambdaInsideAnIterator_KeepsItsOwnReturn()
    {
        // The buffer belongs to the METHOD. A lambda's `return` is the lambda's, and must not be
        // rewritten into the iterator's `return _seq`.
        var ts = Ts("private IEnumerable<int> Ones() { " +
                    "  var make = () => { var acc = new List<int>(); acc.Add(1); return acc; }; " +
                    "  foreach (var x in make()) yield return x; }");

        ts.Should().Contain("return acc;").And.Contain("_seq.push(x)");
    }

    [Fact]
    public void AMethodThatOnlyYieldsInsideALambda_IsNotAnIterator()
    {
        // The yield belongs to the local function; the method around it returns an ordinary value.
        var ts = Ts("private List<int> Wrap() { " +
                    "  IEnumerable<int> inner() { yield return 1; } " +
                    "  return inner().ToList(); }");

        ts.Should().NotContain("const _seq = []; return inner",
            "the method itself yields nothing");
    }

    // ---- EQ2005: the one shape materialisation cannot serve --------------------------------------

    private static bool Endless(string members) =>
        Compile(members).Errors.Any(e => e.Code == "EQ2005");

    [Fact]
    public void AnIteratorThatNeverEnds_IsAnError()
    {
        // Perfectly ordinary C#: the caller stops taking. Eagerly materialised it is a frozen tab,
        // and a frozen tab says nothing about why.
        Endless("private IEnumerable<int> Naturals() { var i = 0; while (true) { yield return i++; } }")
            .Should().BeTrue();
    }

    [Theory]
    [InlineData("for (;;) { yield return 1; }")]
    [InlineData("do { yield return 1; } while (true);")]
    [InlineData("while (true) yield return 1;")]                       // no block around the yield
    [InlineData("while (true) { for (var i = 0; i < 3; i++) { break; } yield return 1; }")]
    [InlineData("while (true) { switch (1) { case 1: break; } yield return 1; }")]
    public void EveryEndlessShape_IsCaught(string loop)
    {
        // The last two matter most: those breaks belong to the inner loop and to the switch. Neither
        // is a way out of the endless one, and reading them as one would let a hang through.
        Endless($"private IEnumerable<int> Go() {{ {loop} }}").Should().BeTrue();
    }

    [Theory]
    [InlineData("while (true) { yield return 1; yield break; }")]
    [InlineData("while (true) { yield return 1; break; }")]
    [InlineData("while (true) { yield return 1; return; }")]
    [InlineData("while (true) { yield return 1; throw new Exception(\"done\"); }")]
    [InlineData("var n = 0; while (n < 10) { yield return n++; }")]
    [InlineData("foreach (var x in new[] { 1, 2 }) yield return x;")]
    [InlineData("while (true) { switch (1) { case 1: yield break; } yield return 1; }")]
    [InlineData("while (true) { switch (1) { case 1: return; } yield return 1; }")]
    public void ALoopWithAWayOut_IsFine(string loop)
    {
        Endless($"private IEnumerable<int> Go() {{ {loop} }}").Should().BeFalse();
    }

    [Fact]
    public void AnEndlessLoopThatYieldsNothing_IsNotOurBusiness()
    {
        // Not an iterator at all — whatever this method is doing, materialisation has no part in it.
        Endless("private int Spin() { while (true) { } }").Should().BeFalse();
    }

    [Fact]
    public void AnEndlessLoopInsideALambda_IsNotTheIteratorsFault()
    {
        Endless("private IEnumerable<int> Go() { " +
                "  Action spin = () => { while (true) { } }; " +
                "  yield return 1; }").Should().BeFalse();
    }
}
