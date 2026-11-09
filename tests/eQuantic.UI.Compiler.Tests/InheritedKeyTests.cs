using System.Linq;
using eQuantic.UI.Compiler;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// The twins of a class chain hold one member per key, as one twin does (#1146), whatever kind each
/// member is (#1173). Along the chain only two methods were compared, so a derived member on the key a
/// member of an in-source base holds passed the build, and one of the two answered every access to the
/// other: <c>Base.Size =&gt; 3</c> beneath a derived <c>size()</c> made <c>new Derived().Size</c> a
/// function, and a value held on each instance hid a getter of its name.
/// </summary>
public class InheritedKeyTests
{
    private static CompilationResult Compile(string source, string name) =>
        new ComponentCompiler().CompileSource(source, "Probe.cs").Single(result => result.ComponentName == name);

    public static TheoryData<string, string, string, string> Refused => new()
    {
        {
            "public class Base { public int Size => 3; }\npublic sealed class Derived : Base { public int size() => 4; }",
            "Derived", "'Derived.size()' lowers to `size`", "'Base.Size', which it inherits (Probe.cs line 1)"
        },
        {
            "public class Base { public int size() => 3; }\npublic sealed class Derived : Base { public int Size => 4; }",
            "Derived", "'Derived.Size' lowers to `size`", "'Base.size()', which it inherits (Probe.cs line 1)"
        },
        {
            "public class Base { public static int Size => 3; }\npublic sealed class Derived : Base { public static int size() => 4; }",
            "Derived", "'Derived.size()' lowers to `static size`", "'Base.Size', which it inherits (Probe.cs line 1)"
        },
        {
            "public class Base { public int Size => 3; }\npublic sealed class Derived : Base { public event System.Action? size; }",
            "Derived", "'Derived.size' lowers to `size`", "'Base.Size', which it inherits (Probe.cs line 1)"
        },
        {
            "public class Base { public event System.Action? Changed; }\npublic sealed class Derived : Base { public bool changed => true; }",
            "Derived", "'Derived.changed' lowers to `changed`", "'Base.Changed', which it inherits (Probe.cs line 1)"
        },
        {
            "public record Base(int Size);\npublic sealed record Derived(int X) : Base(X) { public int size() => 1; }",
            "Derived", "'Derived.size()' lowers to `size`", "'Base.Size', which it inherits (Probe.cs line 1)"
        },
        {
            "public class Root { public int Size => 3; }\npublic class Middle : Root { }\npublic sealed class Leaf : Middle { public int size() => 4; }",
            "Leaf", "'Leaf.size()' lowers to `size`", "'Root.Size', which it inherits (Probe.cs line 1)"
        },
        {
            "using eQuantic.UI.Primitives;\npublic class Shell : StatelessComponent { protected string title = \"x\"; public override VisualNode Build(ComponentContext context) => new Text(title, TypeRole.BodyM); }\n[Page(\"/probe\")] public sealed class Page : Shell { public string Title => \"y\"; }",
            "Page", "'Page.Title' lowers to `title`", "'Shell.title', which it inherits (Probe.cs line 2)"
        },
    };

    [Theory]
    [MemberData(nameof(Refused))]
    public void AMemberOnAKeyItsBaseHolds_IsRefused(string source, string name, string later, string earlier)
    {
        var lines = source.Split('\n');
        var error = Compile(source, name).Errors.Should().ContainSingle().Subject;

        error.Code.Should().Be("EQ1007");
        error.Line.Should().Be(lines.Length);
        error.Message.Should().StartWith(later).And.Contain($"and so does {earlier}");
    }

    /// <summary>
    /// What holds a key of its own is not refused: an override is the member it overrides, a method that
    /// hides a member holds a name of its own (#563), a field that moves is in a slot of its own (#396,
    /// #1175), a static and an instance member never meet, a static that hides a base's static of its own
    /// C# name is reached through its own class, as the base's is through the base, and a member kept on the
    /// server reaches no twin.
    /// </summary>
    [Theory]
    [InlineData("public class Base { public virtual int Size => 3; }\npublic sealed class Derived : Base { public override int Size => 4; }")]
    [InlineData("public class Base { public int Size => 3; }\npublic sealed class Derived : Base { public new int Size() => 4; }")]
    [InlineData("public class Base { public int Size => 3; }\npublic sealed class Derived : Base { private int size; public int Read() => size; }")]
    [InlineData("public class Base { protected int size; }\npublic sealed class Derived : Base { public int Size => size; }")]
    [InlineData("public class Base { protected static int size; }\npublic sealed class Derived : Base { public static int Size() => size; }")]
    [InlineData("public class Base { public static int Size => 3; }\npublic sealed class Derived : Base { public int size() => 4; }")]
    [InlineData("public class Base { public static Base Default => new(); }\npublic sealed class Derived : Base { public static new Derived Default => new(); }")]
    [InlineData("public class Base { [eQuantic.UI.Primitives.ServerOnly] public int Size => 3; }\npublic sealed class Derived : Base { public int size() => 4; }")]
    public void AMemberOnAKeyOfItsOwn_IsNotRefused(string source) =>
        Compile(source, "Derived").Errors.Where(error => error.Code == "EQ1007").Should().BeEmpty();
}
