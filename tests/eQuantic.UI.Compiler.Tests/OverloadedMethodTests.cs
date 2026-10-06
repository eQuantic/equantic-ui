using System.Linq;
using eQuantic.UI.Compiler;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A method declared twice under one name in one type is refused (EQ1007), on every emitter.
/// <para>
/// Each of them lost one of the two in silence, and each in its own way: a plain class, a record and
/// a static class wrote both, and JavaScript kept the last, while a component's parser kept the
/// first and dropped the other. The kinds are compared side by side because they drifted apart
/// once already, as <c>PlainClassOperatorTests</c> records.
/// </para>
/// </summary>
public class OverloadedMethodTests
{
    private static CompilationResult Compile(string source, string name) =>
        new ComponentCompiler().CompileSource(source, "Probe.cs").Single(result => result.ComponentName == name);

    public static TheoryData<string, string, int> Overloads => new()
    {
        {
            "Plain", """
            public sealed class Plain
            {
                public int Twice(int x) => x * 2;
                public string Twice(string s) => s + s;
            }
            """, 4
        },
        {
            "Rec", """
            public sealed record Rec(int X)
            {
                public int Plus(int y) => X + y;
                public int Plus(int y, int z) => X + y + z;
            }
            """, 4
        },
        {
            "Helpers", """
            using System.Collections.Generic;

            public static class Helpers
            {
                public static int Size(this string s) => s.Length;
                public static int Size(this List<int> l) => l.Count;
            }
            """, 6
        },
        {
            "Blocks", """
            using System.Collections.Generic;

            public static class Blocks
            {
                extension(string s)
                {
                    public int Size() => s.Length;
                }

                extension(List<int> l)
                {
                    public int Size() => l.Count;
                }
            }
            """, 12
        },
        {
            "Page", """
            using eQuantic.UI.Primitives;

            [Page("/probe")]
            public sealed class Page : StatelessComponent
            {
                private string Label(int x) => x.ToString();
                private string Label(string s) => s;
                public override VisualNode Build(ComponentContext context) => new Text(Label(1) + Label("a"), TypeRole.BodyM);
            }
            """, 7
        },
    };

    [Theory]
    [MemberData(nameof(Overloads))]
    public void AnOverloadIsRefused_OnEveryEmitter(string name, string source, int line)
    {
        var result = Compile(source, name);

        result.Success.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle().Subject;
        error.Code.Should().Be("EQ1007");
        error.Line.Should().Be(line, "the SECOND declaration is the one the twin could not carry");
    }

    [Fact]
    public void TheErrorNamesBothOverloads()
    {
        var error = Compile("""
            public sealed class Plain
            {
                public int Twice(int x) => x * 2;
                public string Twice(string s) => s + s;
            }
            """, "Plain").Errors.Single();

        error.Message.Should().Contain("'Plain.Twice(string)' lowers to `twice()`")
            .And.Contain("'Twice(int)' (line 3)");
    }

    /// <summary>One lives on the class and the other on its prototype.</summary>
    [Fact]
    public void AStaticAndAnInstanceMethodMayShareAName()
    {
        var result = Compile("""
            public sealed class Plain
            {
                public static int Make(int x) => x;
                public int Make() => 1;
            }
            """, "Plain");

        result.Errors.Should().BeEmpty();
        result.TypeScript.Should().Contain("static make(").And.Contain("\n    make(");
    }

    /// <summary>Two names C# keeps apart, and the lowering folds into one.</summary>
    [Fact]
    public void TwoNamesThatLowerToOne_AreRefused()
    {
        var result = Compile("""
            public sealed class Plain
            {
                public int Foo() => 1;
                public int foo() => 2;
            }
            """, "Plain");

        result.Errors.Should().ContainSingle().Which.Code.Should().Be("EQ1007");
    }

    /// <summary>The defining half and the implementing half are one method.</summary>
    [Fact]
    public void APartialMethodIsOneMethod()
    {
        var result = Compile("""
            public sealed partial class Plain
            {
                partial void Hook();
                partial void Hook() { }
                public void Run() => Hook();
            }
            """, "Plain");

        result.Errors.Should().NotContain(error => error.Code == "EQ1007");
    }

    /// <summary>
    /// An abstract overload takes its name, though the base's twin writes nothing for it: the class
    /// that implements it writes it under that name, and in JavaScript the implementation is what
    /// every call on the name reaches, the base's other overload included. Left uncounted, neither
    /// class would declare two methods that are written, and <c>new Square().Draw("x")</c> would
    /// answer the square on the web where .NET answers the label.
    /// </summary>
    [Fact]
    public void AnAbstractOverload_TakesItsName_ForItsImplementationIsWrittenOverTheOther()
    {
        var result = Compile("""
            public abstract class Shape
            {
                public abstract string Draw(int size);
                public string Draw(string label) => "label " + label;
            }
            """, "Shape");

        result.Errors.Should().ContainSingle().Which.Code.Should().Be("EQ1007");
    }

    /// <summary>An explicit interface implementation's name is the interface's, which the author
    /// cannot change, so "give each its own name" is no answer there.</summary>
    [Fact]
    public void AnExplicitInterfaceImplementation_IsNotAnOverload()
    {
        var result = Compile("""
            using System.Collections;
            using System.Collections.Generic;

            public sealed class Bag : IEnumerable<int>
            {
                private readonly List<int> _items = [];
                public IEnumerator<int> GetEnumerator() => _items.GetEnumerator();
                IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            }
            """, "Bag");

        result.Errors.Should().NotContain(error => error.Code == "EQ1007");
    }

    /// <summary>A component's server-only method never reaches its twin, so it names nothing there.</summary>
    [Fact]
    public void AComponentsServerOnlyMethod_TakesNoName()
    {
        var result = Compile("""
            using eQuantic.UI.Primitives;

            [Page("/probe")]
            public sealed class Page : StatelessComponent
            {
                [ServerOnly] private string Label(System.Net.Http.HttpClient client) => "";
                private string Label(string s) => s;
                public override VisualNode Build(ComponentContext context) => new Text(Label("a"), TypeRole.BodyM);
            }
            """, "Page");

        result.Errors.Should().NotContain(error => error.Code == "EQ1007");
    }

    /// <summary>A static class nested in a component is embedded in its module, a class of its own.</summary>
    [Fact]
    public void AStaticClassNestedInAComponent_IsCheckedToo()
    {
        var result = Compile("""
            using eQuantic.UI.Primitives;

            [Page("/probe")]
            public sealed class Page : StatelessComponent
            {
                private static class Copy
                {
                    public static string Title(int count) => count.ToString();
                    public static string Title(string name) => name;
                }

                public override VisualNode Build(ComponentContext context) => new Text(Copy.Title(1), TypeRole.BodyM);
            }
            """, "Page");

        result.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Code = "EQ1007", Line = 9 });
    }

    /// <summary>
    /// A name a BASE already takes is taken along the chain too: <c>Derived extends Base</c> has one
    /// member per name, so a derived <c>Format(int)</c> beside the base's <c>Format(string)</c>
    /// answers every call on both, and <c>derived.Format("x")</c> reaches the integer body. The error
    /// is the derived one's, and names what it takes over.
    /// </summary>
    [Fact]
    public void AnOverloadOfAnInheritedMethod_IsRefused()
    {
        var results = new ComponentCompiler().CompileSource("""
            public class Base
            {
                public virtual string Format(string s) => s;
            }

            public sealed class Derived : Base
            {
                public string Format(int n) => n.ToString();
            }
            """, "Probe.cs");

        results.Single(result => result.ComponentName == "Base").Errors.Should().NotContain(error => error.Code == "EQ1007");
        var error = results.Single(result => result.ComponentName == "Derived").Errors.Should().ContainSingle().Subject;
        error.Code.Should().Be("EQ1007");
        error.Message.Should().Contain("Derived.Format(int)").And.Contain("Base.Format(string)");
    }

    [Fact]
    public void AStaticThatTakesTheNameOfAnInheritedStatic_IsRefused()
    {
        var results = new ComponentCompiler().CompileSource("""
            public class Base
            {
                public static int Size(int x) => x;
            }

            public class Derived : Base
            {
                public static int Size(string s) => s.Length;
            }
            """, "Probe.cs");

        results.Single(result => result.ComponentName == "Derived").Errors.Should().ContainSingle()
            .Which.Code.Should().Be("EQ1007", "a class's statics are inherited along the chain in JavaScript too");
    }

    /// <summary>
    /// A component is the declaration it was parsed from, not the first class of its name in its file:
    /// an empty <c>A.Probe</c> ahead of a page <c>B.Probe</c> stood in for it, and the page's own
    /// overloads were never checked.
    /// </summary>
    [Fact]
    public void AComponentIsCheckedAsItsOwnDeclaration_NotAsTheFirstOfItsName()
    {
        var results = new ComponentCompiler().CompileSource("""
            using eQuantic.UI.Primitives;

            namespace A
            {
                public class Probe { }
            }

            namespace B
            {
                [Page("/probe")]
                public sealed class Probe : StatelessComponent
                {
                    private string Label(int n) => n.ToString();
                    private string Label(string s) => s;
                    public override VisualNode Build(ComponentContext context) => new Text(Label("a"), TypeRole.BodyM);
                }
            }
            """, "Probe.cs");

        results.SelectMany(result => result.Errors).Should()
            .Contain(error => error.Code == "EQ1007" && error.Line == 14, "the page's second Label is on line 14");
    }

    /// <summary>
    /// A server-only method is left out of a COMPONENT's twin, and only there: a plain class's twin
    /// writes every method, server-only ones too, so a derived <c>Foo(int)</c> takes over the base's
    /// server-only <c>Foo(string)</c> in JavaScript as it would any other.
    /// </summary>
    [Fact]
    public void AnInheritedServerOnlyMethod_TakesItsName_InAPlainClassChain()
    {
        var results = new ComponentCompiler().CompileSource("""
            using eQuantic.UI.Primitives;

            public class Base
            {
                [ServerOnly] public string Foo(string s) => s;
            }

            public sealed class Derived : Base
            {
                public string Foo(int n) => n.ToString();
            }
            """, "Probe.cs");

        results.Single(result => result.ComponentName == "Derived").Errors.Should().ContainSingle()
            .Which.Code.Should().Be("EQ1007");
    }

    /// <summary>In a component's chain a base's server-only method reaches no twin, so it takes no name.</summary>
    [Fact]
    public void AnInheritedServerOnlyMethod_TakesNoName_InAComponentChain()
    {
        var results = new ComponentCompiler().CompileSource("""
            using eQuantic.UI.Primitives;

            public abstract class ScreenBase : StatelessComponent
            {
                [ServerOnly] protected string Label(System.Net.Http.HttpClient client) => "";
            }

            [Page("/probe")]
            public sealed class Screen : ScreenBase
            {
                private string Label(string s) => s;
                public override VisualNode Build(ComponentContext context) => new Text(Label("a"), TypeRole.BodyM);
            }
            """, "Probe.cs");

        results.SelectMany(result => result.Errors).Should().NotContain(error => error.Code == "EQ1007");
    }

    /// <summary>An override is the method it overrides, and replacing it is what it is for.</summary>
    [Fact]
    public void AnOverride_IsNotASecondMethod()
    {
        var results = new ComponentCompiler().CompileSource("""
            public abstract class Base
            {
                public abstract string Format(string s);
                public virtual string Describe() => "base";
            }

            public sealed class Derived : Base
            {
                public override string Format(string s) => s.ToUpperInvariant();
                public override string Describe() => "derived";
            }
            """, "Probe.cs");

        results.SelectMany(result => result.Errors).Should().NotContain(error => error.Code == "EQ1007");
    }

    /// <summary>
    /// Every member the twin holds on each instance under an indexer's names (a field, a property, an
    /// event, a primary constructor's parameter, a record's positional property) lands on the
    /// indexer's method and hides it: <c>new Box(4)[1]</c> answered the text of a function, and
    /// <c>g[0] = 5</c> beside a <c>SetItem</c> property threw. Each is refused, naming both members
    /// and the one to rename. An <c>[IndexerName]</c> lets C# take the <c>Item</c> name for another
    /// member, and the twin's indexer is <c>item</c> whatever C# calls it.
    /// </summary>
    public static TheoryData<string, string, string> MembersOnAnIndexersNames => new()
    {
        { "Box", "public class Box(int item) { public int this[int i] => i + item; }", "Rename 'Box(item)'" },
        { "Flag", "public class Flag { private readonly int[] _v = new int[2]; public bool SetItem { get; set; } = true; public int this[int i] { get => _v[i]; set => _v[i] = value; } }", "Rename 'SetItem'" },
        { "Cells", "public class Cells { private readonly int[] _v = new int[2]; public int Item = 3; [System.Runtime.CompilerServices.IndexerName(\"Cell\")] public int this[int i] { get => _v[i]; set => _v[i] = value; } }", "Rename 'Item'" },
        { "Ev", "public class Ev { public event System.Action SetItem; private readonly int[] _v = new int[2]; public int this[int i] { get => _v[i]; set => _v[i] = value; } public void Fire() => SetItem?.Invoke(); }", "Rename 'SetItem'" },
        { "RItem", "public record RItem(int Item) { [System.Runtime.CompilerServices.IndexerName(\"Cell\")] public int this[int i] => i + Item; }", "Rename 'RItem(Item)'" },
        { "Method", "public class Method { public int Item(int i) => i; [System.Runtime.CompilerServices.IndexerName(\"Cell\")] public int this[int i] => i; }", "Rename 'Item(int)'" },
    };

    [Theory]
    [MemberData(nameof(MembersOnAnIndexersNames))]
    public void AMemberOnAnIndexersName_IsRefused_NamingWhatToRename(string name, string source, string rename)
    {
        var error = Compile(source, name).Errors.Should().ContainSingle(e => e.Code == "EQ1007").Subject;

        error.Message.Should().Contain("this[int]").And.Contain(rename);
    }

    /// <summary>
    /// An explicit implementation of an interface's indexer takes its interface's name, which no
    /// rename changes, so it is no second indexer: it answers an access through the interface as the
    /// twin's <c>item</c>, and the type's own indexer beside it takes names of its own. It was counted
    /// as a second indexer, and the type's module was not written.
    /// </summary>
    [Fact]
    public void AnExplicitIndexer_IsNotASecondIndexer()
    {
        var results = new ComponentCompiler().CompileSource("""
            public interface IGrid { int this[int i] { get; } }
            public class Grid : IGrid { int IGrid.this[int i] => i * 10; public int this[int i] => i; }
            """, "Probe.cs");

        results.SelectMany(result => result.Errors).Should().NotContain(error => error.Code == "EQ1007");
        results.Single(result => result.ComponentName == "Grid").TypeScript
            .Should().MatchRegex(@"\n\s*item\(i\b").And.Contain("Grid$item(i");
    }

    /// <summary>
    /// What an access through an interface reaches is the twin's <c>item</c>, whatever the type behind
    /// it, so two explicit implementations, or one beside an indexer that answers another interface or a
    /// default the type takes, cannot both be reached: refused.
    /// </summary>
    public static TheoryData<string, string> TwoIndexersOneSlot => new()
    {
        { "Two explicit implementations", """
            public interface IA { int this[int i] { get; } }
            public interface IB { int this[int i] { get; } }
            public class Grid : IA, IB { int IA.this[int i] => 1; int IB.this[int i] => 2; }
            """ },
        { "an explicit one beside one that answers another interface", """
            public interface IA { int this[int i] { get; } }
            public interface IB { int this[int i] { get; } }
            public class Grid : IA, IB { int IA.this[int i] => 1; public int this[int i] => 2; }
            """ },
        { "an explicit one beside a default", """
            public interface IA { int this[int i] { get; } }
            public interface IB { int this[string s] => 2; }
            public class Grid : IA, IB { int IA.this[int i] => 1; }
            """ },
    };

    [Theory]
    [MemberData(nameof(TwoIndexersOneSlot))]
    public void TwoIndexersAnInterfaceReaches_AreRefused(string why, string source)
    {
        var results = new ComponentCompiler().CompileSource(source, "Probe.cs");

        results.Single(result => result.ComponentName == "Grid").Errors
            .Should().Contain(error => error.Code == "EQ1007", why);
    }
    /// <summary>
    /// A record's or a struct's instance members that differ only in the case of their first letter
    /// land on one member of the twin, which holds its state on each instance and its methods and
    /// computed properties on its prototype. Two states shared one slot (`struct S(int x) { X = x * 2 }`
    /// answered "6|6" for "3|6"), and a state beside an accessor of its name was written over the
    /// getter, which threw at `new`.
    /// </summary>
    public static TheoryData<string, string, string> StateOnOneName => new()
    {
        { "S", "public struct S(int x) { public int X { get; } = x * 2; public int Raw() => x; }", "'S.X' lowers to `x`" },
        { "R", "public record R(int Count) { private readonly int count = Count * 10; public int Scaled => count; }", "'R.count' lowers to `count`" },
        { "Money", "public readonly struct Money(decimal amount) { public decimal Amount => amount; }", "'Money.Amount' lowers to `amount`" },
        { "Temperature", "public struct Temperature { private double celsius; public double Celsius => celsius; public Temperature(double c) { celsius = c; } }", "'Temperature.Celsius' lowers to `celsius`" },
        { "Tally", "public record Tally { public int Total { get; init; } public int total() => Total; }", "'Tally.total()' lowers to `total`" },
    };

    [Theory]
    [MemberData(nameof(StateOnOneName))]
    public void ARecordsOrAStructsMembersOnOneName_AreRefused(string name, string source, string named)
    {
        var errors = Compile(source, name).Errors;

        errors.Should().ContainSingle().Which.Should().Match<CompilationError>(error =>
            error.Code == "EQ1007" && error.Message.Contains(named) && error.Message.Contains("Rename one of them"));
    }

    /// <summary>
    /// A plain class's primary constructor parameter that a member reads is held on each instance under
    /// its name, as a struct's is (#583), so one that lands on another member's name would hide it, and
    /// a getter or a method of that name would answer the parameter: refused, naming both.
    /// </summary>
    [Theory]
    [InlineData("Greeter", "public class Greeter(string name) { public string Name => name.ToUpper(); }", "'Greeter(name)' lowers to `name`")]
    [InlineData("Clock", "public class Clock(int tick) { public int Tick() => tick + 1; }", "'Clock(tick)' lowers to `tick`")]
    public void AClassesHeldParameterOnAMembersName_IsRefused(string name, string source, string named)
    {
        var errors = Compile(source, name).Errors;

        errors.Should().ContainSingle().Which.Should().Match<CompilationError>(error =>
            error.Code == "EQ1007" && error.Message.Contains(named) && error.Message.Contains("Rename the parameter"));
    }

    /// <summary>
    /// What is one member is not refused: a positional parameter the body redeclares under its own name
    /// (#546), a static beside an instance member of the name, and a struct's primary constructor
    /// parameter read only by an initializer, which no instance holds. A plain class's backing field
    /// beside its property answers right, its field being a class field that shadows the getter, and is
    /// left as it is.
    /// </summary>
    [Theory]
    [InlineData("Box", "public sealed record Box(int X, int Y) { public int X { get; set; } = X; }")]
    [InlineData("Units", "public record struct Units(int Count) { public static int count = 3; }")]
    [InlineData("Plain", "public sealed class Plain { private int count = 2; public int Count => count; }")]
    [InlineData("Point", "public readonly struct Point(int x, int y) { public int X { get; } = x; public int Y { get; } = y; }")]
    [InlineData("Spot", "public class Spot(int x) { public int X { get; } = x; }")]
    [InlineData("Badge", "public class Badge(string label) { public string Name => label; }")]
    public void OneMemberOnItsName_IsNotRefused(string name, string source) =>
        Compile(source, name).Errors.Where(error => error.Code == "EQ1007").Should().BeEmpty();
}
