using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>A <c>KeyValuePair</c>'s text is <c>[key, value]</c>, each half as its type writes it, wherever a
/// value is written: it printed as the array it is held as, <c>1,a</c> (#905).</summary>
public class PairTextConformanceTests
{
    [SkippableTheory]
    // The rows of #905.
    [InlineData("var d = new Dictionary<int, string> { [1] = \"a\" }; return d.First().ToString();")]
    [InlineData("return new KeyValuePair<string, int>(\"k\", 2).ToString();")]
    [InlineData("var p = new KeyValuePair<int, string>(1, \"a\"); return $\"{p}\";")]
    [InlineData("var p = new KeyValuePair<int, string>(1, \"a\"); return \"v=\" + p;")]
    [InlineData("var d = new Dictionary<int, string> { [1] = \"a\", [2] = \"b\" }; return string.Join(\";\", d);")]
    [InlineData("var p = new KeyValuePair<int, string>(1, \"a\"); return string.Format(\"{0}\", p);")]
    [InlineData("var p = new KeyValuePair<int, string>(1, \"a\"); return new System.Text.StringBuilder().Append(p).ToString();")]
    // Each half as its type writes it.
    [InlineData("return new KeyValuePair<string, string>(\"k\", null).ToString() + \"|\" + new KeyValuePair<bool, float>(true, 0.1f).ToString();")]
    [InlineData("return new KeyValuePair<Tone, (int, int)>(Tone.Warm, (1, 2)).ToString();")]
    [InlineData("return new KeyValuePair<int, KeyValuePair<int, int>>(1, new KeyValuePair<int, int>(2, 3)).ToString();")]
    [InlineData("return default(KeyValuePair<string, int>).ToString();")]
    // Held as object, the formatter reads the shape it has.
    [InlineData("object o = new KeyValuePair<int, string>(1, \"a\"); return $\"{o}\" + \"|\" + o.ToString();")]
    // A method group, a pair inside a tuple, and an empty Nullable one.
    [InlineData("var p = new KeyValuePair<int, string>(1, \"a\"); Func<string> f = p.ToString; return f();")]
    [InlineData("var p = new KeyValuePair<int, string>(1, \"a\"); return (p, 2).ToString();")]
    [InlineData("KeyValuePair<int, string>? p = null; return \"<\" + p + \">\" + $\"{p}\";")]
    [InlineData("KeyValuePair<int, string>? p = new KeyValuePair<int, string>(1, \"a\"); return p.ToString();")]
    public void APair_WritesItsTextAsDotNetDoes(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, "public enum Tone { Warm }");
    }

    /// <summary>A reference tuple has no form here: the build refuses it and says which member, where
    /// <c>Tuple.Create</c> built and threw a ReferenceError for <c>Tuple</c> in the browser (#904).</summary>
    [Theory]
    [InlineData("var t = Tuple.Create(1, \"a\"); return t.Item1;")]
    [InlineData("var t = System.Tuple.Create(1, \"a\"); return t.ToString();")]
    [InlineData("var t = new Tuple<int, string>(1, \"a\"); return t.Item1;")]
    public void AReferenceTuple_IsRefusedByTheBuild(string statements) =>
        ConformanceRunner.AssertStatementsRefusedByTheBuild(statements, "EQ2004");

    [SkippableFact]
    public void APair_WritesEachHalfInTheCulture()
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet("return new KeyValuePair<string, double>(\"k\", 1.5).ToString();", culture: "pt-BR");
    }
}
