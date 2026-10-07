using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A property a derived type can override, or that overrides one, or whose accessors use <c>field</c>,
/// keeps its value in a store of its own under accessors on the twin's prototype, as a C# property is a
/// pair of methods over its backing field (#591, #615), run through the module graph an app's build
/// writes, both sides executed. Its value was written onto the instance under the property's name, which
/// reached an accessor of the name along the chain: the twin threw at <c>new</c> where one level computed
/// the property and another kept it, and where it did not throw, the instance's own value hid the
/// override. A record's property that used <c>field</c> ran none of its accessors' bodies.
/// </summary>
public class PropertyStoreConformanceTests
{
    private const string Classes = """
        public class KBase { public virtual string Kind => "base"; public string Read() => Kind; }
        public class KDerived : KBase { public override string Kind { get; } = "derived"; }
        public class LBase { public virtual string Kind { get; set; } = "base"; public string Read() => Kind; }
        public class LDerived : LBase { public override string Kind => "derived"; }
        public class MBase { public virtual int N { get; set; } = 1; public int Read() => N; }
        public class MDerived : MBase { public override int N { get; set; } = 2; }
        public class PBase { public virtual string Name { get; set; } = "p"; }
        public class PDerived : PBase { public override string Name { get => base.Name + "!"; set => base.Name = value; } }
        public abstract class QBase { public abstract string Tag { get; } public string Read() => Tag; }
        public class QDerived : QBase { public override string Tag { get; } = "q"; }
        public class WBase { public virtual string Kind { get; set; } = "b"; }
        public class WDerived : WBase { public override string Kind => "d"; }
        public class Semi { public string Log = ""; public int Size { get; set { Log += value; } } = 4; }
        """;

    private static readonly (string Name, string Statements)[] ClassCases =
    [
        ("an auto-property over a computed one", "return new KDerived().Kind + \"|\" + new KDerived().Read();"),
        ("a computed override over an auto-property", "return new LDerived().Read() + \"|\" + ((LBase)new LDerived()).Kind;"),
        ("an auto-property over an auto-property", "var d = new MDerived(); var a = d.Read(); d.N = 5; return a + \"|\" + d.Read() + \"|\" + ((MBase)d).N;"),
        ("an override that reads its base's", "var d = new PDerived(); var a = d.Name; d.Name = \"z\"; return a + \"|\" + d.Name;"),
        ("an auto-property over an abstract one", "return new QDerived().Read();"),
        ("an override that declares only a getter keeps its base's setter", "var d = new WDerived(); d.Kind = \"x\"; return d.Kind + \"|\" + ((WBase)d).Kind;"),
        ("a property with an accessor of its own and a backing field", "var s = new Semi(); s.Size = 7; return s.Size + \"|\" + s.Log;"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AClassesOverridableProperty_KeepsAStoreOfItsOwn(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Classes, typeAnnotations, ClassCases);

    private const string Records = """
        public record RBase { public virtual string Kind => "base"; public string Read() => Kind; }
        public record RDerived : RBase { public override string Kind { get; } = "derived"; }
        public record RBase2 { public virtual string Kind { get; init; } = "base"; public string Read() => Kind; }
        public record RDerived2 : RBase2 { public override string Kind => "derived"; }
        public record VRec { public virtual int N { get; init; } = 1; }
        public record VRec2 : VRec { public override int N { get; init; } = 2; }
        public record FRec { public int X { get; set => field = value * 2; } }
        public record GRec { public int X { get => field + 1; set => field = value; } }
        public struct FSt { public int X { get; set => field = value * 2; } }
        """;

    private static readonly (string Name, string Statements)[] RecordCases =
    [
        ("an auto-property over a computed one", "return new RDerived().Kind + \"|\" + new RDerived().Read();"),
        ("a computed override over an auto-property", "return new RDerived2().Read() + \"|\" + ((RBase2)new RDerived2()).Kind;"),
        ("a with over a computed override, and its equality",
            "var a = new RDerived2(); var b = a with { Kind = \"x\" }; return (a == b) + \"|\" + b.Read() + \"|\" + (a == new RDerived2());"),
        ("an auto-property over an auto-property, its with, equality and text",
            "var v = new VRec2(); var w = v with { N = 9 }; return v.N + \"|\" + w.N + \"|\" + (v == new VRec2()) + \"|\" + (v == w) + \"|\" + v;"),
        ("a property that uses field, through its initializer and its with",
            "var r = new FRec { X = 5 }; var s = r with { X = 7 }; return r.X + \"|\" + s.X + \"|\" + (r == new FRec { X = 5 }) + \"|\" + r;"),
        ("a getter with a body over its store", "var g = new GRec(); var h = new GRec { X = 3 }; return g.X + \"|\" + h.X + \"|\" + (g == new GRec());"),
        ("a struct's property that uses field, and its zero", "var a = new FSt { X = 5 }; FSt z = default; return a.X + \"|\" + z.X + \"|\" + a.Equals(new FSt { X = 5 });"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void ARecordsOverridableProperty_KeepsAStoreOfItsOwn(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Records, typeAnnotations, RecordCases);
}
