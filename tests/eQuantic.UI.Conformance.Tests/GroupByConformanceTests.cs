using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// <c>GroupBy</c> across its selector shapes, executed on both sides. The element-selector form
/// was silently grouping the RAW items until the query-syntax differential lowered
/// <c>group w.ToUpper() by w.Length</c> onto it.
/// </summary>
public class GroupByConformanceTests
{
    private const string Words = "var ws = new List<string> { \"pear\", \"fig\", \"apple\", \"kiwi\" }; ";

    [SkippableTheory]
    [InlineData(Words + "return ws.GroupBy(w => w.Length).Select(g => g.Key).ToList();")]                          // [4,3,5]
    [InlineData(Words + "return ws.GroupBy(w => w.Length).Select(g => g.Count()).ToList();")]                      // [2,1,1]
    // element selector: what goes INTO each group is transformed
    [InlineData(Words + "return ws.GroupBy(w => w.Length, w => w.ToUpper()).Select(g => string.Join(\",\", g)).ToList();")]
    [InlineData(Words + "return ws.GroupBy(w => w.Length, w => w[0]).Select(g => g.Key + \":\" + string.Join(\"\", g)).ToList();")]
    // result selector: each finished group maps through (key, group)
    [InlineData(Words + "return ws.GroupBy(w => w.Length, (k, g) => k * 100 + g.Count()).ToList();")]            // [402,301,501]
    [InlineData(Words + "return ws.GroupBy(w => w.Length, (k, g) => string.Join(\"|\", g)).ToList();")]
    // element AND result selectors
    [InlineData(Words + "return ws.GroupBy(w => w.Length, w => w.ToUpper(), (k, g) => k + \"=\" + string.Join(\"+\", g)).ToList();")]
    public void GroupBy_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
