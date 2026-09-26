using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A value that starts as its type's default, on both sides (#380): the elements of a sized array,
/// a <c>default</c> literal and a <c>default(T)</c>. Both were decided by the type's SPELLING, so an
/// element or a value whose twin is not a plain number started wrong: a long as 0 where every long
/// is a BigInt, a decimal as 0 with none of its methods, a char and a struct as null, an enum as
/// null or undefined, and a type written as anything but its keyword fell through to null. The
/// default is now the one the semantic model gives the type, the same a field of it starts with.
/// </summary>
public class DefaultValueConformanceTests
{
    private const string Prelude = """
        public struct Point { public int X; public int Y; }
        public record struct Pair(int A, int B);
        public enum Level { Low = 1, High = 2 }
        public struct Gauge { public int Level { get; set; } = 5; public int Count; public Gauge() { } }
        public struct Tally { public int N; public Tally() { N = 3; } }
        """;

    [SkippableTheory]
    // ---- a sized array starts as its element type's default ----
    [InlineData("var a = new decimal[2]; return (a[0] + 1m).ToString();")]                  // "1"
    [InlineData("var a = new long[2]; return (a[0] + 1L).ToString();")]                     // "1"
    [InlineData("var a = new ulong[1]; return (a[0] + 1UL).ToString();")]                   // "1"
    [InlineData("var a = new System.Int64[2]; return (a[1] + 1L).ToString();")]             // "1"
    [InlineData("var a = new char[1]; return (int)a[0];")]                                  // 0
    [InlineData("var a = new DayOfWeek[1]; return a[0].ToString();")]                       // "Sunday"
    [InlineData("var a = new DateTime[1]; return a[0].Year;")]                              // 1
    [InlineData("var a = new Int32[2]; return a[0] + 1;")]                                  // 1
    [InlineData("var a = new int[2]; return a[0] + a[1];")]                                 // 0
    [InlineData("var a = new float[1]; return (double)(a[0] + 0.5f);")]                     // 0.5
    [InlineData("var a = new bool[1]; return a[0] ? \"t\" : \"f\";")]                       // "f"
    [InlineData("var a = new string[1]; return a[0] == null;")]                             // true
    [InlineData("var a = new int?[1]; return a[0].HasValue;")]                              // false
    [InlineData("var a = new double?[2]; return a[1] == null;")]                            // true
    [InlineData("var a = new int[2][]; return a[0] == null;")]                              // true
    [InlineData("int n = 0; var a = new int[++n + 1]; return n + \"|\" + a.Length;")]       // "1|2"
    // The time and identity structs the runtime twins start as their MinValue, Zero or Empty.
    [InlineData("var a = new TimeSpan[1]; return a[0].TotalSeconds;")]                       // 0
    [InlineData("var a = new DateOnly[1]; return a[0].Year;")]                               // 1
    [InlineData("var a = new TimeOnly[1]; return a[0].Hour;")]                               // 0
    [InlineData("var a = new DateTimeOffset[1]; return a[0].Year;")]                         // 1
    [InlineData("var a = new Guid[1]; return a[0] == Guid.Empty;")]                          // true
    public void ASizedArray_StartsAsItsElementTypesDefault(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    [SkippableTheory]
    // A struct element is a zero instance of its own: `fill` shares ONE object across every slot,
    // and a write through one element showed through all of them.
    [InlineData("var a = new Point[2]; return a[0].X + a[1].Y;")]                           // 0
    [InlineData("var a = new Point[2]; a[0].X = 5; return a[1].X;")]                        // 0
    [InlineData("var a = new Point[3]; a[1].Y = 7; return a[0].Y + \"|\" + a[1].Y + \"|\" + a[2].Y;")] // "0|7|0"
    [InlineData("var a = new Pair[2]; return a[0].A + a[1].B;")]                            // 0
    // An enum with no member at zero still defaults to zero, which is none of its members. (Reading
    // that zero back as a number is the enum's own gap: a value no member names has no twin, #404.)
    [InlineData("var a = new Level[1]; return a[0] == default(Level);")]                    // true
    [InlineData("var a = new Level[1]; return a[0] == Level.Low;")]                         // false
    [InlineData("Point p = default; return p.X + p.Y;")]                                    // 0
    // A struct whose construction gives a member more than its zero: `new` runs the initializer
    // or the constructor's body, and `default`, an array slot and OrDefault run neither.
    [InlineData("return new Gauge().Level;")]                                               // 5
    [InlineData("return default(Gauge).Level;")]                                            // 0
    [InlineData("Gauge g = default; return g.Level + g.Count;")]                            // 0
    [InlineData("var a = new Gauge[2]; return a[0].Level + a[1].Level;")]                   // 0
    [InlineData("var a = new Gauge[2]; a[0].Level = 4; return a[1].Level;")]                // 0
    [InlineData("return new List<Gauge>().FirstOrDefault().Level;")]                        // 0
    [InlineData("return default(Tally).N;")]                                                // 0
    public void AStructOrAnEnum_StartsAsItsZero(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }

    [SkippableTheory]
    // ---- a default literal and a default(T) are the type's default ----
    [InlineData("int x = default; return x + 1;")]                                          // 1
    [InlineData("long x = default; return (x + 1L).ToString();")]                           // "1"
    [InlineData("decimal m = default; return (m + 1.5m).ToString();")]                      // "1.5"
    [InlineData("char c = default; return (int)c;")]                                        // 0
    [InlineData("bool b = default; return b ? 1 : 0;")]                                     // 0
    [InlineData("string s = default; return s == null;")]                                   // true
    [InlineData("int? x = default; return x.HasValue;")]                                    // false
    [InlineData("DateTime d = default; return d.Year;")]                                    // 1
    [InlineData("DayOfWeek d = default; return d.ToString();")]                             // "Sunday"
    [InlineData("return (default(long) + 1L).ToString();")]                                 // "1"
    [InlineData("return (default(decimal) + 1.5m).ToString();")]                            // "1.5"
    [InlineData("return (int)default(char);")]                                              // 0
    [InlineData("return default(int?) == null;")]                                           // true
    [InlineData("return default(DayOfWeek).ToString();")]                                   // "Sunday"
    [InlineData("return default(System.Int64).ToString();")]                                // "0"
    [InlineData("int F(int x = default) => x; return F();")]                                // 0
    [InlineData("long F(long x = default) => x + 1L; return F().ToString();")]              // "1"
    [InlineData("int Pick(bool first) => first ? 1 : default; return Pick(false);")]        // 0
    [InlineData("TimeSpan t = default; return t.TotalSeconds;")]                             // 0
    // A tuple is an array on this side, and its zero is an array of its elements' zeros.
    [InlineData("var t = default((int, string)); return t.Item1 + \"|\" + (t.Item2 == null);")] // "0|True"
    [InlineData("var a = new (int, long)[1]; return (a[0].Item2 + 1L).ToString();")]        // "1"
    // Each slot holds its own tuple: a write through one does not show through another.
    [InlineData("var a = new (int, long)[2]; a[0].Item1 = 5; return a[1].Item1;")]          // 0
    [InlineData("Guid g = default; return g == Guid.Empty;")]                                // true
    // Every site that asks a type for its default answers the same: LINQ's OrDefault too.
    [InlineData("return new List<DateTime>().FirstOrDefault().Year;")]                       // 1
    [InlineData("return (new List<long>().LastOrDefault() + 1L).ToString();")]              // "1"
    // DateTimeOffset.MinValue is default(DateTimeOffset), and its twin had no MinValue to call.
    [InlineData("return DateTimeOffset.MinValue.Year + \"|\" + DateTimeOffset.MaxValue.Year;")] // "1|9999"
    public void ADefault_IsTheTypesDefault(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
