using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A generic record or struct equals only a value of its own closed type, as .NET compares a record's
/// EqualityContract, both sides executed through the module graph an app's build writes, one case at a
/// time. The twin is one class for every type argument, so <c>Box&lt;int&gt;</c> equalled
/// <c>Box&lt;double&gt;</c> holding the same 1 (#651). A value built where C# names the type arguments
/// carries them now; one built inside generic code carries none and is not taken for another type. A
/// copy is of the closed type of what it copies: a struct's copy before a write or into a box, and a
/// record's <c>with</c> through a declared copy constructor, both built without the constructor, went
/// unmarked (#751).
/// </summary>
public class GenericRecordClosureConformanceTests
{
    private const string Declarations = """
        public record Box<T>(T Value);
        public record struct Pair<T>(T A);
        public record Kept<T>(T Value) { protected Kept(Kept<T> original) { Value = original.Value; } }
        public static class Make { public static Box<T> Boxed<T>(T value) => new Box<T>(value); }
        """;

    public static TheoryData<string, string, bool> Cases()
    {
        var cases = new (string Name, string Statements)[]
        {
            ("a Box<int> and a Box<double> of one value", "return new Box<int>(1).Equals((object)new Box<double>(1));"),
            ("two Box<int> of one value", "return new Box<int>(1).Equals((object)new Box<int>(1));"),
            ("with keeps the closed type", "return (new Box<int>(1) with { Value = 2 }).Equals((object)new Box<double>(2));"),
            ("a list of objects", "return new System.Collections.Generic.List<object> { new Box<int>(1) }.Contains(new Box<double>(1));"),
            ("a generic struct", "return new Pair<int>(1).Equals((object)new Pair<double>(1));"),
            ("a target-typed new", "Box<int> box = new(1); return box.Equals((object)new Box<double>(1));"),
            ("tuple element names, which the runtime type erases", "return new Box<(int A, int B)>((1, 2)).Equals((object)new Box<(int, int)>((1, 2)));"),
            ("dynamic, which the runtime type takes as object", "return new Box<dynamic>(1).Equals((object)new Box<object>(1));"),
            ("a box built in generic code, against its own type", "return Make.Boxed(1).Equals((object)new Box<int>(1));"),
            ("a generic struct copied before a write", "var pair = new Pair<int>(1); var copy = pair; copy.A = 2; return copy.Equals((object)new Pair<double>(2));"),
            ("a generic struct copied before a write, against its own type", "var pair = new Pair<int>(1); var copy = pair; copy.A = 2; return copy.Equals((object)new Pair<int>(2));"),
            ("a generic struct copied into a box", "var pair = new Pair<double>(1); object boxed = pair; return new Pair<int>(1).Equals(boxed);"),
            ("with through a declared copy constructor", "return (new Kept<int>(1) with { Value = 2 }).Equals((object)new Kept<double>(2));"),
            ("with through a declared copy constructor, against its own type", "return (new Kept<int>(1) with { Value = 2 }).Equals((object)new Kept<int>(2));"),
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
    public void AGenericRecord_EqualsOnlyItsOwnClosedType(string name, string statements, bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Declarations, typeAnnotations, (name, statements));
}
