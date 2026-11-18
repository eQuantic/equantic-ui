using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The walks over a list changed under them that the browser refuses where .NET on a server answers, kept on
/// purpose and listed with the reason (#685, #863). .NET's documented contract for a collection changed while it
/// is enumerated is the InvalidOperationException, and the browser keeps it in every operator, as .NET running in
/// WebAssembly does: its LINQ is the one optimized for size, which enumerates the list through its enumerator. The
/// LINQ optimized for speed, which this suite's .NET runs, reads a <c>List&lt;T&gt;</c> under a <c>Where</c> or a
/// <c>Select</c> by a span of the length it had for the terminals it overrides (<c>ToList</c>, <c>ToArray</c>,
/// <c>Count</c>, <c>First</c>, <c>Last</c>, <c>ElementAt</c>, <c>Any()</c>), so it walks on without comparing the
/// version. That path is an optimization .NET does not promise and has moved between versions; following it would
/// tie the runtime to .NET's internals. Each row asserts both sides, so it leaves the list the day they agree.
/// </summary>
public class ListSpanDivergenceConformanceTests
{
    private const string Modified = "Collection was modified; enumeration operation may not execute.";

    private const string Reason = "the span path of .NET's speed-optimized LINQ, where the documented contract and "
        + "the size-optimized LINQ .NET runs in WebAssembly refuse the change";

    /// <summary>A list, a callback that adds to it at its first element, and the walk under test.</summary>
    private static string Walk(string query) =>
        "var l = new List<int> { 1, 2, 3 }; bool Adds(int x) { if (x == 1) l.Add(9); return true; } "
        + "int Grows(int x) { if (x == 1) l.Add(9); return x; } "
        + $"try {{ return {query}; }} catch (InvalidOperationException e) {{ return e.Message; }}";

    [SkippableTheory]
    [InlineData("l.Where(x => Adds(x)).ToList().Count.ToString()")]
    [InlineData("l.Where(x => Adds(x)).ToArray().Length.ToString()")]
    [InlineData("l.Where(x => Adds(x)).Count().ToString()")]
    [InlineData("l.Where(x => Adds(x) && x == 3).First().ToString()")]
    [InlineData("l.Where(x => Adds(x) && x == 3).FirstOrDefault().ToString()")]
    [InlineData("l.Where(x => Adds(x)).Last().ToString()")]
    [InlineData("l.Where(x => Adds(x)).LastOrDefault().ToString()")]
    [InlineData("l.Where(x => Adds(x)).ElementAt(2).ToString()")]
    [InlineData("l.Where(x => Adds(x) && x == 3).Any().ToString()")]
    [InlineData("l.Where(x => Adds(x)).Where(x => true).ToList().Count.ToString()")]
    [InlineData("l.Where(x => Adds(x)).Select(x => x).ToList().Count.ToString()")]
    [InlineData("l.Where(x => Adds(x)).Select(x => x).Count().ToString()")]
    [InlineData("l.Where(x => Adds(x) && x == 3).Select(x => x).First().ToString()")]
    [InlineData("l.Where(x => Adds(x)).OrderBy(x => x).ToList().Count.ToString()")]
    [InlineData("l.Select(x => Grows(x)).ToList().Count.ToString()")]
    [InlineData("l.Select(x => Grows(x)).ToArray().Length.ToString()")]
    [InlineData("l.Select(x => Grows(x)).Count().ToString()")]
    [InlineData("l.Select(x => Grows(x)).Select(x => x).ToList().Count.ToString()")]
    [InlineData("l.Select(x => Grows(x)).Contains(3).ToString()")]
    [InlineData("l.Select(x => Grows(x)).Take(3).ToList().Count.ToString()")]
    [InlineData("l.Take(3).Where(x => Adds(x)).ToList().Count.ToString()")]
    [InlineData("l.Skip(0).Select(x => Grows(x)).ToList().Count.ToString()")]
    public void AListChangedUnderASpanPath_IsRefusedInTheBrowser(string query)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsDivergeAsListed(Walk(query), Modified, Reason);
    }
}
