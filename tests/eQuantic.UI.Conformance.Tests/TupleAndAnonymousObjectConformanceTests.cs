using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A value the browser holds without a class of its own answers its object members as .NET does: an
/// anonymous object, which is a plain object with camelCased keys there, and a value tuple, which is an
/// array. Neither has a twin to carry an <c>equals</c> or a <c>toString</c>, so each member is lowered
/// at the call from the static type, which knows an anonymous type's C# member names and their order and
/// a tuple's element types.
/// <para>
/// An anonymous object's <c>Equals</c> threw (<c>equals is not a function</c>, #743), and its text was
/// <c>[object Object]</c>. A tuple's text was JavaScript's <c>Array.prototype.toString</c>, <c>1,2</c>,
/// where .NET writes <c>(1, 2)</c>, each element as its own <c>ToString</c> writes it in the culture in
/// force and a null one as nothing (#762), in every route a value takes into text.
/// </para>
/// </summary>
public class TupleAndAnonymousObjectConformanceTests
{
    /// <summary>The cultures a text case runs in: the invariant one, a comma-decimal one with its own
    /// date pattern, and one whose minus sign is not a hyphen.</summary>
    public static readonly string?[] Cultures = [null, "pt-BR", "sv-SE"];

    /// <summary>The types the cases name, declared on both sides.</summary>
    public const string Prelude = """
        public enum Scale { Small, Large }
        public record Point(int X, int Y);
        public record Segment((int, int) Ends, string Name);
        """;

    /// <summary>An anonymous object's <c>Equals</c>, <c>GetHashCode</c> and <c>==</c> (#743).</summary>
    public static readonly string[] AnonymousEquality =
    [
        // The reported row: equal members, and a different value.
        """var a = new { R = (byte)255 }; return a.Equals((object)new { R = (byte)255 }) + "|" + a.Equals((object)new { R = (byte)254 });""", // True|False
        """var a = new { R = 1, Name = "x" }; var b = new { R = 1, Name = "x" }; return a.Equals(b) + "|" + a.Equals(new { R = 1, Name = "y" });""", // True|False
        // Another member set is another type to .NET: other names, one more member, the same names in
        // another order.
        """var a = new { R = 1 }; return a.Equals(new { G = 1 }) + "|" + a.Equals(new { R = 1, G = 2 }) + "|" + new { R = 1, G = 2 }.Equals(new { G = 2, R = 1 });""", // False|False|False
        """var a = new { R = 1 }; object? none = null; return a.Equals(null) + "|" + a.Equals(none);""", // False|False
        // Each member by its own type's equality: an array by reference, a decimal by value, NaN equal
        // to NaN, a nested anonymous object and a tuple by their members.
        """var xs = new[] { 1 }; return new { A = xs }.Equals(new { A = xs }) + "|" + new { A = new[] { 1 } }.Equals(new { A = new[] { 1 } }) + "|" + new { M = 1.50m }.Equals(new { M = 1.5m }) + "|" + new { D = double.NaN }.Equals(new { D = double.NaN }) + "|" + new { P = new { X = 1 } }.Equals(new { P = new { X = 1 } }) + "|" + new { T = (1, "a") }.Equals(new { T = (1, "a") });""", // True|False|True|True|True|True
        // A record with the same members is another type.
        """return new { X = 1, Y = 2 }.Equals(new Point(1, 2)) + "|" + new Point(1, 2).Equals(new { X = 1, Y = 2 });""", // False|False
        // `==` stays C#'s reference equality: an anonymous type declares no operator.
        """var a = new { R = 1 }; var b = new { R = 1 }; var c = a; return (a == b) + "|" + (a == c) + "|" + (a != b) + "|" + object.ReferenceEquals(a, b);""", // False|True|True|False
        // The method group compares as the call does, and so does the call behind object.
        """var a = new { R = 1 }; Func<object?, bool> same = a.Equals; object o = a; return same(new { R = 1 }) + "|" + same(new { R = 2 }) + "|" + same(null) + "|" + o.Equals(new { R = 1 });""", // True|False|False|True
        // A member compared by reference (a list) takes the comparison generated from the member types,
        // in the call and in the group alike, and it too refuses an object of other members.
        """var a = new { L = new List<int> { 1 } }; Func<object?, bool> eq = a.Equals; return eq(new { L = a.L }) + "|" + eq(new { L = new List<int> { 1 } }) + "|" + eq(new { L = a.L, M = 1 }) + "|" + a.Equals(new { L = a.L, M = 1 });""", // True|False|False|False
        // A copy made by `with`, and the anonymous object with no members.
        """var a = new { A = 1, B = "x" }; var b = a with { A = 2 }; return b.ToString() + "|" + a.Equals(b) + "|" + (a with { }).Equals(a) + "|" + new { }.Equals(new { }) + "|" + (new { }.GetHashCode() == new { }.GetHashCode());""", // { A = 2, B = x }|False|True|True|True
        // Equal objects hash equal, by the call and by the method group.
        """var a = new { R = 1, Name = "x", S = (string?)null }; var b = new { R = 1, Name = "x", S = (string?)null }; Func<int> h = a.GetHashCode; return (a.GetHashCode() == b.GetHashCode()) + "|" + (h() == b.GetHashCode());""", // True|True
        // Behind object, and in a set of objects.
        """object o = new { R = 1 }; var set = new HashSet<object> { new { R = 1 } }; return o.Equals(new { R = 1 }) + "|" + set.Contains(new { R = 1 }) + "|" + (o.GetHashCode() == new { R = 1 }.GetHashCode());""", // True|True|True
        // Members of other TYPES make another anonymous type, which the browser holds as the same plain
        // object: a byte and an int, an int and a double, a string and a char, read where the call is.
        """var a = new { A = (byte)1 }; return a.Equals(new { A = 1 }) + "|" + new { A = 1 }.Equals(new { A = 1.0 }) + "|" + new { S = "a" }.Equals((object)new { S = 'a' }) + "|" + a.Equals(new { A = (byte)1 });""", // False|False|False|True
        // The other value is still evaluated, and a value of an unknown type is still compared.
        """var log = ""; var a = new { A = (byte)1 }; object Other() { log += "o"; return new { A = (byte)1 }; } (int, int) Pair() { log += "p"; return (1, 2); } return a.Equals(Pair()) + "|" + a.Equals(Other()) + "|" + a.Equals((object)null) + "|" + log;""", // False|True|False|po
        // An instance call through null throws, after its argument is evaluated, as .NET's callvirt does.
        """var log = ""; object F() { log += "f"; return null; } var a = new { R = 1 }; a = null!; try { return a.Equals(F()).ToString(); } catch (NullReferenceException) { return "NRE" + log; }""", // NREf
        """var a = new { R = 1 }; a = null!; try { return a.Equals(null).ToString(); } catch (NullReferenceException) { return "NRE"; }""", // NRE
        // A method group of a null anonymous object is refused where the delegate is made.
        """var a = new { R = 1 }; a = null!; var made = ""; try { Func<object?, bool> f = a.Equals; made += "e"; } catch (NullReferenceException) { made += "E"; } catch (ArgumentException) { made += "A"; } try { Func<string> g = a.ToString; made += "t"; } catch (NullReferenceException) { made += "T"; } catch (ArgumentException) { made += "A"; } return made;""",
    ];

    /// <summary>An anonymous object's text, <c>{ R = 255, Name = a }</c>: the C# member names in their
    /// declared order, each member as its own <c>ToString</c> writes it in the culture in force.</summary>
    public static readonly string[] AnonymousText =
    [
        """return new { R = 255, Name = "a" }.ToString() + "|" + new { }.ToString();""", // { R = 255, Name = a }|{ }
        // A null member writes nothing, through ToString, a hole, a concatenation and Concat alike.
        """string? s = null; var a = new { S = s, N = -1 }; return a.ToString() + "|" + $"{a}|" + a + "|" + string.Concat(a, "!");""",
        // A member named by inference keeps the name C# gives it, a lower-case one included.
        """var x = 1; var p = new { Name = "n" }; return new { x, p.Name }.ToString();""", // { x = 1, Name = n }
        // Every kind of member, in the culture.
        """return new { B = true, F = 0.1f, D = -1.5, M = 2.50m, C = 'c', L = -5L, E = Scale.Large, T = new DateTime(2024, 1, 2, 3, 4, 5), N = (int?)null }.ToString();""",
        // A member that is itself a value the browser holds as data: an anonymous object, a tuple, a record.
        """return new { A = new { B = 1 }, T = (1, "a"), P = new Point(1, 2) }.ToString();""", // { A = { B = 1 }, T = (1, a), P = Point { X = 1, Y = 2 } }
        // Every other route into text: the method group, Join, Format, an aligned hole, a builder, Convert.
        """var a = new { A = 1.5 }; Func<string> f = a.ToString; return f() + "|" + string.Join(";", new[] { a, new { A = 2.5 } }) + "|" + string.Format("[{0}]", a) + "|" + $"[{a,12}]" + "|" + new StringBuilder().Append(a).ToString() + "|" + Convert.ToString(a);""",
        // An explicit ToString through null throws, where a hole and a concatenation write nothing.
        """var a = new { R = 1 }; a = null!; string s; try { s = a.ToString(); } catch (NullReferenceException) { s = "NRE"; } return s + "|" + $"[{a}]" + "|" + ("[" + a + "]");""", // NRE|[]|[]
        // A member whose type never says what its text is writes its type's name, as Object.ToString does:
        // an array, a list, a dictionary, a set, a delegate; the browser wrote JavaScript's own text of it.
        """var xs = new[] { 1, 2 }; var l = new List<int> { 1 }; var d = new Dictionary<string, int> { ["a"] = 1 }; var h = new HashSet<int> { 1 }; Func<int> f = () => 1; return new { Xs = xs, L = l, D = d, H = h, F = f }.ToString();""",
    ];

    /// <summary>A value tuple's text, <c>(1, 2)</c>: each element as its own <c>ToString</c> writes it in
    /// the culture in force, a null one as nothing, between parentheses and separated by <c>, </c> (#762).</summary>
    public static readonly string[] TupleText =
    [
        """var t = (1, 2); return t.ToString() + "|" + $"{t}" + "|" + t + "|" + string.Concat(t, "!");""", // (1, 2)|(1, 2)|(1, 2)|(1, 2)!
        """string? none = null; return ("a", none, 1).ToString() + "|" + (1, (2, (3, "x"))).ToString();""", // (a, , 1)|(1, (2, (3, x)))
        // A double, a decimal, a date, a negative int, a float and a long, in the culture.
        """return (1.5, 2.50m, new DateTime(2024, 1, 2, 3, 4, 5), -3, 0.1f, -5L).ToString();""",
        // Eight elements and more, which .NET nests in Rest and writes flat.
        """return (1, 2, 3, 4, 5, 6, 7, 8).ToString() + "|" + (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16).ToString();""",
        // A bool, a char, an enum, a null nullable, a negative zero, and a nullable tuple with a value and without.
        """int? n = null; (int, int)? some = (1, 2); (int, int)? empty = null; return (true, 'c', Scale.Large, n, -0.0).ToString() + "|" + some.ToString() + "|" + empty.ToString() + "|" + $"[{empty}]";""",
        // A named tuple, and the pairs and the triples Zip answers.
        """(int X, int Y) p = (3, 4); var z = new[] { 1, 2 }.Zip(new[] { "a", "b" }).ToList(); var three = new[] { 1 }.Zip(new[] { "a" }, new[] { true }).First(); return p.ToString() + "|" + string.Join(";", z) + "|" + z[0] + "|" + $"{z[1]}" + "|" + three;""",
        // Every other route into text: the method group, Format, an aligned hole, a hole with a format
        // (a tuple is not IFormattable, so .NET ignores it), a builder, Convert.
        """var t = (1.5, "a"); Func<string> f = t.ToString; return f() + "|" + string.Format("[{0}]", t) + "|" + $"[{t,10}]" + "|" + $"[{t:N2}]" + "|" + new StringBuilder().Append(t).ToString() + "|" + Convert.ToString(t);""",
        // A tuple a record and an anonymous object hold.
        """return new Segment((1, 2), "s").ToString() + "|" + new { T = (1.5, 2) };""", // Segment { Ends = (1, 2), Name = s }|{ T = (1.5, 2) }
        // The holes a builder's own handler takes, aligned and formatted, and Format's aligned arguments,
        // a tuple behind object among them.
        """var t = (1, 2); var s = new StringBuilder(); s.Append($"{t,8}|{t,-8}|{t:G}"); return s.ToString() + "|" + string.Format("{0,10}|{1}|{2,-12}|", t, new { A = 1 }, (object)(2.5, 'x'));""",
        // The method groups are made over a copy of the tuple, as C# copies the receiver into the delegate.
        """var p = (X: 1, Y: new { Z = "q" }); Func<object?, bool> eq = p.Equals; Func<string> text = p.ToString; p.X = 5; return eq((1, new { Z = "q" })) + "|" + text() + "|" + p;""", // True|(1, { Z = q })|(5, { Z = q })
        // An element whose type never says what its text is writes its type's name, and so does the value
        // itself in every route into text; a null one writes nothing.
        """var xs = new[] { 1, 2 }; var l = new List<string> { "a" }; Func<int, string> f = n => ""; int[]? none = null; return (xs, 3).ToString() + "|" + (l, f).ToString() + "|" + xs + "|" + $"{l}|{f}|" + xs.ToString() + "|[" + none + "]|" + string.Join(";", new[] { xs }) + "|" + string.Format("{0}", xs);""",
        // A set of tuples and a dictionary keyed by anonymous objects find their values, and print them.
        """var set = new HashSet<(int, string)> { (1, "a") }; var d = new Dictionary<object, int> { [new { A = 1 }] = 1 }; return set.Contains((1, "a")) + "|" + d.ContainsKey(new { A = 1 }) + "|" + string.Join(",", set);""", // True|True|(1, a)
    ];

    /// <summary>
    /// An anonymous object as a KEY: a set's element, <c>Distinct</c>'s and <c>GroupBy</c>'s key. Two keys of
    /// one anonymous type are always of the same members in the same order, so the comparison that refuses
    /// another member set must still find them equal, whether the members compare by value (<c>$eq.equals</c>)
    /// or one of them by reference (the comparison generated from the member types).
    /// </summary>
    public static readonly string[] AnonymousKeys =
    [
        """var set = new HashSet<object>(); var a = set.Add(new { A = 1, B = "x" }); var b = set.Add(new { A = 1, B = "x" }); var typed = new[] { new { A = 1 }, new { A = 1 }, new { A = 2 } }.ToHashSet(); return a + "|" + b + "|" + set.Count + "|" + typed.Count;""", // True|False|1|2
        """var xs = new[] { 1 }; var set = new[] { new { L = xs, N = 1 }, new { L = xs, N = 1 }, new { L = new[] { 1 }, N = 1 } }.ToHashSet(); return set.Count + "|" + set.Contains(new { L = xs, N = 1 });""", // 2|True
        """var xs = new[] { 1 }; return new[] { new { A = 1, B = "x" }, new { A = 1, B = "x" }, new { A = 2, B = "x" } }.Distinct().Count() + "|" + new[] { new { L = xs }, new { L = xs }, new { L = new[] { 1 } } }.Distinct().Count();""", // 2|2
        """var xs = new[] { 1 }; var g = new[] { 1, 2, 3, 4 }.GroupBy(n => new { Odd = n % 2 == 1, L = xs }).Select(x => x.Key.Odd + ":" + x.Count()); var h = new[] { 1, 2, 3 }.GroupBy(n => new { Big = n > 1 }).Select(x => x.Key + "=" + x.Count()); return string.Join(",", g) + "|" + string.Join(",", h);""", // True:2,False:2|{ Big = False }=1,{ Big = True }=2
    ];

    /// <summary>
    /// A value the browser holds as DATA (<c>Color</c>, <c>Curve</c>, <c>[TwinIsData]</c>) is a plain object too, and
    /// not <c>IFormattable</c>: an aligned hole, a hole with a format and <c>string.Format</c> write its record
    /// text, as its <c>ToString</c> does, where the formatter wrote <c>[object Object]</c>.
    /// </summary>
    public static readonly string[] DataTwinThroughTheFormatter =
    [
        """var c = Color.FromRgba(1, 2, 3, 4); return $"[{c,40}]|[{c,-40}]|[{c:X}]";""",
        """var c = Color.FromRgba(1, 2, 3, 4); return string.Format("[{0}]|[{0,40}]|[{1}]", c, (object)c);""",
        """var c = Curve.Standard; return $"[{c,50}]|[{c:N2}]|" + string.Format("[{0,-50}]", c);""",
        """Color? some = Color.FromRgba(1, 2, 3, 4); Color? none = null; return $"[{some,40}]|[{none,4}]|" + string.Format("[{0}]", none);""",
    ];

    /// <summary>A value tuple's <c>Equals</c> and <c>GetHashCode</c>, measured beside its text.</summary>
    public static readonly string[] TupleEquality =
    [
        """var a = (1, "x"); var b = (1, "x"); return a.Equals(b) + "|" + a.Equals((object)b) + "|" + a.Equals((2, "x")) + "|" + a.Equals((object?)null) + "|" + (1, 2).Equals((object)(1, 2, 3));""", // True|True|False|False|False
        """var xs = new[] { 1 }; return (xs, 1).Equals((xs, 1)) + "|" + (new[] { 1 }, 1).Equals((new[] { 1 }, 1)) + "|" + (1, (2, 3)).Equals((1, (2, 3))) + "|" + (double.NaN, 1).Equals((double.NaN, 1));""", // True|False|True|True
        """var a = (1, "x", (2.5, 'c')); var b = (1, "x", (2.5, 'c')); Func<int> h = a.GetHashCode; return (a.GetHashCode() == b.GetHashCode()) + "|" + (h() == b.GetHashCode()) + "|" + ((1, 2, 3, 4, 5, 6, 7, 8, 9).GetHashCode() == (1, 2, 3, 4, 5, 6, 7, 8, 9).GetHashCode());""", // True|True|True
        // The method group compares as the call does: by value, and an array element by reference.
        """var t = (1, 2); Func<object?, bool> same = t.Equals; object o = (1, 2); return same((1, 2)) + "|" + same((2, 1)) + "|" + o.Equals((1, 2)) + "|" + (t == (1, 2));""", // True|False|True|True
        """var xs = new[] { 1 }; var t = (xs, 1); Func<object?, bool> eq = t.Equals; return eq((xs, 1)) + "|" + eq((new[] { 1 }, 1));""", // True|False
        // Elements of other TYPES make another tuple type, which the browser holds as the same array: a byte
        // and an int, an int and a double, a string and a char; a tuple's element names are no part of its type.
        """(int X, int Y) p = (1, 2); return ((byte)1, 2).Equals((object)(1, 2)) + "|" + (1, 2).Equals((object)(1.0, 2.0)) + "|" + ("a", 1).Equals((object)('a', 1)) + "|" + p.Equals((object)(1, 2)) + "|" + (1, 2).Equals((object)p);""", // False|False|False|True|True
        """(int, int)? n = (1, 2); (int, int)? none = null; string? s = null; return n.Equals((object)(1.0, 2.0)) + "|" + none.Equals((object)(1.0, 2.0)) + "|" + none.Equals((object?)s) + "|" + (1, 2).Equals((object?)n);""", // False|False|True|True
        // A Nullable tuple's Equals is its value's, and an empty one equals only null.
        """(int, int)? n = (1, 2); (int, int)? none = null; return n.Equals((1, 2)) + "|" + none.Equals(null) + "|" + none.Equals((1, 2)) + "|" + (n.GetHashCode() == (1, 2).GetHashCode()) + "|" + none.GetHashCode();""", // True|True|False|True|0
    ];

    public static IEnumerable<object?[]> Each(string[] cases) => cases.Select(statements => new object?[] { statements });

    public static IEnumerable<object?[]> InEveryCulture(string[] cases) =>
        cases.SelectMany(statements => Cultures.Select(culture => new object?[] { statements, culture }));

    public static IEnumerable<object?[]> AnonymousEqualityCases() => Each(AnonymousEquality);
    public static IEnumerable<object?[]> AnonymousTextCases() => InEveryCulture(AnonymousText);
    public static IEnumerable<object?[]> TupleTextCases() => InEveryCulture(TupleText);
    public static IEnumerable<object?[]> TupleEqualityCases() => Each(TupleEquality);
    public static IEnumerable<object?[]> AnonymousKeyCases() => Each(AnonymousKeys);
    public static IEnumerable<object?[]> DataTwinCases() => Each(DataTwinThroughTheFormatter);

    [SkippableTheory]
    [MemberData(nameof(AnonymousEqualityCases))]
    public void AnAnonymousObject_ComparesAndHashesAsDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }

    [SkippableTheory]
    [MemberData(nameof(AnonymousTextCases))]
    public void AnAnonymousObject_IsWrittenAsDotNetWritesIt(string statements, string? culture)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude, culture);
    }

    [SkippableTheory]
    [MemberData(nameof(TupleTextCases))]
    public void AValueTuple_IsWrittenAsDotNetWritesIt(string statements, string? culture)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude, culture);
    }

    [SkippableTheory]
    [MemberData(nameof(AnonymousKeyCases))]
    public void AnAnonymousKey_FindsItsEqualAsDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }

    [SkippableTheory]
    [MemberData(nameof(DataTwinCases))]
    public void AValueHeldAsData_IsWrittenThroughTheFormatterAsDotNetWritesIt(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertVocabularyStatementsSameAsDotNet(statements);
    }

    [SkippableTheory]
    [MemberData(nameof(TupleEqualityCases))]
    public void AValueTuple_ComparesAndHashesAsDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }
}
