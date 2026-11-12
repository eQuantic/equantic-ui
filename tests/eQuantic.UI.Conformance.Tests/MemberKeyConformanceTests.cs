using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Members that C# tells apart and the twin once held on one key answer as .NET answers them, run through
/// the module graph an app's build writes, both sides executed. A partial property, indexer or event is
/// one member, written from its implementing half (#1172): the defining half was written beside it, so a
/// class's setter never ran and a record counted its state twice. A static field moves to a slot of its
/// own beside a static member of its name, as an instance field does (#1175): the backing-field idiom was
/// refused. Each case reads a difference rather than a total, since the statics a case changes are the
/// next case's too, on both sides.
/// </summary>
public class MemberKeyConformanceTests
{
    private const string Source = """
        public sealed partial class Gauge
        {
            public partial int Level { get; set; }
            public partial int Level { get => field; set => field = value * 2; }
        }
        public sealed partial record Meter(int N)
        {
            public partial int Level { get; set; }
            public partial int Level { get => field; set => field = value * 2; }
        }
        public sealed partial class Grid
        {
            public partial int this[int i] { get; }
            public partial int this[int i] { get => i * 2; }
        }
        public static partial class Dial
        {
            public static partial int Level { get; set; }
            public static partial int Level { get => field; set => field = value + 1; }
        }
        public static class Counter
        {
            private static int count;
            public static int Count { get => count; set => count = value * 2; }
        }
        public sealed class Seq
        {
            private static int next;
            public static int Next() => ++next;
            public static int Peek => next;
        }
        public static class Limits
        {
            private const int max = 10;
            public static int Max => max * 2;
        }
        public sealed class Ordered
        {
            private static int count = Seed();
            private static int Seed() => 5;
            public static int Count => count;
        }
        public sealed record Stamp(int V)
        {
            private static int made;
            public static int Made => made;
            public static Stamp Make(int v) { made++; return new(v); }
        }
        public class Tally { protected static int total; public static int Read() => total; }
        public sealed class Ledger : Tally { public static int Total() => ++total; }
        """;

    private static readonly (string Name, string Statements)[] Cases =
    [
        ("a partial property's setter, on a class", "var g = new Gauge(); g.Level = 3; return g.Level;"),
        ("a partial property on a record: its setter, its equality and its text",
            "var a = new Meter(1) { Level = 3 }; var b = new Meter(1) { Level = 3 }; "
            + "return a.Level + \"|\" + a.Equals(b) + \"|\" + a.Equals(new Meter(1)) + \"|\" + a;"),
        ("a partial indexer", "return new Grid()[4];"),
        ("a static partial property", "Dial.Level = 3; return Dial.Level;"),
        ("a static field behind a setter with logic", "Counter.Count = 3; return Counter.Count;"),
        ("a static field beside the method that counts it",
            "var a = Seq.Next(); var b = Seq.Next(); return (b - a) + \"|\" + (Seq.Peek == b);"),
        ("a constant beside a static property of its name", "return Limits.Max;"),
        ("a static field the type initializer starts", "return Ordered.Count;"),
        ("a record's static field beside its property", "var before = Stamp.Made; Stamp.Make(1); return Stamp.Made - before;"),
        ("a base's static field beside a derived static method of its name",
            "var before = Tally.Read(); var after = Ledger.Total(); return (after - before) + \"|\" + (Tally.Read() == after);"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AMemberOnItsOwnKey_AnswersAsDotNetDoes(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Source, typeAnnotations, Cases);
}
