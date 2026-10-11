using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The members of .NET's <c>Queue&lt;T&gt;</c>, <c>Stack&lt;T&gt;</c> and <c>SortedSet&lt;T&gt;</c> that the
/// runtime's twins lacked, each a TypeError in the browser (#723), and the null source every collection
/// constructor refuses by its parameter's name, which the twins read as no collection (#726). Each runs
/// on both sides, refusals included.
/// <para>
/// A sorted set's union and intersection keep the instance .NET keeps of two its comparer finds equal,
/// which a decimal shows (<c>1.0m</c> and <c>1.00m</c>): the other set's where .NET merges two sets of
/// one comparer, and the set's own where it adds the other's elements one by one.
/// </para>
/// </summary>
public class CollectionMemberConformanceTests
{
    /// <summary>The statements, run in a <c>try</c> whose answer names the refusal: its type, its
    /// parameter where it has one, and its message.</summary>
    private static string Refused(string statements) =>
        "try { " + statements + " } "
        + "catch (ArgumentNullException e) { return \"null:\" + e.ParamName + \":\" + e.Message; } "
        + "catch (ArgumentOutOfRangeException e) { return \"range:\" + e.ParamName + \":\" + e.Message; } "
        + "catch (ArgumentException e) { return \"argument:\" + e.ParamName + \":\" + e.Message; }";

    [SkippableTheory]
    // Queue: front to back, from the index it is handed.
    [InlineData("var q = new Queue<int>(new[] { 1, 2, 3 }); q.Dequeue(); q.Enqueue(4); var a = new int[5]; q.CopyTo(a, 1); return string.Join(\",\", a);")] // 0,2,3,4,0
    [InlineData("var q = new Queue<int>(); var a = new int[2]; q.CopyTo(a, 2); return string.Join(\",\", a);")]                                       // 0,0
    // Stack: from the top, as it enumerates.
    [InlineData("var s = new Stack<int>(); s.Push(1); s.Push(2); s.Push(3); var a = new int[4]; s.CopyTo(a, 1); return string.Join(\",\", a);")] // 0,3,2,1
    // SortedSet: in its order, all of it or the count it is handed.
    [InlineData("var s = new SortedSet<int> { 3, 1, 2 }; var a = new int[4]; s.CopyTo(a); return string.Join(\",\", a);")]                         // 1,2,3,0
    [InlineData("var s = new SortedSet<int> { 3, 1, 2 }; var a = new int[4]; s.CopyTo(a, 1); return string.Join(\",\", a);")]                      // 0,1,2,3
    [InlineData("var s = new SortedSet<int> { 3, 1, 2 }; var a = new int[4]; s.CopyTo(a, 1, 2); return string.Join(\",\", a);")]                   // 0,1,2,0
    [InlineData("var s = new SortedSet<string> { \"b\", \"a\" }; var a = new string[2]; s.CopyTo(a); return string.Join(\",\", a);")]              // a,b
    public void CopyTo_WritesWhereDotNetWrites(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    [SkippableTheory]
    // Queue: an index past the array, or a room too small, each in .NET's words.
    [InlineData("var q = new Queue<int>(new[] { 1, 2, 3 }); q.CopyTo(new int[5], -1); return \"no\";")]
    [InlineData("var q = new Queue<int>(new[] { 1, 2, 3 }); q.CopyTo(new int[5], 6); return \"no\";")]
    [InlineData("var q = new Queue<int>(new[] { 1, 2, 3 }); q.CopyTo(new int[5], 3); return \"no\";")]
    [InlineData("var q = new Queue<int>(new[] { 1, 2, 3 }); q.CopyTo(null, 0); return \"no\";")]
    [InlineData("var s = new Stack<int>(new[] { 1 }); s.CopyTo(new int[2], 2); return \"no\";")]
    [InlineData("var s = new Stack<int>(new[] { 1 }); s.CopyTo(null, 0); return \"no\";")]
    [InlineData("var s = new SortedSet<int> { 3, 1, 2 }; s.CopyTo(new int[4], 2); return \"no\";")]
    [InlineData("var s = new SortedSet<int> { 3, 1, 2 }; s.CopyTo(new int[4], 1, 9); return \"no\";")]
    [InlineData("var s = new SortedSet<int> { 3, 1, 2 }; s.CopyTo(new int[2]); return \"no\";")]
    [InlineData("var s = new SortedSet<int>(); s.CopyTo(new int[2], 9); return \"no\";")]
    [InlineData("var s = new SortedSet<int> { 3, 1, 2 }; s.CopyTo(null); return \"no\";")]
    [InlineData("var s = new SortedSet<int> { 3, 1, 2 }; s.CopyTo(null, -1, -1); return \"no\";")]
    public void CopyTo_RefusesAsDotNetRefuses(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(Refused(statements));
    }

    /// <summary>
    /// A range refused with its actual value, which <c>ArgumentOutOfRangeException</c> writes after
    /// <c>Environment.NewLine</c>; a queue's refusal names no actual value, and a stack's does.
    /// </summary>
    [SkippableTheory]
    [InlineData("var s = new Stack<int>(new[] { 1 }); s.CopyTo(new int[2], -1); return \"no\";")]
    [InlineData("var s = new Stack<int>(new[] { 1 }); s.CopyTo(new int[2], 3); return \"no\";")]
    [InlineData("var s = new SortedSet<int> { 3, 1, 2 }; s.CopyTo(new int[4], -1); return \"no\";")]
    [InlineData("var s = new SortedSet<int> { 3, 1, 2 }; s.CopyTo(new int[4], 0, -1); return \"no\";")]
    [InlineData("var q = new Queue<int>(-1); return \"no\";")]
    [InlineData("var s = new Stack<int>(-1); return \"no\";")]
    public void CopyToAndCapacity_WhereDotNetAnswersTheHostsNewline(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNetExceptTheHostsNewline(
            Refused(statements), "an ArgumentOutOfRangeException writes its actual value after Environment.NewLine");
    }

    [SkippableTheory]
    // Each operation's set, an argument that repeats an element included.
    [InlineData("var s = new SortedSet<int> { 5, 1 }; s.UnionWith(new[] { 3, 1, 9 }); return string.Join(\",\", s);")]                          // 1,3,5,9
    [InlineData("var s = new SortedSet<int> { 1, 2 }; s.IntersectWith(new[] { 1, 1 }); return string.Join(\",\", s);")]                         // 1
    [InlineData("var s = new SortedSet<int> { 1, 2, 3, 4 }; s.IntersectWith(new SortedSet<int> { 4, 2, 9 }); return string.Join(\",\", s);")]    // 2,4
    [InlineData("var s = new SortedSet<int> { 1, 2, 3, 4 }; s.ExceptWith(new List<int> { 4, 2, 9 }); return string.Join(\",\", s);")]            // 1,3
    [InlineData("var s = new SortedSet<int> { 1, 2, 3, 4 }; s.ExceptWith(new SortedSet<int> { 0, 4, 2, 9 }); return string.Join(\",\", s);")]    // 1,3
    [InlineData("var s = new SortedSet<int> { 1, 2, 3 }; s.SymmetricExceptWith(new[] { 4, 2, 4, 2, 5 }); return string.Join(\",\", s);")]        // 1,3,4,5
    [InlineData("var s = new SortedSet<int> { 1, 2, 3 }; s.SymmetricExceptWith(new SortedSet<int> { 4, 2 }); return string.Join(\",\", s);")]   // 1,3,4
    [InlineData("var s = new SortedSet<int>(); s.SymmetricExceptWith(new[] { 2, 1, 2 }); return string.Join(\",\", s);")]                        // 1,2
    // The set itself as the argument.
    [InlineData("var s = new SortedSet<int> { 1, 2 }; s.ExceptWith(s); return s.Count;")]                                                         // 0
    [InlineData("var s = new SortedSet<int> { 1, 2 }; s.SymmetricExceptWith(s); return s.Count;")]                                                // 0
    [InlineData("var s = new SortedSet<int> { 1, 2 }; s.IntersectWith(s); s.UnionWith(s); return string.Join(\",\", s);")]                        // 1,2
    // The instance kept of two equal decimals: the other set's where two sets of one comparer merge,
    // the set's own where the other's elements are added one by one.
    [InlineData("var s = new SortedSet<decimal> { 1.0m, 2m }; s.UnionWith(new SortedSet<decimal> { 1.00m, 3m }); return string.Join(\",\", s);")]             // 1.00,2,3
    [InlineData("var s = new SortedSet<decimal> { 1.0m, 2m, 4m, 5m, 6m }; s.UnionWith(new SortedSet<decimal> { 1.00m, 3m }); return string.Join(\",\", s);")] // 1.0,2,3,4,5,6
    [InlineData("var s = new SortedSet<decimal> { 1.0m, 2m }; s.UnionWith(new List<decimal> { 1.00m, 3m }); return string.Join(\",\", s);")]                  // 1.0,2,3
    [InlineData("var s = new SortedSet<decimal>(); s.UnionWith(new SortedSet<decimal> { 1.00m, 3m }); return string.Join(\",\", s);")]                         // 1.00,3
    [InlineData("var s = new SortedSet<decimal> { 1.0m, 2m }; s.IntersectWith(new List<decimal> { 1.00m, 2.0m }); return string.Join(\",\", s);")]            // 1.00,2.0
    [InlineData("var s = new SortedSet<decimal> { 1.0m, 2m }; s.IntersectWith(new SortedSet<decimal> { 1.00m, 2.0m }); return string.Join(\",\", s);")]       // 1.00,2.0
    [InlineData("var s = new SortedSet<decimal> { 1.0m }; s.SymmetricExceptWith(new[] { 2.0m, 2.00m }); return string.Join(\",\", s);")]                     // 1.0,2.0
    // The questions, over an argument that repeats an element, the empty set and the set itself.
    [InlineData("var s = new SortedSet<int> { 1, 2 }; return s.IsSubsetOf(new[] { 2, 1, 1 }) + \",\" + s.IsProperSubsetOf(new[] { 2, 1, 1 }) + \",\" + s.IsProperSubsetOf(new[] { 2, 1, 3 }) + \",\" + s.IsSupersetOf(new[] { 1, 1 }) + \",\" + s.IsProperSupersetOf(new[] { 1, 1 }) + \",\" + s.IsProperSupersetOf(new[] { 1, 2 }) + \",\" + s.Overlaps(new[] { 5, 2 }) + \",\" + s.SetEquals(new[] { 2, 1, 2 }) + \",\" + s.SetEquals(new[] { 2, 1, 3 });")]
    [InlineData("var s = new SortedSet<int>(); return s.IsSubsetOf(new int[0]) + \",\" + s.IsProperSubsetOf(new int[0]) + \",\" + s.IsProperSubsetOf(new[] { 1 }) + \",\" + s.IsSupersetOf(new int[0]) + \",\" + s.IsProperSupersetOf(new int[0]) + \",\" + s.Overlaps(new int[0]) + \",\" + s.SetEquals(new int[0]) + \",\" + s.SetEquals(new[] { 1 });")]
    [InlineData("var s = new SortedSet<int> { 1 }; return s.IsSubsetOf(s) + \",\" + s.IsProperSubsetOf(s) + \",\" + s.IsSupersetOf(s) + \",\" + s.IsProperSupersetOf(s) + \",\" + s.Overlaps(s) + \",\" + s.SetEquals(s);")]
    [InlineData("var s = new SortedSet<int> { 1, 3 }; var o = new SortedSet<int> { 1, 2, 3 }; return s.IsSubsetOf(o) + \",\" + s.IsProperSubsetOf(o) + \",\" + o.IsSupersetOf(s) + \",\" + o.IsProperSupersetOf(s) + \",\" + s.Overlaps(new SortedSet<int> { 4, 5 }) + \",\" + s.SetEquals(new SortedSet<int> { 3, 1 });")]
    // A string's elements are its chars, and a culture order finds "a" before "B".
    [InlineData("var s = new SortedSet<char> { 'b', 'z' }; s.UnionWith(\"abc\"); s.ExceptWith(\"z\"); return string.Join(\",\", s) + \"|\" + s.SetEquals(\"cab\");")]
    [InlineData("var s = new SortedSet<string> { \"B\", \"c\" }; s.UnionWith(new[] { \"a\", \"C\" }); return string.Join(\",\", s) + \"|\" + s.Overlaps(new[] { \"b\" });")]
    // RemoveWhere asks the predicate of every element, then removes, and answers how many. The order
    // it asks in is its tree's in .NET and the set's here (#766), so the predicate keeps no record.
    [InlineData("var s = new SortedSet<int> { 3, 1, 2, 4 }; var n = s.RemoveWhere(x => x % 2 == 0); return n + \":\" + string.Join(\",\", s);")] // 2:1,3
    [InlineData("var s = new SortedSet<int> { 3, 1, 2 }; try { s.RemoveWhere(x => x == 3 ? throw new InvalidOperationException(\"x\") : x == 1); } catch (InvalidOperationException) { } return string.Join(\",\", s);")] // 1,2,3
    public void SortedSet_SetOperationsAnswerAsDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    [SkippableTheory]
    // A null argument, refused by its name, an empty set's included.
    [InlineData("var s = new SortedSet<int> { 1 }; s.UnionWith(null); return \"no\";")]
    [InlineData("var s = new SortedSet<int>(); s.IntersectWith(null); return \"no\";")]
    [InlineData("var s = new SortedSet<int>(); s.ExceptWith(null); return \"no\";")]
    [InlineData("var s = new SortedSet<int> { 1 }; s.SymmetricExceptWith(null); return \"no\";")]
    [InlineData("var s = new SortedSet<int>(); return s.IsSubsetOf(null);")]
    [InlineData("var s = new SortedSet<int> { 1 }; return s.IsProperSubsetOf(null);")]
    [InlineData("var s = new SortedSet<int> { 1 }; return s.IsSupersetOf(null);")]
    [InlineData("var s = new SortedSet<int> { 1 }; return s.IsProperSupersetOf(null);")]
    [InlineData("var s = new SortedSet<int>(); return s.Overlaps(null);")]
    [InlineData("var s = new SortedSet<int> { 1 }; return s.SetEquals(null);")]
    [InlineData("var s = new SortedSet<int> { 1 }; return s.RemoveWhere(null);")]
    public void SortedSet_RefusesANullArgumentByName(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(Refused(statements));
    }

    [SkippableTheory]
    // A null source, refused by the parameter it binds to, a comparer beside it or not.
    [InlineData("IEnumerable<int> source = null; var q = new Queue<int>(source); return \"no\";")]
    [InlineData("IEnumerable<int> source = null; var s = new Stack<int>(source); return \"no\";")]
    [InlineData("IEnumerable<int> source = null; var l = new LinkedList<int>(source); return \"no\";")]
    [InlineData("IEnumerable<int> source = null; var l = new List<int>(source); return \"no\";")]
    [InlineData("IEnumerable<int> source = null; var s = new SortedSet<int>(source); return \"no\";")]
    [InlineData("IEnumerable<string> source = null; var s = new SortedSet<string>(source, StringComparer.Ordinal); return \"no\";")]
    [InlineData("IDictionary<string, int> source = null; var d = new Dictionary<string, int>(source); return \"no\";")]
    [InlineData("IDictionary<string, int> source = null; var d = new Dictionary<string, int>(source, StringComparer.Ordinal); return \"no\";")]
    [InlineData("IEnumerable<KeyValuePair<string, int>> source = null; var d = new Dictionary<string, int>(source); return \"no\";")]
    [InlineData("IDictionary<string, int> source = null; var d = new SortedDictionary<string, int>(source); return \"no\";")]
    [InlineData("IDictionary<int, int> source = null; var d = new SortedDictionary<int, int>(source); return \"no\";")]
    [InlineData("IDictionary<string, int> source = null; var d = new SortedList<string, int>(source); return \"no\";")]
    public void Constructor_RefusesANullSourceByName(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(Refused(statements));
    }

    [SkippableTheory]
    // The constructions a null source must not disturb: none, a capacity, a comparer alone, a source.
    [InlineData("var q = new Queue<int>(); var s = new Stack<int>(4); q.Enqueue(1); s.Push(2); return q.Count + s.Count;")]                       // 2
    [InlineData("var q = new Queue<int>(new[] { 1, 2 }); var s = new Stack<int>(new List<int> { 1, 2 }); return q.Peek() + \",\" + s.Peek();")] // 1,2
    [InlineData("var s = new SortedSet<string>(StringComparer.Ordinal) { \"b\", \"B\" }; return string.Join(\",\", s);")]                        // B,b
    [InlineData("var s = new SortedSet<int>(new[] { 2, 1 }); var l = new LinkedList<int>(s); return string.Join(\",\", l);")]                    // 1,2
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1 }; var c = new Dictionary<string, int>(d); var p = new Dictionary<string, int>((IEnumerable<KeyValuePair<string, int>>)d); return c[\"a\"] + p[\"a\"];")] // 2
    [InlineData("var d = new SortedDictionary<string, int>(new Dictionary<string, int> { [\"b\"] = 2, [\"a\"] = 1 }); return string.Join(\",\", d.Keys);")] // a,b
    [InlineData("var d = new SortedList<int, int>(new Dictionary<int, int> { [2] = 2, [1] = 1 }); return string.Join(\",\", d.Keys);")]          // 1,2
    [InlineData("var l = new List<int>(new[] { 1, 2 }) { 3 }; var e = new List<int>(new HashSet<int> { 4 }); return string.Join(\",\", l) + \"|\" + e[0];")] // 1,2,3|4
    // A string's elements are its UTF-16 code units, an astral char two of them.
    [InlineData("return new Queue<char>(\"a\\U0001F600b\").Count + \",\" + new Stack<char>(\"ab\").Peek() + \",\" + new List<char>(\"a\\U0001F600b\").Count + \",\" + new LinkedList<char>(\"a\\U0001F600b\").Count;")] // 4,b,4,4
    public void Constructor_BuildsWhatDotNetBuilds(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
