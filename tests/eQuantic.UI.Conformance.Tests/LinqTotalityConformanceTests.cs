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

    /// <summary>
    /// The operators that had exactly ONE case before slice (1) of the coverage plan. One case
    /// proves the happy path and nothing else, and the edges are where a JavaScript array and a
    /// .NET sequence part company: a JS `reverse`/`sort` MUTATES its receiver where LINQ never
    /// touches the source, an empty sequence makes `All` vacuously true, a count outside the
    /// sequence is clamped rather than an error, ties keep the FIRST element met, and `OrderBy` is
    /// stable. Statement-shaped so the source can be inspected after the operator ran.
    /// </summary>
    [SkippableTheory]
    [InlineData("var a = new[]{1,2,3}; var r = a.Reverse().ToList(); return a[0] + \",\" + r[0];")]
    [InlineData("var a = new[]{3,1,2}; var r = a.Order().ToList(); return a[0] + \",\" + r[0];")]
    [InlineData("var a = new[]{3,1,2}; var r = a.OrderDescending().ToList(); return a[0] + \",\" + r[0];")]
    [InlineData("var a = new[]{1,2}; var r = a.Append(3).ToList(); return a.Length + \",\" + r.Count;")]
    [InlineData("var a = new[]{1,2}; var r = a.Prepend(0).ToList(); return a.Length + \",\" + r.Count;")]
    [InlineData("return new[]{1,2,3}.Skip(9).Count();")]                          // 0
    [InlineData("return new[]{1,2,3}.Skip(-1).Count();")]                         // 3
    [InlineData("return new[]{1,2,3}.Take(9).Count();")]                          // 3
    [InlineData("return new[]{1,2,3}.Take(-1).Count();")]                         // 0
    [InlineData("return string.Join(\",\", new[]{1,2,3,4}.Skip(1).Take(2));")]    // "2,3"
    [InlineData("return new int[0].All(x => x > 100);")]                          // true
    [InlineData("return new int[0].Any();")]                                      // false
    [InlineData("return new[]{1,2,3}.SkipWhile(x => x > 100).Count();")]          // 3
    [InlineData("return new[]{1,2,3}.TakeWhile(x => x > 100).Count();")]          // 0
    [InlineData("return new[]{\"bb\",\"aa\",\"c\"}.MaxBy(s => s.Length);")]      // "bb"
    [InlineData("return new[]{\"c\",\"bb\",\"a\"}.MinBy(s => s.Length);")]       // "c"
    [InlineData("return string.Join(\",\", new[]{\"ax\",\"ay\",\"b\"}.DistinctBy(s => s[0]));")] // "ax,b"
    [InlineData("return string.Join(\",\", new[]{\"bb\",\"aa\",\"cc\",\"d\"}.OrderBy(s => s.Length));")] // "d,bb,aa,cc"
    [InlineData("return string.Join(\",\", new[]{31,12,21,11}.OrderBy(x => x % 10));")]                     // "31,21,11,12"
    [InlineData("return string.Join(\",\", new[]{\"aa\",\"bb\"}.UnionBy(new[]{\"ac\",\"cc\"}, s => s[0]));")]     // "aa,bb,cc"
    [InlineData("return string.Join(\",\", new[]{\"aa\",\"bb\"}.IntersectBy(new[]{'a'}, s => s[0]));")]              // "aa"
    [InlineData("return string.Join(\",\", new[]{\"aa\",\"bb\"}.ExceptBy(new[]{'a'}, s => s[0]));")]                 // "bb"
    [InlineData("return string.Join(\"|\", new[]{1,2,3,4,5}.Chunk(2).Select(c => string.Join(\",\", c)));")] // "1,2|3,4|5"
    [InlineData("return new[]{1,2,2,3}.ToHashSet().Count;")]                       // 3
    [InlineData("return new[]{1,2}.ToDictionary(x => x, x => x * 10).Count;")]     // 2
    [InlineData("return new[]{1,2,3}.Zip(new[]{10,20}, (a, b) => a + b).Count();")] // 2 — the shorter wins
    [InlineData("return string.Join(\",\", new[]{1,2}.SelectMany((x, i) => new[]{x, i}));")] // "1,0,2,1"
    [InlineData("return new[]{1,2,3}.ElementAt(1);")]                              // 2
    [InlineData("return new[]{1,2,3}.Last(x => x < 3);")]                          // 2
    public void LinqEdges_MatchDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
