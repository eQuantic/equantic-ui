using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The LINQ overloads the null-argument measurement found lowered as a sibling (#715): a default value
/// read as a predicate (#720, and <c>SingleOrDefault</c>'s, #760), three sequences zipped as two and a
/// selector (#722), a mean of longs that added a BigInt to a number (#719), and a comparer's
/// <c>SequenceEqual</c> that answered its first sequence (#721). Each overload is executed on both sides,
/// beside the sibling it was taken for.
/// </summary>
public class LinqOverloadConformanceTests
{
    /// <summary>
    /// <c>FirstOrDefault(defaultValue)</c>, <c>LastOrDefault(defaultValue)</c> and
    /// <c>SingleOrDefault(defaultValue)</c>, .NET 6's overloads, with and without a predicate: the element
    /// when there is one, a null element included, and the value handed otherwise (#720), a second element
    /// or a second match refused where the one element is asked for (#760).
    /// </summary>
    [SkippableTheory]
    [InlineData("return new[]{1,2}.FirstOrDefault(9);")]                                       // 1
    [InlineData("return new int[0].FirstOrDefault(9);")]                                       // 9
    [InlineData("return new[]{1,2}.LastOrDefault(9);")]                                        // 2
    [InlineData("return new int[0].LastOrDefault(9);")]                                        // 9
    [InlineData("return new[]{1,2,3}.FirstOrDefault(x => x > 1, 9);")]                         // 2
    [InlineData("return new[]{1,2,3}.FirstOrDefault(x => x > 5, 9);")]                         // 9
    [InlineData("return new[]{1,2,3}.LastOrDefault(x => x < 3, 9);")]                          // 2
    [InlineData("return new[]{1,2,3}.LastOrDefault(x => x > 5, 9);")]                          // 9
    [InlineData("return new string[]{null}.FirstOrDefault(\"x\") ?? \"null\";")]               // null
    [InlineData("return new string[]{\"a\", null}.LastOrDefault(\"x\") ?? \"null\";")]         // null
    [InlineData("return new string[]{null, \"a\"}.FirstOrDefault(s => s == null, \"x\") ?? \"null\";")] // null
    [InlineData("return new string[]{\"a\"}.FirstOrDefault(s => s == null, \"x\");")]          // x
    [InlineData("return new List<int>{4}.FirstOrDefault(9) + new List<int>().LastOrDefault(9);")] // 13
    [InlineData("var seen = 0; var r = new[]{1,2,3}.FirstOrDefault(x => { seen++; return x == 2; }, 9); return seen + \",\" + r;")] // 2,2
    [InlineData("var seen = 0; var r = new[]{1,2,3}.LastOrDefault(x => { seen++; return x == 2; }, 9); return seen + \",\" + r;")]  // 2,2: an array is searched from its end
    [InlineData("var order = \"\"; int[] Src() { order += \"s\"; return new[]{5}; } int Def() { order += \"d\"; return 9; } var r = Src().FirstOrDefault(Def()); return order + r;")] // sd5
    [InlineData("return new int[0].SingleOrDefault(9);")]                                      // 9
    [InlineData("return new[]{4}.SingleOrDefault(9);")]                                        // 4
    [InlineData("return new[]{1,2,3}.SingleOrDefault(x => x > 5, 9);")]                        // 9
    [InlineData("return new[]{1,2,3}.SingleOrDefault(x => x == 2, 9);")]                       // 2
    [InlineData("return new string[]{null}.SingleOrDefault(\"x\") ?? \"null\";")]              // null
    [InlineData("try { return new[]{1,2}.SingleOrDefault(9).ToString(); } catch (InvalidOperationException e) { return e.Message; }")]
    [InlineData("try { return new[]{1,2,3}.SingleOrDefault(x => x > 1, 9).ToString(); } catch (InvalidOperationException e) { return e.Message; }")]
    [InlineData("var seen = 0; var r = new[]{1,2,3}.SingleOrDefault(x => { seen++; return x == 2; }, 9); return seen + \",\" + r;")] // 3,2
    [InlineData("var seen = 0; try { new[]{1,2,3,4}.SingleOrDefault(x => { seen++; return x > 1; }, 9); } catch (InvalidOperationException) { } return seen;")] // 3
    public void ADefaultValue_IsAValue(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>
    /// <c>Zip(second)</c> answers pairs and <c>Zip(second, third)</c> triples, as many as the shortest
    /// sequence holds (#722), beside the overload with a result selector.
    /// </summary>
    [SkippableTheory]
    [InlineData("return string.Join(\",\", new[]{1,2,3}.Zip(new[]{\"a\",\"b\"}).Select(p => p.First + p.Second));")]          // 1a,2b
    [InlineData("return new[]{1,2}.Zip(new[]{10,20,30}).Count();")]                                                           // 2
    [InlineData("return string.Join(\",\", new[]{1,2,3}.Zip(new[]{\"a\",\"b\",\"c\"}, new[]{'x','y'}).Select(t => t.First + t.Second + t.Third));")] // 1ax,2by
    [InlineData("return new[]{1,2,3}.Zip(new[]{4,5,6}, new[]{7,8,9}).Sum(t => t.First * t.Second * t.Third);")]               // 28+80+162
    [InlineData("var (a, b, c) = new[]{1}.Zip(new[]{2}, new[]{3}).First(); return a * 100 + b * 10 + c;")]                     // 123
    [InlineData("return new[]{1,2,3}.Zip(new[]{10,20,30}, (a, b) => a + b).Sum();")]                                           // 66
    public void Zip_AnswersTuplesAsDotNetDoes(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>
    /// An argument fills the parameter it binds to and runs where it is written: a selector named and
    /// written before the sequence it zips is still the selector, and a string handed under a
    /// null-conditional is still read by its UTF-16 code units, as it is without one.
    /// </summary>
    [SkippableTheory]
    [InlineData("return new[]{0}.Zip(resultSelector: (x, y) => x + y, second: new[]{1}).Sum();")]                           // 1
    [InlineData("var order = \"\"; int[] B() { order += \"b\"; return new[]{1}; } Func<int, int, int> S() { order += \"s\"; return (x, y) => x + y; } var r = new[]{0}.Zip(resultSelector: S(), second: B()).Sum(); return order + r;")] // sb1
    [InlineData("var order = \"\"; int[] B() { order += \"b\"; return new[]{1}; } int[] C() { order += \"c\"; return new[]{2}; } var r = new[]{0}.Zip(third: C(), second: B()).Count(); return order + r;")] // cb1
    [InlineData("int[] a = {1, 2}; return a?.Zip(\"\U0001F600\").Count();")]                    // 2
    [InlineData("char[] a = {'\\uD83D', '\\uDE00'}; return a?.SequenceEqual(\"\U0001F600\");")] // true
    [InlineData("int[] a = null; return a?.FirstOrDefault(9) is null;")]                       // true
    public void AnArgument_FillsTheParameterItBindsTo(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>
    /// The mean as .NET computes it for each element type (#719): a long's summed exactly and divided as
    /// a double whatever the selector is, a nullable's over its values alone and null when it has none,
    /// and an empty sequence refused where the answer is not nullable.
    /// </summary>
    [SkippableTheory]
    [InlineData("return new[]{1L,2L,4L}.Average();")]                                          // 2.3333333333333335
    [InlineData("return new[]{1,2,4}.Average(x => (long)x);")]
    [InlineData("Func<int, long> wide = x => x * 3L; return new[]{1,2}.Average(wide);")]       // 4.5
    [InlineData("return new[]{1,2}.Average(Widen); static long Widen(int x) => x;")]           // 1.5
    [InlineData("return new[]{9007199254740993L, 9007199254740995L}.Average();")]
    [InlineData("return new long?[]{1, null, 4}.Average();")]                                  // 2.5
    [InlineData("return new long?[]{null}.Average() is null;")]                                // true
    [InlineData("return new[]{1,2}.Average(x => x == 1 ? (long?)null : x);")]                  // 2
    [InlineData("return new int?[]{1, null, 4}.Average();")]                                   // 2.5
    [InlineData("return new int?[0].Average() is null;")]                                      // true
    [InlineData("return new double?[]{1.5, null}.Average();")]                                 // 1.5
    [InlineData("return new[]{int.MaxValue, int.MaxValue}.Average();")]                        // 2147483647
    [InlineData("try { return new int[0].Average(); } catch (InvalidOperationException e) { return e.Message; }")]
    [InlineData("try { return new long[0].Average(); } catch (InvalidOperationException e) { return e.Message; }")]
    [InlineData("try { return new int[0].Average(x => (long)x); } catch (InvalidOperationException e) { return e.Message; }")]
    [InlineData("try { return new double[0].Average(); } catch (InvalidOperationException e) { return e.Message; }")]
    [InlineData("try { return new[]{long.MaxValue, 1L}.Average(); } catch (OverflowException e) { return e.Message; }")]
    public void Average_IsTheMeanDotNetComputes(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>
    /// <c>SequenceEqual</c> by the element type's default equality, as .NET's (#721): the comparer that
    /// asks for it, a long that JSON cannot write, a decimal by its value and not its scale, and a class
    /// by reference.
    /// </summary>
    [SkippableTheory]
    [InlineData("return new[]{1,2}.SequenceEqual(new[]{1,2}, EqualityComparer<int>.Default);")]   // true
    [InlineData("return new[]{1,2}.SequenceEqual(new[]{1,3}, null);")]                          // false
    [InlineData("return new[]{\"a\"}.SequenceEqual(new[]{\"a\"}, StringComparer.Ordinal);")]   // true
    [InlineData("return new[]{1L,2L}.SequenceEqual(new[]{1L,2L});")]                            // true
    [InlineData("return new[]{1L,2L}.SequenceEqual(new[]{1L,3L});")]                            // false
    [InlineData("return new[]{1.0m}.SequenceEqual(new[]{1.00m});")]                             // true
    [InlineData("return new double?[]{null}.SequenceEqual(new double?[]{double.NaN});")]        // false
    [InlineData("return new[]{double.NaN}.SequenceEqual(new[]{double.NaN});")]                  // true
    [InlineData("return new List<int>{1,2}.SequenceEqual(new[]{1,2});")]                        // true
    [InlineData("return new[]{1,2}.SequenceEqual(new[]{1,2,3});")]                              // false
    public void SequenceEqual_ComparesAsDotNetDoes(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>
    /// An array compares by reference, so two arrays of the same items are two sequences' different
    /// elements, and the same array is equal to itself; a record compares by value (#721).
    /// </summary>
    [SkippableTheory]
    [InlineData("var a = new[]{1}; return new[]{a}.SequenceEqual(new[]{new[]{1}});")]          // false
    [InlineData("var a = new[]{1}; return new[]{a}.SequenceEqual(new[]{a});")]                 // true
    [InlineData("return new[]{new Pt(1, 2)}.SequenceEqual(new[]{new Pt(1, 2)});")]             // true
    [InlineData("return new[]{new Pt(1, 2)}.SequenceEqual(new[]{new Pt(1, 3)});")]             // false
    public void SequenceEqual_FindsAnArrayByReferenceAndARecordByValue(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, "public record Pt(int X, int Y);");
    }
}
