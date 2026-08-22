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
    // ── The operators the strategies implement and NOTHING executed, before slice (1) of the
    // coverage plan. A table that replaces 42 strategies can only be trusted against a net that
    // covers what those 42 claim; 14 of the 57 had no executed case at all.
    // Set operations
    [InlineData("new[]{1,2,3,2}.Union(new[]{3,4}).ToList()")]                   // [1,2,3,4]
    [InlineData("new[]{1,2,3}.Intersect(new[]{2,3,4}).ToList()")]               // [2,3]
    [InlineData("new[]{1,2,3}.Except(new[]{2}).ToList()")]                      // [1,3]
    [InlineData("new[]{1,2}.SequenceEqual(new[]{1,2})")]                        // true
    [InlineData("new[]{1,2}.SequenceEqual(new[]{2,1})")]                        // false
    // The single-element family, and what it does when the count is not one
    [InlineData("new[]{7}.Single()")]                                           // 7
    [InlineData("new[]{7}.SingleOrDefault()")]                                  // 7
    [InlineData("new int[0].SingleOrDefault()")]                                // 0
    [InlineData("new[]{1,2}.Where(x => x > 5).SingleOrDefault()")]              // 0
    [InlineData("new[]{1,2,3}.LastOrDefault()")]                                // 3
    [InlineData("new int[0].FirstOrDefault()")]                                 // 0
    [InlineData("new[]{1,2}.Where(x => x > 5).FirstOrDefault()")]               // 0
    [InlineData("new string[0].FirstOrDefault()")]                              // null
    [InlineData("new[]{1,2}.FirstOrDefault(x => x > 5)")]                       // 0
    [InlineData("new int[0].LastOrDefault()")]                                  // 0
    [InlineData("new[]{1,2,3}.ElementAtOrDefault(1)")]                          // 2
    [InlineData("new[]{1,2,3}.ElementAtOrDefault(9)")]                          // 0
    // Empty sequences and generators
    [InlineData("new int[0].DefaultIfEmpty().ToList()")]                        // [0]
    [InlineData("new int[0].DefaultIfEmpty(5).ToList()")]                       // [5]
    [InlineData("new[]{1}.DefaultIfEmpty(5).ToList()")]                         // [1]
    [InlineData("Enumerable.Empty<int>().Count()")]                             // 0
    [InlineData("Enumerable.Range(2, 4).ToList()")]                             // [2,3,4,5]
    [InlineData("Enumerable.Repeat(7, 3).ToList()")]                            // [7,7,7]
    [InlineData("Enumerable.Range(1, 5).Where(x => x % 2 == 1).Sum()")]         // 9
    // Type filtering: Cast throws where OfType skips
    [InlineData("new object[]{1, 2}.Cast<int>().Sum()")]                        // 3
    [InlineData("new object[]{1, \"a\", 2}.OfType<int>().Sum()")]              // 3
    [InlineData("new object[]{1, \"a\"}.OfType<string>().Count()")]            // 1
    public void Linq_MatchesDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }
}
