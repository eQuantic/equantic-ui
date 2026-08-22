using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Fase 4, slice 0 — the BOUND tree as an instrument. Every case here is a place where C#'s
/// meaning lives in a conversion or an operator the syntax does not spell out: implicit numeric
/// widening and narrowing, integer wraparound, checked arithmetic, char and enum arithmetic,
/// nullable lifting, decimal and long mixing, compound assignments that narrow. The syntax-driven
/// strategies get some of these right by asking the semantic model; the ones they get wrong are
/// exactly what an IOperation-driven front end is for. The divergent set is a COMMITTED baseline
/// (<c>conversion-gaps.baseline.txt</c>): a case may leave it — regenerate with
/// EQ_UPDATE_CONVERSION_GAPS=1 so the number moves on record — and may never join it.
/// </summary>
public class ConversionConformanceTests
{
    public static readonly string[] Cases =
    [
        // integer division and remainder
        "int a = 7, b = 2; return a / b;",                                         // 3
        "int a = -7, b = 2; return a / b;",                                        // -3 (truncates toward zero)
        "int a = -7, b = 2; return a % b;",                                        // -1
        "int a = 7; double d = a / 2; return d;",                                  // 3 — int division THEN widening
        "int a = 7; double d = a / 2.0; return d;",                                // 3.5
        "int a = 7; return a / 2 * 2.0;",                                          // 6
        // widening in mixed arithmetic
        "int i = 3; long l = 4; return (l + i).ToString();",                       // "7" (long)
        "int i = 3; double d = i; return d / 2;",                                  // 1.5
        "byte b = 200; int r = b + 100; return r;",                                // 300 (byte + int → int)
        "float f = 0.1f; double d = f; return d > 0.1;",                           // true — float widened carries its error
        "float f = 0.1f + 0.2f; return f.ToString();",                             // "0.3" (single precision)
        "float a = 0.1f, b = 0.2f; return (a + b).ToString();",                    // "0.3" — unstored, rounded on print
        "float a = 0.1f, b = 0.2f; return a + b == 0.3f;",                         // true — singles compare as singles
        "float a = 0.1f, b = 0.2f; float c = a + b; return c == 0.3f;",            // true — rounded at the store
        // narrowing casts truncate, and integers wrap (unchecked by default)
        "double d = 3.99; int i = (int)d; return i;",                              // 3
        "double d = -3.99; int i = (int)d; return i;",                             // -3
        "int big = int.MaxValue; int r = big + 1; return r;",                      // -2147483648 (wraps)
        // An ARRAY INDEX out of range throws in .NET and answers undefined here. Substring and a
        // missing dictionary key were fixed to fail where .NET fails; this one is not, and the
        // reason is cost: it would put a bounds check on EVERY index in every loop the framework
        // emits, and an index out of range is a defect the server catches on the same code path.
        // Recorded so the limit is known rather than discovered.
        "try { var bad = (new int[1])[5]; return 1; } catch { return -1; }",          // -1 in .NET; undefined here
        "var xs = new[]{1,2}; try { var v = xs[9]; return 1; } catch { return -1; }", // same, through a variable
        // `m[k]++` on a MISSING key: .NET reads first and throws, and a plain object increments an
        // undefined into NaN and creates the key. A compound `+=` IS lowered (guarded read, plain
        // write); ++ is not, because the guarded read cannot be the assignment TARGET and the
        // postfix form's value is the OLD one, which a template cannot express.
        "var m = new Dictionary<string, int>(); try { m[\"gone\"]++; return 1; } catch { return -1; }",
        "int big = int.MaxValue; long r = big + 1L; return r.ToString();",         // "2147483648" — widened first
        "byte b = 250; b += 10; return b;",                                        // 4 (wraps at 256)
        "short s = 32767; s++; return s;",                                         // -32768

        // An implicit conversion is not a shape of expression — it happens wherever C# says a value
        // flows into a wider or different type. These are the sites a syntax rule never sees.
        "char c = 'a'; int i = c; return i;",                                       // 97 — initializer
        "int Twice(int x) => x * 2; char c = 'a'; return Twice(c);",                 // 194 — argument
        "char c = 'a'; var a = new int[100]; a[c] = 7; return a[97];",              // 7 — index
        "char Pick() => 'z'; int n = Pick(); return n;",                            // 122 — a call's result
        "char c = 'a'; return c > 90;",                                             // true — comparison
        "char c = 'a'; return (c + 1).ToString();",                                 // "98"
        "int n = 7; long l = n; return l.ToString();",                              // "7" — int widens to long
        "int n = 7; return DateTimeOffset.FromUnixTimeSeconds(n).Year;",            // 1970 — int into a long parameter
        "int n = 3; float f = n; return f.ToString();",                             // "3"
        "long l = 5; double d = l; return d;",                                      // 5 — a long narrows to double
        "long Big(long v) => v * 2; int n = 21; return Big(n).ToString();",           // "42" — int into a long parameter
        "char c = 'a'; var list = new List<int>(); list.Add(c); return list[0];",   // 97 — into a collection (an annotated declaration: the harness runs TypeScript now)

        // STATEMENTS where the bound tree knows a conversion the syntax does not show: the ELEMENT
        // of a foreach converts to the declared variable type, one item at a time.
        "var xs = new[] { 1, 2 }; long s = 0; foreach (long l in xs) s += l; return s.ToString();",      // "3" — int elements into a long
        "var xs = new[] { 1, 2 }; long s = 0; foreach (long l in xs) s += l * 3000000000L; return s.ToString();", // "9000000000" — exact past 2^32
        "var xs = new[] { 7, 8 }; double s = 0; foreach (double d in xs) s += d / 2; return s;",          // 7.5 — int elements as doubles
        "var xs = new[] { 'a', 'b' }; int s = 0; foreach (int code in xs) s += code; return s;",          // 195 — char elements as ints
        "var xs = new[] { 1.5f, 2.5f }; float s = 0; foreach (var f in xs) s += f; return s.ToString();", // "4"
        "var s = \"ab\"; int n = 0; foreach (var c in s) n += c; return n;",                            // 195 — a string enumerates chars
        "var d = new Dictionary<string, int> { [\"a\"] = 1 }; long s = 0; foreach (var (k, v) in d) s += v; return s.ToString();", // "1"

        // A value flowing into a string through `+=` — the one concatenation shape no syntax rule saw.
        "string s = \"a\"; s += true; return s;",                                     // "aTrue"
        "string s = \"a\"; string? n = null; s += n; return s;",                      // "a"
        "string s = \"a\"; int? q = null; s += q; return s;",                         // "a"
        "string s = \"a\"; s += 'b'; return s;",                                      // "ab"
        "string s = \"a\"; s += 1.5; return s;",                                      // "a1.5"
        // The author's word decides for int and long: `unchecked` wraps, `checked` throws, and the
        // default keeps the double's count (a documented limit — see IntegerWidth).
        "return unchecked(int.MaxValue + 1);",                                      // -2147483648
        "int big = int.MaxValue; unchecked { big++; } return big;",                 // -2147483648
        "int a = 65536; return unchecked(a * a);",                                  // 0 — Math.imul
        "long l = long.MaxValue; unchecked { l++; } return l.ToString();",          // "-9223372036854775808"
        "int big = int.MaxValue; try { return checked(big + 1); } catch { return -1; }", // -1 — overflow thrown
        "try { checked { byte b = 255; b += 1; return b; } } catch { return -1; }", // -1
        "uint h = 2166136261; h *= 16777619; return h.ToString();",                // FNV step wraps at 2^32
        "uint u = 0; u--; return u.ToString();",                                   // "4294967295"
        "int x = 1; return x << 33;",                                              // 2 — shift count masked to 5 bits
        "int x = -8; return x >> 1;",                                              // -4 (arithmetic shift)
        "int x = 1; x *= 3000000000L > 0 ? 2 : 1; return x;",                      // 2
        "int x = 10; x /= 4; return x;",                                           // 2 — compound keeps integer division
        "int x = 10; x /= 4.0 > 1 ? 4 : 1; return x;",                             // 2
        // checked arithmetic throws
        "try { checked { int m = int.MaxValue; m++; return m; } } catch (OverflowException) { return -1; }", // -1
        "int m = int.MaxValue; try { return checked(m + 1); } catch (OverflowException) { return 0; }",       // 0
        // char arithmetic and comparisons
        "char c = 'a'; c++; return c.ToString();",                                 // "b"
        "char c = 'a'; return c + 1;",                                             // 98
        "char c = 'a'; return (char)(c + 1);",                                     // 'b'
        "char c = 'b'; return c > 'a';",                                           // true
        "char c = 'x'; return c - 'a';",                                           // 23
        "string s = \"abc\"; return s[1] == 'b';",                                 // true
        "string s = \"abc\"; int sum = 0; foreach (var ch in s) sum += ch; return sum;", // 294
        // enums and their underlying values
        "var v = DayOfWeek.Wednesday; return (int)v;",                             // 3
        "var v = DayOfWeek.Wednesday; return v + 1 == DayOfWeek.Thursday;",         // true
        "int n = 3; return ((DayOfWeek)n).ToString();",                            // "Wednesday"
        "var v = DayOfWeek.Monday; return v.CompareTo(DayOfWeek.Friday) < 0;",     // true
        // nullable lifting
        "int? a = null; int? b = 3; return a + b;",                                // null
        "int? a = 4; int? b = 3; return a * b;",                                   // 12
        "int? a = null; return a > 0;",                                            // false
        "int? a = null; return a == null;",                                        // true
        "int? a = 5; int b = a ?? 0; return b * 2;",                               // 10
        "int? a = null; return (a ?? 7) + 1;",                                     // 8
        "bool? f = null; return f == true;",                                       // false
        "double? d = 2.5; int i = (int)d; return i;",                              // 2
        // decimal and long mixed with int literals
        "decimal m = 10m; int i = 3; return (m / i).ToString();",                  // "3.3333333333333333333333333333"
        "decimal m = 0.1m; return (m + 0.2m == 0.3m);",                            // true
        "decimal m = 1.5m; return Math.Round(m).ToString();",                      // "2"
        "decimal m = 2.5m; return Math.Round(m).ToString();",                      // "2" — banker's
        "long l = 5000000000L; int i = 2; return (l * i).ToString();",             // "10000000000"
        "long l = 7; return (l / 2).ToString();",                                  // "3"
        "long l = long.MaxValue; return (l + 1).ToString();",                      // "-9223372036854775808"
        // string concatenation with the conversions it implies
        "string s = null; return \"a\" + s + \"b\";",                              // "ab"
        "char c = 'x'; return \"a\" + c;",                                         // "ax"
        "bool f = true; return \"v=\" + f;",                                       // "v=True"
        "double d = 1.0; return \"v=\" + d;",                                      // "v=1"
        "int? n = null; return \"v=\" + n;",                                       // "v="
        "object o = 5; return \"v=\" + o;",                                        // "v=5"
        "return 1 + 2 + \"3\";",                                                   // "33"
        "return \"1\" + 2 + 3;",                                                   // "123"
        // comparisons across widths and boxing
        "int i = 5; long l = 5; return i == l;",                                   // true
        "object a = 5; object b = 5; return a.Equals(b);",                         // true
        "double d = 0.1 + 0.2; return d == 0.3;",                                  // false
        "float f = 0.1f; return f == 0.1;",                                        // false — float vs double
        "int i = 3; double d = 3.0; return i == d;",                               // true
        // switch on a converted value
        "long l = 2; switch (l) { case 1: return \"one\"; case 2: return \"two\"; default: return \"many\"; }", // "two"
        "char c = 'b'; switch (c) { case 'a': return 1; case 'b': return 2; default: return 0; }",              // 2
    ];

    public static IEnumerable<object[]> AllCases() => Cases.Select(c => new object[] { c });

    /// <summary>Every case that still diverges, against the committed baseline.</summary>
    [SkippableFact]
    public void ConversionSemantics_DivergencesOnlyEverLeaveTheBaseline()
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        var divergent = new List<string>();
        foreach (var statements in Cases)
        {
            try { ConformanceRunner.AssertStatementsSameAsDotNet(statements); }
            catch (Exception e) { divergent.Add($"{statements}  ⟶  {FirstLine(e.Message)}"); }
        }

        var report = string.Join("\n", divergent.Select(d => d.Split("  ⟶  ")[0])) + (divergent.Count > 0 ? "\n" : "");
        var baselinePath = Path.Combine(RepoRoot(), "tests", "eQuantic.UI.Conformance.Tests", "conversion-gaps.baseline.txt");
        if (Environment.GetEnvironmentVariable("EQ_UPDATE_CONVERSION_GAPS") == "1")
        {
            File.WriteAllText(baselinePath, report);
            return;
        }

        Assert.True(File.Exists(baselinePath), "No committed baseline — run once with EQ_UPDATE_CONVERSION_GAPS=1 and commit the file.");
        var committed = File.ReadAllText(baselinePath).Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        var current = divergent.Select(d => d.Split("  ⟶  ")[0]).ToHashSet(StringComparer.Ordinal);

        var joined = current.Where(c => !committed.Contains(c)).ToList();
        var left = committed.Where(c => !current.Contains(c)).ToList();
        Assert.True(joined.Count == 0, "A conversion that used to match .NET diverges now:\n  "
            + string.Join("\n  ", divergent.Where(d => joined.Contains(d.Split("  ⟶  ")[0]))));
        Assert.True(left.Count == 0, $"{left.Count} conversion(s) now match .NET — regenerate the baseline "
            + "(EQ_UPDATE_CONVERSION_GAPS=1) so the number moves on record:\n  " + string.Join("\n  ", left));
    }

    private static string FirstLine(string message)
    {
        var line = message.Split('\n').FirstOrDefault(l => l.Contains("differ") || l.Contains("error") || l.Contains("Expected")) ?? message;
        return line.Trim().Length > 160 ? line.Trim()[..160] + "…" : line.Trim();
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "eQuantic.UI.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("repository root not found");
    }
}
