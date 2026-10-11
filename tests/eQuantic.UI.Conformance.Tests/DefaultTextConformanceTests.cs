using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A value whose type never says what its text is writes the type's full name, as Object.ToString and
/// ValueType.ToString do (#570): a plain struct's twin wrote a record's text, and a plain class's
/// JavaScript's <c>[object Object]</c>. A record keeps the text it synthesizes, and a type that declares
/// ToString, or derives it from a base that does, keeps its own. Run through the module graph an app's
/// build writes, where a type has the namespace it is declared in.
/// </summary>
public class DefaultTextConformanceTests
{
    private const string Source = """
        namespace My.App
        {
            public struct Pt { public int X; }
            public class Plain { public int X = 1; }
            public class Outer { public struct In { public int Y; } }
            public class Named { public override string ToString() => "named"; }
            public class FromNamed : Named { }
            public class Base { }
            public class Derived : Base { }
            public record Rec(int A);
        }
        public struct Bare { }
        """;

    private static readonly (string Name, string Statements)[] Cases =
    [
        ("a plain struct writes its full name", "return new My.App.Pt().ToString();"),
        ("a plain class writes its full name", "return new My.App.Plain().ToString() + \"|\" + new My.App.Plain();"),
        ("a nested struct writes its owner and itself", "return new My.App.Outer.In().ToString();"),
        ("a type that declares its text keeps it, and so does one over it", "return new My.App.Named() + \"|\" + new My.App.FromNamed();"),
        ("a class over a plain base writes its own name", "My.App.Base b = new My.App.Derived(); return b.ToString() + \"|\" + new My.App.Base();"),
        ("a record keeps its own text", "return new My.App.Rec(1).ToString();"),
        ("a type in no namespace writes its name", "return new Bare().ToString() + \"|\" + $\"{new My.App.Pt()}\";"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AValueWhoseTypeSaysNothingOfItsText_WritesItsFullName(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Source, typeAnnotations, Cases);
}
