using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Broad LINQ coverage sweep (beyond the core operators already covered). Drives the .NET coverage
/// program — failures are triaged into a native strategy, a TS compat helper, or fail-on-unsupported.
/// </summary>
public class LinqTotalityConformanceTests
{
    [SkippableTheory]
    // Projection / flattening
    [InlineData("new[]{1,2,3}.SelectMany(x => new[]{x, x * 10}).ToList()")]      // [1,10,2,20,3,30]
    [InlineData("new[]{1,2}.Concat(new[]{3,4}).ToList()")]                       // [1,2,3,4]
    [InlineData("new[]{10,20,30}.Select((x, i) => x + i).ToList()")]            // indexed Select [10,21,32]
    [InlineData("new[]{10,20,30,40}.Where((x, i) => i % 2 == 0).ToList()")]     // indexed Where [10,30]
    // Partitioning
    [InlineData("new[]{1,2,3,4,1}.TakeWhile(x => x < 3).ToList()")]             // [1,2]
    [InlineData("new[]{1,2,3,4,1}.SkipWhile(x => x < 3).ToList()")]             // [3,4,1]
    [InlineData("new[]{1,2,3,4,5}.Chunk(2).Count()")]                          // 3
    // Aggregations with selectors
    [InlineData("new[]{1,2,3}.Sum(x => x * 2)")]                                // 12
    [InlineData("new[]{1,2,3}.Average(x => x * 2)")]                            // 4
    [InlineData("new[]{1,2,3}.Max(x => -x)")]                                   // -1
    [InlineData("new[]{1,2,3}.Min(x => -x)")]                                   // -3
    // By-key
    [InlineData("new[]{\"a\",\"bbb\",\"cc\"}.MaxBy(s => s.Length)")]            // "bbb"
    [InlineData("new[]{\"a\",\"bbb\",\"cc\"}.MinBy(s => s.Length)")]            // "a"
    [InlineData("new[]{\"a\",\"bb\",\"cc\"}.DistinctBy(s => s.Length).Count()")] // 2
    // Grouping / dictionaries (scalar projections to keep JSON clean)
    [InlineData("new[]{1,2,3,4}.GroupBy(x => x % 2).Count()")]                  // 2
    [InlineData("new[]{1,2,3}.ToDictionary(x => x, x => x * 10)[2]")]           // 20
    // Zip
    [InlineData("new[]{1,2,3}.Zip(new[]{10,20,30}, (a, b) => a + b).ToList()")] // [11,22,33]
    // Materialization
    [InlineData("new[]{3,1,2}.OrderBy(x => x).ToArray()")]                      // [1,2,3]
    public void Linq_MatchesDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }
}
