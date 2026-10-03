using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// LINQ reads whatever sequence it is given, as .NET's does: a collection the runtime holds as a
/// class of its own (a set, a queue, a stack, a linked list, a sorted collection), a dictionary as its
/// pairs, and a string as its UTF-16 code units. Each operator was written for an array and called
/// a method these do not have, `TypeError: s.filter is not a function`, with no diagnostic (#434,
/// #439, #524). And what LINQ materializes is a copy, which the code that made it may change.
/// </summary>
public class LinqOverAnySequenceConformanceTests
{
    [SkippableTheory]
    [InlineData("var s = new SortedSet<int> { 3, 1, 2 }; var l = s.ToList(); return l.Count * 1000 + l[0] * 100 + l[1] * 10 + l[2];")] // 3123
    [InlineData("var s = new SortedSet<int>(new[] { 3, 1, 2 }); return s.Select(x => x * 2).ToArray()[2] + s.First();")]                  // 7
    [InlineData("var q = new Queue<int>(new[] { 4, 5, 6 }); return q.Where(x => x > 4).Sum() * 10 + q.First();")]                         // 114
    [InlineData("var t = new Stack<int>(new[] { 1, 2, 3 }); return t.First() * 10 + t.Last();")]                                           // 31
    [InlineData("var l = new LinkedList<int>(new[] { 1, 2, 3 }); return l.Select(x => x * x).Sum();")]                                     // 14
    [InlineData("var h = new HashSet<int> { 5, 6, 7 }; return h.Count(x => x > 5) * 10 + h.Min();")]                                       // 25
    [InlineData("var d = new SortedDictionary<string, int> { [\"b\"] = 2, [\"a\"] = 1 }; return d.First().Key + d.Sum(p => p.Value);")]   // "a3"
    [InlineData("var d = new SortedList<int, string> { [2] = \"two\", [1] = \"one\" }; return d.Select(p => p.Value).First();")]           // "one"
    public void ACollectionOfItsOwnClass_IsASequence(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    [SkippableTheory]
    [InlineData("var p = Pairs().First(kv => kv.Key == \"b\"); return p.Key + p.Value;")]   // "b2"
    [InlineData("return Pairs().Where(kv => kv.Value > 1).Count();")]                         // 1
    [InlineData("return Pairs().Select(kv => kv.Key).Last();")]                               // "b"
    [InlineData("var d = Pairs(); return d.Count() * 10 + d.ToList().Count;")]               // 22
    [InlineData("return Pairs().Any(kv => kv.Value == 2);")]                                  // true
    [InlineData("return Pairs().OrderByDescending(kv => kv.Value).First().Key;")]             // "b"
    [InlineData("return Pairs().Sum(kv => kv.Value);")]                                       // 3
    public void ADictionary_IsASequenceOfItsPairs(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(
            "Dictionary<string, int> Pairs() => new Dictionary<string, int> { [\"a\"] = 1, [\"b\"] = 2 }; " + statements);
    }

    [SkippableTheory]
    [InlineData("return \"a1b2\".Count(c => char.IsDigit(c));")]                              // 2
    [InlineData("return \"a1b2\".Count(char.IsDigit);")]                                      // 2
    [InlineData("return new string(\"a1b2\".Where(char.IsLetter).ToArray());")]               // "ab"
    [InlineData("return \"aB\".Any(c => char.IsUpper(c)) && \"aB\".All(c => char.IsLetter(c));")] // true
    [InlineData("return \"ab\".Select(c => (int)c).Sum();")]                                  // 195
    [InlineData("var n = 0; foreach (var c in \"a\\uD83D\\uDE00\") n++; return n;")]          // 3
    [InlineData("return \"a\\uD83D\\uDE00\".ToCharArray().Length;")]                         // 3
    [InlineData("return \"x\\uD83D\".Count();")]                                              // 2
    [InlineData("return new string(new[] { 'a', 'b' });")]                                     // "ab"
    [InlineData("return new string(new[] { 'a', 'b', 'c', 'd' }, 1, 2);")]                     // "bc"
    [InlineData("return new string('-', 3);")]                                                 // "---"
    [InlineData("return \"abc\".Reverse().First().ToString();")]                               // "c"
    [InlineData("return new string(\"abcdef\".ToCharArray(1, 3));")]                            // "bcd"
    [InlineData("return new string(length: 2, startIndex: 1, value: new[] { 'a', 'b', 'c' });")]   // "bc"
    [InlineData("char[] none = null; return new string(none).Length;")]                           // 0
    public void AString_IsASequenceOfCodeUnits(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>
    /// A sequence handed to an operator as its argument is read as one too, and an ordering reads its
    /// source through the same place: a set given to <c>Intersect</c> was asked <c>includes</c>, and
    /// <c>OrderBy</c> spread a string by code point.
    /// </summary>
    [SkippableTheory]
    [InlineData("var s = new HashSet<int> { 1, 2, 3 }; return new[] { 2, 3, 4 }.Intersect(s).Count();")]                     // 2
    [InlineData("return new[] { 1, 2, 3 }.Except(new Queue<int>(new[] { 2 })).Sum();")]                                       // 4
    [InlineData("return new[] { 1, 2 }.Zip(new Stack<int>(new[] { 10, 20 }), (a, b) => a + b).Sum();")]                        // 33
    [InlineData("return new[] { 'a', 'b' }.SequenceEqual(\"ab\");")]                                                           // true
    [InlineData("return new[] { 1 }.Concat(new HashSet<int> { 2, 3 }).Count();")]                                             // 3
    [InlineData("return \"a\\uD83D\\uDE00\".OrderBy(c => c).Count();")]                                                     // 3
    public void ASequenceArgument_IsASequenceToo(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>A range of chars is refused where it leaves its source, as .NET refuses it: `slice`
    /// clamped it, counting a negative start from the end.</summary>
    [SkippableTheory]
    [InlineData("try { return new string(new[] { 'a', 'b', 'c' }, -1, 1); } catch { return \"refused\"; }")] // "refused"
    [InlineData("try { return new string(new[] { 'a', 'b', 'c' }, 1, 5); } catch { return \"refused\"; }")]  // "refused"
    [InlineData("try { return \"abc\".ToCharArray(-1, 1).Length; } catch { return -1; }")]                   // -1
    [InlineData("try { return \"abc\".ToCharArray(1, 5).Length; } catch { return -1; }")]                    // -1
    public void ACharRange_IsRefusedWhereItLeavesItsSource(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>A char static reached by its name alone, under <c>using static System.Char</c>, is the
    /// same call and the same group as through its type.</summary>
    [SkippableTheory]
    [InlineData("return \"a1b2\".Count(IsDigit);")]           // 2
    [InlineData("return IsLetter('x') && !IsDigit('x');")]    // true
    public void AUsingStaticChar_IsTheSameMethod(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, "using static System.Char;");
    }

    [SkippableTheory]
    [InlineData("var a = new List<int> { 3, 1 }; var b = a.ToList(); b.Add(9); return a.Count;")]   // 2
    [InlineData("var a = new List<int> { 3, 1 }; var b = a.ToArray(); b[0] = 7; return a[0];")]   // 3
    [InlineData("var a = new List<int> { 3, 1 }; var b = a.ToList(); b.Sort(); return a[0];")]    // 3
    [InlineData("var a = new List<int> { 3, 1 }; IReadOnlyCollection<int> r = a; var b = r.ToList(); b.Sort(); return a[0];")] // 3
    [InlineData("var a = new List<object> { 3 }; var b = a.Cast<object>().ToList(); b.Add(4); return a.Count;")]              // 1
    [InlineData("var a = new List<int> { 3 }; var b = a.DefaultIfEmpty().ToList(); b.Add(4); return a.Count;")]              // 1
    [InlineData("var q = new Queue<int>(new[] { 1, 2 }); var b = q.ToArray(); return b[0] * 10 + b[1];")]                      // 12
    [InlineData("var s = new HashSet<int> { 1, 2, 3 }; return s.Last() + s.DefaultIfEmpty().Count();")]                       // 6
    public void WhatLinqMaterializes_IsACopy(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
