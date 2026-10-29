using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The keys and the elements LINQ's operators hold, found as the type's default comparer
/// (<c>EqualityComparer&lt;T&gt;.Default</c>) finds them: a record, a decimal and a date by value, an
/// array a record holds by reference, a class by its own <c>Equals</c> where it overrides one, and a
/// value behind an interface by what it turns out to be. A JavaScript <c>Set</c> and <c>Map</c> compare
/// by reference, and <c>JSON.stringify</c> by the text of every member.
/// </summary>
public class LinqKeyEqualityConformanceTests
{
    public const string Prelude = """
        public record Point(int X, int Y);
        public record Holder(int[] Items);
        public interface IShape { }
        public record Square(int Side) : IShape;
        """;

    public static readonly string[] Elements =
    [
        // A record, by its members.
        """var p = new[] { new Point(1, 2), new Point(1, 2), new Point(3, 4) }; var q = new[] { new Point(1, 2) }; return p.Distinct().Count() + "|" + p.Union(q).Count() + "|" + p.Intersect(q).Count() + "|" + p.Except(q).Count() + "|" + p.GroupBy(x => x).Count() + "|" + p.Join(q, x => x, y => y, (x, y) => 0).Count();""", // 2|2|1|1|2|2
        // A record holding an array, the array by reference.
        """var xs = new[] { 1 }; var p = new[] { new Holder(xs), new Holder(xs), new Holder(new[] { 1 }) }; return p.Distinct().Count() + "|" + p.Union(new[] { new Holder(xs) }).Count() + "|" + p.GroupBy(h => h).Count() + "|" + p.DistinctBy(h => h).Count();""", // 2|2|2|2
        // A decimal, by value whatever its scale.
        """var m = new[] { 1.0m, 1.00m, 2m }; return m.Distinct().Count() + "|" + m.Union(new[] { 2.0m }).Count() + "|" + m.Intersect(new[] { 1m }).Count() + "|" + m.Except(new[] { 1m }).Count() + "|" + m.GroupBy(x => x).Count() + "|" + m.Join(new[] { 1m }, x => x, y => y, (x, y) => 0).Count();""", // 2|2|1|1|2|2
        // A date, by value.
        """var d = new[] { new DateTime(2020, 1, 1), new DateTime(2020, 1, 1), new DateTime(2021, 1, 1) }; return d.Distinct().Count() + "|" + d.Union(d).Count() + "|" + d.DistinctBy(x => x.Date).Count() + "|" + d.GroupBy(x => x).Count();""", // 2|2|2|2
        // A value behind an interface, by what it turns out to be: a record by its members.
        """IShape[] s = { new Square(1), new Square(1) }; return s.Distinct().Count() + "|" + s.GroupBy(x => x).Count() + "|" + s.Union(new IShape[] { new Square(2) }).Count();""", // 1|1|2
        // A tuple holding an array, the array by reference.
        """var xs = new[] { 1 }; var t = new[] { (xs, 1), (xs, 1), (new[] { 1 }, 1) }; return t.Distinct().Count() + "|" + t.Union(t).Count() + "|" + t.GroupBy(x => x).Count();""", // 2|2|2
        // A null key matches nothing in a join, as .NET skips it: a record compared by value, which the
        // runtime's dictionary refuses as a key, and a string, which a Map found equal to another null.
        """var a = new[] { (K: (Point?)null, V: "a"), (K: new Point(1, 2), V: "b") }; var b = new[] { (K: (Point?)null, W: "n"), (K: new Point(1, 2), W: "y") }; return string.Join(",", a.Join(b, x => x.K, y => y.K, (x, y) => x.V + y.W)) + "|" + string.Join(",", a.GroupJoin(b, x => x.K, y => y.K, (x, ys) => x.V + ys.Count()));""", // by|a0,b1
        """var a = new[] { (K: (string?)null, V: "a"), (K: "x", V: "b") }; var b = new[] { (K: (string?)null, W: "n"), (K: "x", W: "y") }; return string.Join(",", a.Join(b, x => x.K, y => y.K, (x, y) => x.V + y.W)) + "|" + string.Join(",", a.GroupJoin(b, x => x.K, y => y.K, (x, ys) => x.V + ys.Count()));""", // by|a0,b1
        // A double, NaN equal to NaN and 0 to -0, as SameValueZero already answers.
        """var n = new[] { double.NaN, double.NaN, 0.0, -0.0 }; return n.Distinct().Count() + "|" + n.GroupBy(x => x).Count() + "|" + n.Union(n).Count();""", // 2|2|2
    ];

    public static IEnumerable<object?[]> ElementCases() => Elements.Select(row => new object?[] { row });

    [SkippableTheory]
    [MemberData(nameof(ElementCases))]
    public void AKeyOrAnElement_IsFoundAsTheTypesDefaultComparerFindsIt(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }

    /// <summary>A class that overrides <c>Equals</c>, and one that does not.</summary>
    private const string Classes = """
        using System.Linq;

        public class Code
        {
            public int V;
            public Code(int v) => V = v;
            public override bool Equals(object? other) => other is Code code && code.V == V;
            public override int GetHashCode() => V;
        }
        public class Plain
        {
            public int V;
            public Plain(int v) => V = v;
        }
        public class Animal { public int Legs; }
        public class Dog : Animal
        {
            public int Id;
            public Dog(int id) => Id = id;
            public override bool Equals(object? other) => other is Dog dog && dog.Id == Id;
            public override int GetHashCode() => Id;
        }
        """;

    private static readonly (string Name, string Statements)[] ClassCases =
    [
        ("Distinct, Union, Intersect and Except ask its own Equals",
            "var c = new[] { new Code(1), new Code(1), new Code(2) }; return c.Distinct().Count() + \"|\" + c.Union(new[] { new Code(2) }).Count() + \"|\" + c.Intersect(new[] { new Code(1) }).Count() + \"|\" + c.Except(new[] { new Code(1) }).Count();"), // 2|2|1|1
        ("GroupBy, a join, DistinctBy and a lookup ask its own Equals",
            "var c = new[] { new Code(1), new Code(1), new Code(2) }; return c.GroupBy(x => x).Count() + \"|\" + c.Join(new[] { new Code(1) }, x => x, y => y, (x, y) => 0).Count() + \"|\" + c.DistinctBy(x => x).Count() + \"|\" + c.ToLookup(x => x)[new Code(1)].Count();"), // 2|2|2|2
        ("GroupJoin, the other By forms and a lookup's grouping ask its own Equals",
            "var c = new[] { new Code(1), new Code(1), new Code(2) }; return c.GroupJoin(new[] { new Code(1) }, x => x, y => y, (x, ys) => ys.Count()).Sum() + \"|\" + c.UnionBy(new[] { new Code(3) }, x => x).Count() + \"|\" + c.IntersectBy(new[] { new Code(1) }, x => x).Count() + \"|\" + c.ExceptBy(new[] { new Code(1) }, x => x).Count() + \"|\" + c.ToLookup(x => x).Count;"), // 2|3|1|1|2
        ("a lookup's Contains asks its own Equals, its subclass's, and a reference otherwise (#1145)",
            "var c = new[] { new Code(1), new Code(2) }.ToLookup(x => x); var p = new Plain(1); var ps = new[] { p }.ToLookup(x => x); Animal[] a = { new Dog(1) }; var d = a.ToLookup(x => x); return c.Contains(new Code(2)) + \"|\" + c.Contains(new Code(3)) + \"|\" + ps.Contains(p) + \"|\" + ps.Contains(new Plain(1)) + \"|\" + d.Contains(new Dog(1));"), // True|False|True|False|True
        ("a class that does not override Equals is found by reference",
            "var p = new Plain(1); var ps = new[] { p, p, new Plain(1) }; return ps.Distinct().Count() + \"|\" + ps.GroupBy(x => x).Count() + \"|\" + ps.Union(new[] { p }).Count() + \"|\" + ps.Join(new[] { p }, x => x, y => y, (x, y) => 0).Count();"), // 2|2|2|2
        ("a class that does not override Equals asks the Equals its subclass overrides",
            "Animal[] a = { new Dog(1), new Dog(1), new Dog(2) }; return a.Distinct().Count() + \"|\" + a.GroupBy(x => x).Count() + \"|\" + a.Union(new Animal[] { new Dog(2) }).Count() + \"|\" + a.Join(new Animal[] { new Dog(1) }, x => x, y => y, (x, y) => 0).Count();"), // 2|2|2|2
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AClassKey_IsFoundByItsOwnEquals_OrByReference(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Classes, typeAnnotations, ClassCases);
}
