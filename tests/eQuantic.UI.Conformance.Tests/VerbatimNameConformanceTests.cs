using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A name spelled verbatim (<c>@Shelf</c>) is the name without its <c>@</c>, as C# reads it: the type, its
/// module, a member, a parameter and a local are one name with <c>Shelf</c> (#704). eqc wrote the
/// declaration as it was spelled, so <c>public static class @Shelf</c> went out as <c>export class
/// @Shelf</c> in a module named <c>@Shelf</c>, neither of them JavaScript, while every reference wrote
/// <c>Shelf</c>. Run through the module graph an app's build writes, so the module that reads the types
/// has to import them by the name their own modules carry.
/// </summary>
public class VerbatimNameConformanceTests
{
    private const string Source = """
        namespace Store
        {
            public static class @Shelf { public static int @Raise() => 1; }

            public class @Box
            {
                public int @value = 2;
                public int Count { get; set; } = 3;
                public int @Read(int @event) => @value + @event + @Count;
            }

            public record @Point(int @X, int Y);

            public enum @Tone { @Warm, Cold }

            public class Tag
            {
                public string @class { get; set; } = "c";
                public static string @Join(string @default, params string[] @params) => @default + string.Join("", @params);
            }

            public class @Failure : System.Exception
            {
                public @Failure(string @message) : base(@message) { }
            }

            public class @Base { public virtual int Level() => 1; }

            public class Derived : @Base { public override int Level() => 2; }

            public static class Calc
            {
                public static int nameof(int x) => x * 2;
                public static int Twice(int x) => @nameof(x) + nameof(x);
            }

            public class Holder
            {
                public @Point Where { get; set; } = new @Point(3, 4);
                public static int Twice(@Box box) => box.@value * 2;
            }
        }

        namespace @Depot
        {
            public static class Crate { public static int Size() => 9; }
        }
        """;

    private static readonly (string Name, string Statements)[] Cases =
    [
        ("a static class spelled verbatim, read without the @", "return Store.Shelf.Raise();"),
        ("the same class read with it", "return Store.@Shelf.@Raise();"),
        ("a field, a parameter and a method spelled verbatim", "var b = new Store.Box(); return b.Read(4) * 10 + b.@value;"),
        ("a record spelled verbatim prints its own name", "var p = new Store.@Point(1, 2); return p.ToString() + p.@X + p.Y;"),
        ("a record's positional member reads either way", "var p = new Store.Point(@X: 5, Y: 6); return p.X + p.@Y;"),
        ("an enum and its member spelled verbatim", "return Store.@Tone.@Warm.ToString() + (int)Store.Tone.Cold;"),
        ("a local spelled verbatim", "var @total = 7; var @if = 8; return @total + @if;"),
        ("a keyword as a property and as parameters", "var t = new Store.Tag { @class = \"k\" }; return t.@class + Store.Tag.Join(\"d\", \"p\", \"q\");"),
        ("a namespace spelled verbatim", "return Depot.Crate.Size() + @Depot.Crate.Size();"),
        ("an exception class spelled verbatim", "try { throw new Store.@Failure(\"m\"); } catch (Store.Failure f) { return f.Message + (f is Store.@Failure); }"),
        ("a base class spelled verbatim", "Store.@Base b = new Store.Derived(); return b.Level() + (b is Store.@Base ? 10 : 0);"),
        ("a property and a parameter typed verbatim", "var h = new Store.Holder(); return h.Where.X + Store.Holder.Twice(new Store.@Box());"),
        ("a local, a pattern and a cast typed verbatim", "object o = new Store.@Box(); Store.@Box typed = (Store.@Box)o; return (o is Store.@Box b ? b.@value : 0) + typed.Count;"),
        ("an array and a list of a type spelled verbatim", "var boxes = new Store.@Box[] { new Store.Box() }; var list = new System.Collections.Generic.List<Store.@Box> { new Store.@Box() }; return boxes.Length + list.Count + boxes[0].Count;"),
        ("a method named nameof, called with the escape and without it", "return Store.Calc.Twice(3);"),
        ("an enum spelled verbatim named by typeof", "return string.Join(\",\", System.Enum.GetNames(typeof(Store.@Tone)));"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void ANameSpelledVerbatim_IsTheNameWithoutItsAt(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Source, typeAnnotations, Cases);
}
