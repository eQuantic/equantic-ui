using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A list's and an array's searches, finds and copies answer as .NET's do, on both sides.
/// <list type="bullet">
/// <item><c>IndexOf</c>, <c>LastIndexOf</c>, <c>Contains</c> and <c>Remove</c> compare as
/// <c>EqualityComparer&lt;T&gt;.Default</c> does, by ONE comparison the compiler chooses from the element
/// type (#425). <c>indexOf</c>'s <c>===</c> never found a NaN, a record or a tuple equal to the one in the
/// list, so <c>Contains</c> and <c>IndexOf</c> disagreed about one list; and a tuple holding an array
/// compared the array element by element, where <c>ValueTuple.Equals</c> compares it by reference.</item>
/// <item><c>Find</c> answers the element type's default for no match; <c>FindIndex</c> and
/// <c>FindLastIndex</c> take their ranges, which <c>findIndex</c> took for its predicate; <c>RemoveAll</c>
/// answers how many it removed (it threw a ReferenceError); <c>CopyTo</c> writes into the array it is
/// handed, where <c>[...list]</c> made a copy nothing read (#488). Every range is checked as .NET checks
/// it, in its order and in its words.</item>
/// </list>
/// </summary>
public class ListSearchConformanceTests
{
    private const string Prelude = "public record Point(int X, int Y);";

    [SkippableTheory]
    // The rows of #425: a NaN, a record, a tuple, found as the default comparer finds them.
    [InlineData("return new List<double> { double.NaN }.IndexOf(double.NaN);")]                                       // 0
    [InlineData("return new List<Point> { new Point(1, 2) }.IndexOf(new Point(1, 2));")]                               // 0
    [InlineData("return new List<(int, int)> { (1, 2) }.IndexOf((1, 2));")]                                           // 0
    [InlineData("return new List<(int, int)> { (1, 2), (1, 2) }.LastIndexOf((1, 2));")]                               // 1
    [InlineData("return Array.IndexOf(new[] { double.NaN }, double.NaN);")]                                            // 0
    [InlineData("return Array.LastIndexOf(new[] { double.NaN, 1 }, double.NaN);")]                                     // 0
    // A tuple's array element compares by reference, as ValueTuple.Equals compares it.
    [InlineData("var a = new[] { 1 }; var l = new List<(int[], int)> { (a, 1) }; return l.Contains((new[] { 1 }, 1)) + \"|\" + l.Contains((a, 1));")] // False|True
    [InlineData("var a = new[] { 1 }; var l = new List<(int[], int)> { (a, 1) }; return l.Remove((new[] { 1 }, 1)) + \"|\" + l.Count;")]             // False|1
    [InlineData("var a = new[] { 1 }; var l = new List<(int[], int)> { (a, 1) }; return l.IndexOf((a, 1)) + \"|\" + l.IndexOf((new[] { 1 }, 1));")]   // 0|-1
    [InlineData("var a = new[] { 1 }; IEnumerable<(int[], int)> l = new List<(int[], int)> { (a, 1) }; return l.Contains((new[] { 1 }, 1)) + \"|\" + l.Contains((a, 1));")]
    [InlineData("var a = new[] { 1 }; var l = new List<((int[], int), int)> { ((a, 1), 2) }; return l.Contains(((a, 1), 2)) + \"|\" + l.Contains(((new[] { 1 }, 1), 2));")]
    // An anonymous type's array member, by reference too; a pair, by each half.
    [InlineData("return new[] { new { A = 1, B = new[] { 1 } } }.ToList().IndexOf(new { A = 1, B = new[] { 1 } });")] // -1
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1 }; var pairs = new List<KeyValuePair<string, int>>(d); return pairs.IndexOf(pairs[0]);")]
    // An element typed object compares by what it holds: a decimal by value.
    [InlineData("return new List<object> { 1.0m }.IndexOf(1.00m) + \"|\" + new List<object> { 1.0m }.Contains(1.00m);")]
    [InlineData("return new[] { double.NaN }.AsEnumerable().Contains(double.NaN);")]
    // Contains, IndexOf and Remove agree about one list: of records, of tuples, of doubles with a NaN.
    [InlineData("var l = new List<Point> { new Point(1, 2), new Point(3, 4) }; var x = new Point(3, 4); return l.Contains(x) + \"|\" + l.IndexOf(x) + \"|\" + l.LastIndexOf(x) + \"|\" + l.Remove(x) + \"|\" + l.Count;")]
    [InlineData("var l = new List<(int, string)> { (1, \"a\"), (2, \"b\") }; var x = (2, \"b\"); return l.Contains(x) + \"|\" + l.IndexOf(x) + \"|\" + l.LastIndexOf(x) + \"|\" + l.Remove(x) + \"|\" + l.Count;")]
    [InlineData("var l = new List<double> { 1, double.NaN, double.NaN }; return l.Contains(double.NaN) + \"|\" + l.IndexOf(double.NaN) + \"|\" + l.LastIndexOf(double.NaN) + \"|\" + l.Remove(double.NaN) + \"|\" + l.Count;")]
    // The ranges, and .NET's words where one leaves the list.
    [InlineData("var l = new List<int> { 5, 6, 5 }; return l.IndexOf(5, 1) + \",\" + l.IndexOf(5, 3) + \",\" + l.IndexOf(5, 1, 1) + \",\" + l.LastIndexOf(5, 1) + \",\" + l.LastIndexOf(5, 1, 2);")] // 2,-1,-1,0,0
    [InlineData("return new List<int>().LastIndexOf(5, -1, 0) + \",\" + new List<int>().LastIndexOf(5);")]
    [InlineData("try { return new List<int> { 5, 6, 5 }.IndexOf(5, 4); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return new List<int> { 5, 6, 5 }.IndexOf(5, -1); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return new List<int> { 5, 6, 5 }.IndexOf(5, 1, 3); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return new List<int> { 5, 6, 5 }.LastIndexOf(5, 3); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return new List<int> { 5, 6, 5 }.LastIndexOf(5, 2, 4); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return new List<int> { 1 }.LastIndexOf(5, -1, 1); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return new List<int>().LastIndexOf(5, 5); } catch (Exception e) { return e.Message; }")]
    [InlineData("return Array.IndexOf(new[] { 1, 2, 1 }, 1, 1) + \",\" + Array.IndexOf(new[] { 1, 2, 1 }, 1, 1, 1) + \",\" + Array.LastIndexOf(new[] { 1, 2, 1 }, 1, 1);")] // 2,-1,0
    [InlineData("try { return Array.IndexOf(new[] { 1, 2, 1 }, 1, 4); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return Array.LastIndexOf(new[] { 1, 2, 1 }, 1, 3); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return Array.IndexOf((int[])null, 1); } catch (Exception e) { return e.Message; }")]
    public void Search_ComparesAsTheDefaultComparer(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }

    [SkippableTheory]
    // The rows of #488: a range for FindIndex, the element type's default for a Find with no match.
    [InlineData("return new List<int> { 5, 6, 7 }.FindIndex(1, x => x > 4);")]                                        // 1
    [InlineData("return $\"[{new List<int> { 1, 2 }.Find(x => x > 5)}]\";")]                                           // [0]
    [InlineData("return $\"[{new List<int> { 1, 2 }.FindLast(x => x > 5)}]\";")]                                       // [0]
    [InlineData("return $\"[{new List<string> { \"a\" }.Find(x => x == \"b\")}]\";")]                                   // []
    [InlineData("return new List<Point> { new Point(1, 2) }.Find(x => x.X > 5) == null;")]                             // True
    [InlineData("return new List<int> { 1, 2, 3, 2 }.Find(x => x > 1) + \",\" + new List<int> { 1, 2, 3, 2 }.FindLast(x => x < 3);")] // 2,2
    [InlineData("return Array.Find(new[] { 1, 2, 3 }, x => x > 1) + \",\" + Array.FindLast(new[] { 1, 2, 3 }, x => x > 1) + \",\" + Array.Find(new[] { 1 }, x => x > 5);")] // 2,3,0
    [InlineData("return Array.FindIndex(new[] { 5, 6, 7 }, 1, x => x > 4) + \",\" + Array.FindLastIndex(new[] { 5, 6, 7 }, x => x < 7);")] // 1,1
    [InlineData("var l = new List<int> { 5, 6, 7 }; return l.FindIndex(3, x => true) + \",\" + l.FindIndex(1, 2, x => x == 7) + \",\" + l.FindLastIndex(1, x => x > 4) + \",\" + l.FindLastIndex(2, 2, x => x == 5);")] // -1,2,1,-1
    [InlineData("return new List<int>().FindLastIndex(-1, x => true) + \",\" + new List<int>().FindLastIndex(x => true);")] // -1,-1
    [InlineData("try { return new List<int> { 5, 6, 7 }.FindIndex(4, x => true); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return new List<int> { 5, 6, 7 }.FindIndex(-1, x => true); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return new List<int> { 5, 6, 7 }.FindIndex(1, 3, x => x == 7); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return new List<int> { 5, 6, 7 }.FindLastIndex(3, x => x > 4); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return new List<int>().FindLastIndex(0, x => true); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return new List<int> { 5, 6, 7 }.FindIndex(0, 1, null); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return new List<int> { 1 }.Find(null); } catch (Exception e) { return e.Message; }")]
    public void Find_AnswersAsDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }

    [SkippableTheory]
    // RemoveAll answers how many it removed (#488): "2:1,3". The predicate is asked once per element,
    // in order, as .NET's single pass asks it.
    [InlineData("var l = new List<int> { 1, 2, 3, 4 }; var n = l.RemoveAll(x => x % 2 == 0); return n + \":\" + string.Join(\",\", l);")]
    [InlineData("var l = new List<int> { 1, 2, 3, 4, 5, 6 }; var n = l.RemoveAll(x => x > 2 && x < 5); return n + \":\" + string.Join(\",\", l);")]
    [InlineData("var l = new List<int> { 1, 2 }; return l.RemoveAll(x => x > 5) + \":\" + l.Count;")]
    [InlineData("var seen = new List<int>(); var l = new List<int> { 1, 2, 3 }; l.RemoveAll(x => { seen.Add(x); return x == 2; }); return string.Join(\",\", seen) + \"|\" + string.Join(\",\", l);")]
    [InlineData("try { return new List<int> { 1 }.RemoveAll(null); } catch (Exception e) { return e.Message; }")]
    // CopyTo writes into the array handed (#488): "1,2,0".
    [InlineData("var a = new int[3]; new List<int> { 1, 2 }.CopyTo(a); return string.Join(\",\", a);")]
    [InlineData("var a = new int[3]; new List<int> { 1, 2 }.CopyTo(a, 1); return string.Join(\",\", a);")]                 // 0,1,2
    [InlineData("var a = new int[3]; new List<int> { 1, 2 }.CopyTo(1, a, 0, 1); return string.Join(\",\", a);")]           // 2,0,0
    [InlineData("try { var a = new int[1]; new List<int> { 1, 2 }.CopyTo(a); return \"no\"; } catch (Exception e) { return e.Message; }")]
    [InlineData("try { var a = new int[3]; new List<int> { 1, 2 }.CopyTo(a, 2); return \"no\"; } catch (Exception e) { return e.Message; }")]
    [InlineData("try { var a = new int[3]; new List<int> { 1, 2 }.CopyTo(a, -1); return \"no\"; } catch (Exception e) { return e.Message; }")]
    [InlineData("try { var a = new int[3]; new List<int> { 1, 2 }.CopyTo(1, a, 0, 2); return \"no\"; } catch (Exception e) { return e.Message; }")]
    [InlineData("try { var a = new int[3]; new List<int> { 1, 2 }.CopyTo(-1, a, 0, 1); return \"no\"; } catch (Exception e) { return e.Message; }")]
    [InlineData("try { var a = new int[3]; new List<int> { 1, 2 }.CopyTo(0, a, 0, -1); return \"no\"; } catch (Exception e) { return e.Message; }")]
    [InlineData("try { var a = new int[3]; new List<int> { 1, 2 }.CopyTo(0, a, 3, 1); return \"no\"; } catch (Exception e) { return e.Message; }")]
    [InlineData("try { new List<int> { 1, 2 }.CopyTo(null); return \"no\"; } catch (Exception e) { return e.Message; }")]
    public void RemoveAllAndCopyTo_AnswerAsDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }
}
