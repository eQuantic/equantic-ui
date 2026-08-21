using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// LINQ QUERY syntax executed on both sides. The lowering hands each clause to the method-syntax
/// strategies, so these cases are also a second execution of those — with the clause bodies
/// arriving as re-parented copies whose symbols come through the synthetic-node map.
/// </summary>
public class QuerySyntaxConformanceTests
{
    private const string Numbers = "var xs = new List<int> { 5, 1, 4, 2, 3 }; ";
    private const string Words = "var ws = new List<string> { \"pear\", \"fig\", \"apple\", \"kiwi\" }; ";

    [SkippableTheory]
    // where + select
    [InlineData(Numbers + "var q = from n in xs where n > 2 select n * 10; return q.Sum();")]          // 120
    [InlineData(Numbers + "return (from n in xs where n % 2 == 1 select n).ToList();")]                // [5,1,3]
    [InlineData(Numbers + "var q = from n in xs where n > 10 select n; return q.Any();")]              // false
    // degenerate and identity forms
    [InlineData(Numbers + "return (from n in xs select n).Count();")]                                  // 5
    [InlineData(Numbers + "return (from n in xs where n > 1 select n).ToList();")]                     // [5,4,2,3]
    // orderby: directions, several keys, stability
    [InlineData(Words + "return (from w in ws orderby w.Length, w select w).ToList();")]               // fig,kiwi,pear,apple
    [InlineData(Words + "return (from w in ws orderby w.Length descending, w descending select w).ToList();")]
    [InlineData("var ws = new List<string> { \"bb\", \"aa\", \"cc\", \"a\" }; "
                + "return (from w in ws orderby w.Length select w).ToList();")]                        // a,bb,aa,cc — stable
    [InlineData("var arr = new[] { 3, 1, 2 }; return (from a in arr orderby a select a).ToList();")]  // [1,2,3]
    // range-variable members inside the bodies
    [InlineData(Words + "return (from w in ws where w.Length > 3 orderby w select w.ToUpper()).ToList();")]
    [InlineData(Words + "return (from w in ws where w.StartsWith(\"p\") select w.Length).ToList();")]  // [4]
    // group … by
    [InlineData(Words + "return (from w in ws group w by w.Length).Select(g => g.Key).ToList();")]    // [4,3,5]
    [InlineData(Words + "return (from w in ws group w by w.Length).Select(g => g.Count()).ToList();")] // [2,1,1]
    [InlineData(Words + "return (from w in ws group w.ToUpper() by w.Length).Select(g => string.Join(\",\", g)).ToList();")]
    // a query as another query's source
    [InlineData("var xs = new List<int> { 1, 2, 3, 4 }; "
                + "return (from n in (from m in xs select m * 2) where n > 4 select n).ToList();")]    // [6,8]
    public void QuerySyntax_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
