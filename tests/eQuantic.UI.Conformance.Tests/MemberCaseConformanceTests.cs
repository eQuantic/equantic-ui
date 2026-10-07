using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A field and a property or a method a case apart stay two members in the browser, both sides executed
/// through the module graph an app's build writes, one case at a time. They lowered to one name: the
/// own field hid the accessors, so a setter never ran, an auto-property and its field shared one slot,
/// and a call reached the number the field held (#396). The field moves now, to a slot with a `$` after
/// its name.
/// </summary>
public class MemberCaseConformanceTests
{
    private const string Declarations = """
        public class Doubler { int value; public int Value { get => value; set => this.value = value * 2; } }
        public class Named { string name = "ana"; public string Name { get; set; } = "bia"; public string Both() => name + Name; }
        public class Sized { int size = 3; public int Size() => size * 2; }
        public class Counted { int count; public int Count => count; public void Set() { count = 2; } }
        public class Pair { int value = 1; public int Value => value * 10; public int Sum(Pair other) => other.value + value; }
        public class Base { public int Total { get; set; } = 5; }
        public class Derived : Base { int total = 7; public int Own() => total + Total; }
        """;

    public static TheoryData<string, string, bool> Cases()
    {
        var cases = new (string Name, string Statements)[]
        {
            ("a setter beside its field", "var d = new Doubler(); d.Value = 3; return d.Value;"),
            ("an auto-property beside a field", "return new Named().Both();"),
            ("a method beside a field", "return new Sized().Size();"),
            ("a getter beside its field", "var c = new Counted(); c.Set(); return c.Count;"),
            ("another instance's field", "var a = new Pair(); return a.Sum(new Pair()) + a.Value;"),
            ("a field beside a base's property", "return new Derived().Own();"),
        };
        var data = new TheoryData<string, string, bool>();
        foreach (var (name, statements) in cases)
        {
            data.Add(name, statements, true);
            data.Add(name, statements, false);
        }
        return data;
    }

    [SkippableTheory]
    [MemberData(nameof(Cases))]
    public void AFieldAndAMemberACaseApart_StayTwoMembers(string name, string statements, bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Declarations, typeAnnotations, (name, statements));
}
