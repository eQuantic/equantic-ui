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

    /// <summary>A class's, a static class's and a record's server-only method never reaches their twin
    /// either (#816), so it names nothing there: it was written, and refused as an overload.</summary>
    [Theory]
    [InlineData("public sealed class Bag", "")]
    [InlineData("public static class Bag", "static ")]
    [InlineData("public sealed record Bag(int N)", "")]
    public void AModulesServerOnlyMethod_TakesNoName(string declaration, string modifier)
    {
        var result = Compile($$"""
            using eQuantic.UI.Primitives;

            {{declaration}}
            {
                [ServerOnly] public {{modifier}}string Label(System.Net.Http.HttpClient client) => "";
                public {{modifier}}string Label(string s) => s;
            }
            """, "Bag");

        result.Errors.Should().NotContain(error => error.Code == "EQ1007");
        result.TypeScript.Should().Contain("label(", Exactly.Once(), "the twin holds the method that crosses, and only it");
    }

    /// <summary>A static class nested in a component is a module of its own (#654), checked as one, and
    /// refused once, where it is written: checked from its owner as well, it was refused twice.</summary>
    [Fact]
    public void AStaticClassNestedInAComponent_IsCheckedToo()
    {
        var results = new ComponentCompiler().CompileSource("""
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
            """, "Probe.cs");

        results.Single(result => result.ComponentName == "Page$Copy").Errors.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Code = "EQ1007", Line = 9 });
        results.Single(result => result.ComponentName == "Page").Errors.Should().BeEmpty();
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
    /// A server-only method is left out of every twin, a plain class's as much as a component's (#816),
    /// so in a plain class's chain too it takes no name, and a derived <c>Foo(int)</c> beside the base's
    /// server-only <c>Foo(string)</c> is the one <c>foo</c> there is. A plain class's twin wrote it, and
    /// the two were refused as overloads.
    /// </summary>
    [Fact]
    public void AnInheritedServerOnlyMethod_TakesNoName_InAPlainClassChain()
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

        results.Single(result => result.ComponentName == "Derived").Errors.Should().BeEmpty();
        results.Single(result => result.ComponentName == "Base").TypeScript.Should().NotContain("foo(",
            "the base's twin holds no server-only method");
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

    /// <summary>
    /// A method that HIDES an inherited one, with <c>new</c> or with the same signature and no
    /// <c>override</c>, holds a name of its own on its twin (#563): it takes no name a base holds, so it
    /// is not refused, and a call bound to the hidden one keeps reaching it. It was refused, legal C# and
    /// all, where the base was the app's, and where it was not, it answered for the hidden one.
    /// </summary>
    [Fact]
    public void AMethodThatHidesAnInheritedOne_HoldsANameOfItsOwn()
    {
        var results = new ComponentCompiler().CompileSource("""
            public class Base
            {
                public virtual string Name() => "base";
            }

            public class Hiding : Base
            {
                public new string Name() => "hiding";
                public string Both() => Name() + base.Name();
            }

            public class Again : Hiding
            {
                public new virtual string Name() => "again";
            }

            public sealed class Last : Again
            {
                public override string Name() => "last";
            }
            """, "Probe.cs");

        results.SelectMany(result => result.Errors).Should().NotContain(error => error.Code == "EQ1007");
        var hiding = results.Single(result => result.ComponentName == "Hiding").TypeScript;
        hiding.Should().Contain("name$1()").And.Contain("this.name$1()").And.Contain("super.name()");
        results.Single(result => result.ComponentName == "Again").TypeScript.Should().Contain("name$2()");
        results.Single(result => result.ComponentName == "Last").TypeScript.Should().Contain("name$2()",
            "an override fills the slot of the method it overrides, whatever that one is named");
    }

    /// <summary>
    /// A method that hides an inherited one and answers an interface is refused: a call through the
    /// interface reaches the member by the interface's name, which the hidden one holds on the twin, so it
    /// would reach the hidden one where C# reaches the one that hides it.
    /// </summary>
    [Fact]
    public void AMethodThatHidesOne_AndAnswersAnInterface_IsRefused()
    {
        var results = new ComponentCompiler().CompileSource("""
            public interface INamed { string Name(); }

            public class Base
            {
                public string Name() => "base";
            }

            public sealed class Hiding : Base, INamed
            {
                public new string Name() => "hiding";
            }
            """, "Probe.cs");

        var error = results.Single(result => result.ComponentName == "Hiding").Errors.Should().ContainSingle().Subject;
        error.Code.Should().Be("EQ1007");
        error.Message.Should().Contain("'Hiding.Name' answers 'INamed.Name'").And.Contain("`name$1()`");
    }

    /// <summary>
    /// A method beside an explicit implementation of its name holds a name of its own (Copilot's review of
    /// #708): the explicit one holds the interface member's name, which a call through the interface
    /// reaches, and a call through the class reaches the method by its own. Both were written under one
    /// name, the later replacing the other. One that answers another interface is refused, since a call
    /// through that interface would reach the explicit one.
    /// </summary>
    [Fact]
    public void AMethodBesideAnExplicitImplementation_HoldsANameOfItsOwn()
    {
        var results = new ComponentCompiler().CompileSource("""
            public interface INamedThing { string Name(); }
            public interface ITitled { string Name(); }

            public sealed class Labeled : INamedThing
            {
                public string Name() => "own";
                string INamedThing.Name() => "interface";
            }

            public sealed class Answering : INamedThing, ITitled
            {
                public string Name() => "own";
                string INamedThing.Name() => "interface";
            }
            """, "Probe.cs");

        var labeled = results.Single(result => result.ComponentName == "Labeled");
        labeled.Errors.Should().BeEmpty();
        labeled.TypeScript.Should().Contain("name$1()").And.Contain("name()");
        var error = results.Single(result => result.ComponentName == "Answering").Errors.Should().ContainSingle().Subject;
        error.Code.Should().Be("EQ1007");
        error.Message.Should().Contain("'Answering.Name' answers 'ITitled.Name'").And.Contain("explicit implementation of 'INamedThing.Name'");
    }

    private const string Sequences = """
        using System.Collections;
        using System.Collections.Generic;
        public class Both : IEnumerable<int>, IEnumerable<string>
        {
            IEnumerator<int> IEnumerable<int>.GetEnumerator() { yield return 1; }
            IEnumerator<string> IEnumerable<string>.GetEnumerator() { yield return "a"; }
            IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<int>)this).GetEnumerator();
        }
        public class Ints : IEnumerable<int>
        {
            public IEnumerator<int> GetEnumerator() { yield return 7; }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
        public class IntsAndNames : Ints, IEnumerable<string>
        {
            IEnumerator<string> IEnumerable<string>.GetEnumerator() { yield return "c"; }
        }
        public class TwoWalks : IEnumerator<int>, IEnumerator<string>
        {
            int IEnumerator<int>.Current => 1;
            string IEnumerator<string>.Current => "a";
            object IEnumerator.Current => 1;
            public bool MoveNext() => false;
            public void Reset() { }
            public void Dispose() { }
        }
        public class Odd : IEnumerable<int>
        {
            public IEnumerator<int> GetEnumerator() { yield return 1; }
            IEnumerator IEnumerable.GetEnumerator() { yield return "x"; }
        }
        public class OddCurrent : IEnumerator<int>
        {
            private int at;
            public int Current => at;
            object IEnumerator.Current => "other";
            public bool MoveNext() => ++at <= 2;
            public void Reset() { }
            public void Dispose() { }
        }
        public class OddGenericCurrent : IEnumerator<int>
        {
            private int at;
            public int Current => at;
            int IEnumerator<int>.Current => at * 10;
            object IEnumerator.Current => Current;
            public bool MoveNext() => ++at <= 2;
            public void Reset() { }
            public void Dispose() { }
        }
        public class Alike : IEnumerable<string>, IEnumerator<string>
        {
            private int at;
            IEnumerator<string> IEnumerable<string>.GetEnumerator() => this;
            IEnumerator IEnumerable.GetEnumerator() { return ((IEnumerable<string>)this).GetEnumerator(); }
            public string Current => "s" + at;
            string IEnumerator<string>.Current { get { return Current; } }
            object IEnumerator.Current => ((IEnumerator<string>)this).Current;
            public bool MoveNext() => ++at <= 2;
            public void Reset() { }
            public void Dispose() { }
        }
        """;

    /// <summary>
    /// A sequence's twin has ONE iteration, which LINQ, a spread, <c>string.Join</c> and a <c>foreach</c>
    /// over each interface walk, and ONE <c>current</c>, while C# lets a type implement
    /// <c>IEnumerable&lt;T&gt;</c> over two types, and its members answer apart: the non-generic
    /// <c>GetEnumerator()</c> and a <c>Current</c> are held as one with the member the twin keeps. Each walked,
    /// or read, the other's answer in the browser (Copilot's second review of #708), so each is refused
    /// (EQ1007), where a member that hands back the other's answer is held as one (<c>Alike</c>).
    /// </summary>
    [Theory]
    [InlineData("Both", "implements 'IEnumerable<int>' and 'IEnumerable<string>'")]
    [InlineData("IntsAndNames", "and its twin has one iteration")]
    [InlineData("TwoWalks", "implements 'IEnumerator<int>' and 'IEnumerator<string>'")]
    [InlineData("Odd", "'IEnumerable.GetEnumerator()' does not return it through 'GetEnumerator()'")]
    [InlineData("OddCurrent", "'IEnumerator.Current' does not read it through 'Current'")]
    [InlineData("OddGenericCurrent", "'IEnumerator<int>.Current' does not read it through 'Current'")]
    public void ASequenceItsTwinWouldWalkOtherwise_IsRefused(string name, string said)
    {
        var results = new ComponentCompiler().CompileSource(Sequences, "Probe.cs");

        var error = results.Single(result => result.ComponentName == name).Errors.Should().ContainSingle().Subject;
        error.Code.Should().Be("EQ1007");
        error.Message.Should().Contain(said);
        results.Where(result => result.ComponentName is "Ints" or "Alike").SelectMany(result => result.Errors)
            .Should().BeEmpty("a member that hands back what the other answers answers alike");
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
    /// Two parameters of a primary constructor that land on one name: the twin's constructor names each
    /// one camelCased, and a function cannot take two parameters of one name, so the module did not load,
    /// whether a member read both, one or neither (found by Copilot's review of #621).
    /// </summary>
    [Theory]
    [InlineData("Both", "public class Both(int x, int X) { public int Sum() => x + X; }")]
    [InlineData("One", "public class One(int x, int X) { public int Get() => x; }")]
    [InlineData("Neither", "public class Neither(int x, int X) { }")]
    [InlineData("Pair", "public struct Pair(int x, int X) { public int Get() => x; }")]
    [InlineData("Twins", "public record Twins(int x, int X);")]
    public void TwoPrimaryParametersOnOneName_AreRefused(string name, string source)
    {
        var errors = Compile(source, name).Errors;

        errors.Should().ContainSingle().Which.Should().Match<CompilationError>(error =>
            error.Code == "EQ1007" && error.Message.Contains($"'{name}(X)' lowers to `x`")
            && error.Message.Contains($"'{name}(x)'") && error.Message.Contains("cannot take two parameters of one name"));
    }

    /// <summary>
    /// What is one member is not refused: a positional parameter the body redeclares under its own name
    /// (#546), a static beside an instance member of the name, a struct's primary constructor parameter
    /// read only by an initializer, which no instance holds, and the two accessors of one property. A
    /// plain class's field beside a member of its name moves to a slot of its own (`count$`, #396). An
    /// override of object's <c>ToString()</c>, <c>GetHashCode()</c> or <c>Equals(object)</c> is written
    /// in place of the twin's own (#1146), a typed <c>Equals</c> holds a name of its own (#947), and a
    /// static <c>ToString</c> lives on the class, apart from the instance's text. The two halves of a
    /// partial property or a partial event are one member, as a partial method's are.
    /// </summary>
    [Theory]
    [InlineData("Box", "public sealed record Box(int X, int Y) { public int X { get; set; } = X; }")]
    [InlineData("Units", "public record struct Units(int Count) { public static int count = 3; }")]
    [InlineData("Plain", "public sealed class Plain { private int count = 2; public int Count => count; }")]
    [InlineData("Point", "public readonly struct Point(int x, int y) { public int X { get; } = x; public int Y { get; } = y; }")]
    [InlineData("Spot", "public class Spot(int x) { public int X { get; } = x; }")]
    [InlineData("Badge", "public class Badge(string label) { public string Name => label; }")]
    [InlineData("Gauge", "public sealed class Gauge { private int level; public int Level { get => level; set => level = value * 2; } }")]
    [InlineData("Person", "public sealed class Person { private string name = \"x\"; public string Name { get; set; } = \"y\"; }")]
    [InlineData("Tally", "public sealed class Tally { public static int Count => 1; public int count; }")]
    [InlineData("Cash", "public sealed record Cash(int Cents) { public override string ToString() => \"$\" + Cents; public override int GetHashCode() => Cents; }")]
    [InlineData("Pin", "public struct Pin { public int X; public override bool Equals(object? o) => o is Pin p && p.X == X; public override int GetHashCode() => X; public override string ToString() => \"pin\"; }")]
    [InlineData("Key", "public sealed class Key { public int X; public override bool Equals(object? o) => o is Key k && k.X == X; public override int GetHashCode() => X; public override string ToString() => \"key\"; }")]
    [InlineData("Same", "public sealed record Same(int V) { public bool Equals(Same? other) => other?.V == V; public override int GetHashCode() => V; }")]
    [InlineData("Fmt", "public sealed record Fmt(int V) { public static string ToString(int v) => v.ToString(); }")]
    [InlineData("Halves", "public sealed partial class Halves { public partial int Level { get; set; } public partial int Level { get => field; set => field = value * 2; } public partial event System.Action? Moved; public partial event System.Action? Moved { add { } remove { } } }")]
    [InlineData("Shown", "using eQuantic.UI.Primitives; public sealed class Shown : StatelessComponent { public override string ToString() => \"Shown\"; public override VisualNode Build(ComponentContext context) => new Text(\"\", TypeRole.BodyM); }")]
    [InlineData("Consent", "using eQuantic.UI.Primitives; public sealed class Consent(string? policyHref = null) : StatefulComponent { public string? PolicyHref { get; init; } = policyHref; public override VisualNode Build(ComponentContext context) => new Text(PolicyHref ?? \"\", TypeRole.BodyM); }")]
    [InlineData("Typed", "public record Root(int X) { public virtual bool Equals(int x) => X == x; } public sealed record Typed(int X, int Y) : Root(X) { public override bool Equals(int x) => Y == x; }")]
    public void OneMemberOnItsName_IsNotRefused(string name, string source) =>
        Compile(source, name).Errors.Where(error => error.Code == "EQ1007").Should().BeEmpty();

    /// <summary>
    /// Every pair of members of one type that lands on one key of its twin, the same lowered name and the
    /// same static-ness, whatever kind each member is (#1146). Each passed the build, and the browser kept
    /// one of them: a static field took the place of a static accessor or method of its name when the
    /// class was defined (<c>Seq.Next()</c> was "not a function"), a value held on each instance hid an
    /// accessor or a method of its name, two statics of one name answered for each other, and so did two
    /// <c>operator +</c>, which the twin names by their symbol alone.
    /// </summary>
    public static TheoryData<string, string, string, string> MembersOnOneKey => new()
    {
        { "Counter", "public static class Counter { private static int count; public static int Count { get => count; set => count = value * 2; } }", "Counter.Count", "Counter.count" },
        { "Seq", "public static class Seq { private static int next; public static int Next() => ++next; }", "Seq.Next()", "Seq.next" },
        { "Limits", "public static class Limits { public const int Max = 3; public static int max => 4; }", "Limits.max", "Limits.Max" },
        { "Ids", "public sealed class Ids { private static int next; public static int Next() => ++next; }", "Ids.Next()", "Ids.next" },
        { "Cfg", "public sealed class Cfg { private static int count; public static int Count { get; set; } = 5; }", "Cfg.Count", "Cfg.count" },
        { "Shape", "public sealed class Shape { public int Total => 3; public int total() => 4; }", "Shape.total()", "Shape.Total" },
        { "Twins", "public sealed class Twins { public int Total { get; set; } = 1; public int total { get; set; } = 2; }", "Twins.total", "Twins.Total" },
        { "Named", "public sealed class Named { public string Name { get; set; } = \"a\"; public string name() => Name; }", "Named.name()", "Named.Name" },
        { "Clicker", "public sealed class Clicker { public event System.Action? Changed; public void changed() => Changed?.Invoke(); }", "Clicker.changed()", "Clicker.Changed" },
        { "Sizes", "public static class Sizes { extension(string s) { public int Size => s.Length; } public static int Size(this System.Collections.Generic.List<int> l) => l.Count; }", "Sizes.Size(System.Collections.Generic.List<int>)", "Sizes.extension(string).Size" },
        { "Items", "public static class Items { extension(string s) { public char this[int i] => s[i]; } public static int Item(int x) => x; }", "Items.Item(int)", "Items.extension(string).this[int]" },
        { "Texts", "public static class Texts { extension(string) { public static string Blank => \"\"; } public static string blank() => \"b\"; }", "Texts.blank()", "Texts.extension(string).Blank" },
        { "Money", "public sealed record Money(int Cents) { public static implicit operator Money(int cents) => new(cents); public static Money FromInt(int cents) => new(cents * 100); }", "Money.FromInt(int)", "Money.implicit operator Money(int)" },
        { "Sum", "public sealed record Sum(int V) { public static Sum operator +(Sum a, Sum b) => new(a.V + b.V); public static Sum operator +(Sum a, int b) => new(a.V + b); }", "Sum.operator +(Sum, int)", "Sum.operator +(Sum, Sum)" },
    };

    [Theory]
    [MemberData(nameof(MembersOnOneKey))]
    public void TwoMembersOfAnyKindOnOneKey_AreRefused(string name, string source, string later, string earlier)
    {
        var error = Compile(source, name).Errors.Should().ContainSingle().Subject;

        error.Code.Should().Be("EQ1007");
        error.Message.Should().StartWith($"'{later}' lowers to").And.Contain($"and so does '{earlier}' (line 1)");
    }

    /// <summary>
    /// A component's members land on one key the same way, stateless and stateful alike. Its fields keep
    /// the names the server hydrates them under, so none moves as a plain class's does (#662): a field
    /// beside its property hid the getter (<c>Label =&gt; label.ToUpper()</c> answered "x"), and a
    /// setter never ran.
    /// </summary>
    [Theory]
    [InlineData("StatelessComponent", "private readonly string label = \"x\"; public string Label => label.ToUpper();", "Page.Label", "Page.label")]
    [InlineData("StatefulComponent", "private int count; public int Count { get => count; set => count = value * 2; }", "Page.Count", "Page.count")]
    [InlineData("StatelessComponent", "public string Title => \"t\"; private string title() => \"x\";", "Page.title()", "Page.Title")]
    [InlineData("StatelessComponent", "private static int count; public static int Count => count;", "Page.Count", "Page.count")]
    [InlineData("StatefulComponent", "public event System.Action? Changed; private bool changed;", "Page.changed", "Page.Changed")]
    public void AComponentsMembersOnOneKey_AreRefused(string kind, string members, string later, string earlier)
    {
        var error = Compile($$"""
            using eQuantic.UI.Primitives;

            [Page("/probe")]
            public sealed class Page : {{kind}}
            {
                {{members}}
                public override VisualNode Build(ComponentContext context) => new Text("", TypeRole.BodyM);
            }
            """, "Page").Errors.Should().ContainSingle().Subject;

        error.Should().BeEquivalentTo(new { Code = "EQ1007", Line = 6 });
        error.Message.Should().StartWith($"'{later}' lowers to").And.Contain($"and so does '{earlier}' (line 6)");
    }

    /// <summary>A static class nested in a component is a module of its own, and holds one member per key
    /// too.</summary>
    [Fact]
    public void AStaticClassNestedInAComponent_HoldsOneMemberPerKey()
    {
        var result = Compile("""
            using eQuantic.UI.Primitives;

            [Page("/probe")]
            public sealed class Page : StatelessComponent
            {
                private static class Copy
                {
                    private static int count;
                    public static int Count() => ++count;
                }

                public override VisualNode Build(ComponentContext context) => new Text(Copy.Count().ToString(), TypeRole.BodyM);
            }
            """, "Page$Copy");

        result.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Code = "EQ1007", Line = 9 });
    }

    /// <summary>A <c>[ServerOnly]</c> static class nested in a component is no module, so nothing of it
    /// reaches a twin, and its owner is refused nothing for it: checked from its owner, its backing-field
    /// idiom was refused.</summary>
    [Fact]
    public void AServerOnlyClassNestedInAComponent_IsRefusedNowhere()
    {
        var results = new ComponentCompiler().CompileSource("""
            using eQuantic.UI.Primitives;

            [Page("/probe")]
            public sealed class Page : StatelessComponent
            {
                [ServerOnly] private static class Backend
                {
                    private static int count;
                    public static int Count => count;
                }

                public override VisualNode Build(ComponentContext context) => new Text("", TypeRole.BodyM);
            }
            """, "Probe.cs");

        results.Should().ContainSingle().Which.Errors.Should().BeEmpty();
    }

    /// <summary>
    /// A component's instance holds every parameter of its primary constructor, read or not: its
    /// constructor writes each one it is handed onto the instance, where it hid a method of its name
    /// (<c>Caption()</c> was "not a function" wherever a caption was handed over), and where it filled the
    /// slot of the property of its name before that property's initializer ran, which then ran nowhere
    /// (<c>= policyHref?.Trim()</c> held the untrimmed value).
    /// </summary>
    [Theory]
    [InlineData("Tile(string caption) : StatelessComponent", "public string Caption() => \"x\";", "'Tile.Caption()' lowers to `caption`", "'Tile(caption)' (line 4)")]
    [InlineData("Tile(string? policyHref = null) : StatefulComponent", "public string? PolicyHref { get; init; } = policyHref?.Trim();", "'Tile.PolicyHref' lowers to `policyHref`", "'Tile(policyHref)' (line 4)")]
    public void AComponentsParameter_HoldsItsKey_ReadOrNot(string declaration, string member, string later, string earlier)
    {
        var error = Compile($$"""
            using eQuantic.UI.Primitives;

            [Page("/probe")]
            public sealed class {{declaration}}
            {
                {{member}}
                public override VisualNode Build(ComponentContext context) => new Text("", TypeRole.BodyM);
            }
            """, "Tile").Errors.Should().ContainSingle().Subject;

        error.Should().BeEquivalentTo(new { Code = "EQ1007", Line = 6 });
        error.Message.Should().StartWith(later).And.Contain($"and so does {earlier}");
    }

    /// <summary>
    /// A component's twin is asked the names the runtime asks any value, and an overload there answered
    /// for <c>ToString()</c>: <c>"" + this</c> called <c>ToString(string)</c> with no argument. Its
    /// <c>constructor</c> is EQ2011's (<see cref="ShadowedRuntimeMembersTests"/>).
    /// </summary>
    [Theory]
    [InlineData("public string ToString(string prefix) => prefix;", "'Page.ToString(string)' lowers to `toString`")]
    [InlineData("public int GetHashCode(int salt) => salt;", "'Page.GetHashCode(int)' lowers to `getHashCode`")]
    [InlineData("public string ToJSON() => \"j\";", "'Page.ToJSON()' lowers to `toJSON`")]
    public void AComponentsMemberOnANameTheRuntimeAsks_IsRefused(string members, string named)
    {
        var error = Compile($$"""
            using eQuantic.UI.Primitives;

            [Page("/probe")]
            public sealed class Page : StatelessComponent
            {
                {{members}}
                public override VisualNode Build(ComponentContext context) => new Text("" + this, TypeRole.BodyM);
            }
            """, "Page").Errors.Should().ContainSingle().Subject;

        error.Code.Should().Be("EQ1007");
        error.Message.Should().StartWith(named).And.Contain("a name of its own");
    }

    /// <summary>
    /// A member on a name the twin holds itself: a record's or a struct's <c>with</c>, the names the
    /// runtime and JavaScript ask a value for its equality, its hash, its text and its JSON, a class's
    /// <c>constructor</c> and its static <c>prototype</c> (#1146). Each answered in the twin's place, or
    /// lost to it, or left a module that did not parse. Only an override of object's member stands there.
    /// </summary>
    public static TheoryData<string, string, string> NamesTheTwinHolds => new()
    {
        { "Style", "public sealed record Style(int Size) { public Style With(int size) => this with { Size = size }; }", "'Style.With(int)' lowers to `with`" },
        { "Vec", "public readonly record struct Vec(int X) { public Vec With(int x) => new(x); }", "'Vec.With(int)' lowers to `with`" },
        { "Fit", "public sealed record Fit(int With, int Height);", "'Fit(With)' lowers to `with`" },
        { "Pt", "public sealed record Pt(int X) { public bool equals(Pt other) => X == other.X; }", "'Pt.equals(Pt)' lowers to `equals`" },
        { "Seed", "public sealed record Seed(int V) { public int GetHashCode(int salt) => V ^ salt; }", "'Seed.GetHashCode(int)' lowers to `getHashCode`" },
        { "Temp", "public sealed class Temp { public double C = 1; public string ToString(string unit) => C + unit; }", "'Temp.ToString(string)' lowers to `toString`" },
        { "Tag", "public sealed record Tag(string V) { public string ToString(string prefix) => prefix + V; }", "'Tag.ToString(string)' lowers to `toString`" },
        { "Doc", "public sealed class Doc { public string Title = \"a\"; public string ToJSON() => Title; }", "'Doc.ToJSON()' lowers to `toJSON`" },
        { "Js", "public sealed record Js(int V) { public string ToJSON() => \"x\"; }", "'Js.ToJSON()' lowers to `toJSON`" },
        { "Builder", "public sealed class Builder { public string Constructor() => \"c\"; }", "'Builder.Constructor()' lowers to `constructor`" },
        { "Shaped", "public sealed record Shaped(int Constructor);", "'Shaped(Constructor)' lowers to `constructor`" },
        { "Proto", "public static class Proto { public static int Prototype => 2; }", "'Proto.Prototype' lowers to `static prototype`" },
    };

    [Theory]
    [MemberData(nameof(NamesTheTwinHolds))]
    public void AMemberOnANameTheTwinHolds_IsRefused(string name, string source, string named)
    {
        var error = Compile(source, name).Errors.Should().ContainSingle().Subject;

        error.Code.Should().Be("EQ1007");
        error.Message.Should().StartWith(named).And.Contain("Give").And.Contain("a name of its own");
    }

    /// <summary>
    /// The twin writes an override of object's member in place of its own, and nothing else in its place
    /// (#1146): a struct's <c>Equals(object)</c> was written beside the twin's <c>equals</c>, the browser
    /// keeping the later, and a record's static <c>ToString(int)</c> took the place of its synthesized
    /// text, which the twin then wrote nowhere.
    /// </summary>
    [Fact]
    public void AStructsEqualsOverride_IsItsTwinsOneEquals()
    {
        var result = Compile("public struct Pin { public int X; public override bool Equals(object? o) => o is Pin p && p.X == X; public override int GetHashCode() => X; }", "Pin");

        result.Errors.Should().BeEmpty();
        result.TypeScript.Should().Contain("equals(", Exactly.Once(), "the override is the twin's equals, and nothing beside it");
    }

    /// <summary>A server-only override is no member of the twin, so the twin keeps its own: the record's
    /// synthesized text and its hash. A server-only <c>GetHashCode()</c> took the hash away with nothing in
    /// its place, so the runtime hashed the record by its identity: two equal records hashed apart, and a
    /// dictionary keyed by one never found the other.</summary>
    [Fact]
    public void AServerOnlyOverride_LeavesTheTwinsOwnInPlace()
    {
        var result = Compile("""
            using eQuantic.UI.Primitives;

            public sealed record Note(int V)
            {
                [ServerOnly] public override string ToString() => System.IO.Path.GetTempPath();
                [ServerOnly] public override int GetHashCode() => System.IO.Path.GetTempPath().Length;
            }
            """, "Note");

        result.Errors.Should().BeEmpty();
        result.TypeScript.Should().Contain("$eq.text.record(this, 'Note'")
            .And.Contain("$eq.hash.combine(this.");
    }

    /// <summary>An override of a typed <c>Equals</c> a base record declares fills that one's slot, a name of
    /// its own, and leaves the twin's <c>equals</c> in place: it took it away, and the derived record's
    /// values compared as its base's, its own state unread.</summary>
    [Fact]
    public void ADerivedRecordsTypedEqualsOverride_LeavesItsEqualsInPlace()
    {
        var result = Compile("""
            public record Base(int X) { public virtual bool Equals(int x) => X == x; }
            public sealed record Derived(int X, int Y) : Base(X) { public override bool Equals(int x) => Y == x; }
            """, "Derived");

        result.Errors.Should().BeEmpty();
        result.TypeScript.Should().Contain("$eq.equals(this.y, o.y)", "the derived record compares its own state");
    }

    [Fact]
    public void ARecordsStaticToString_LeavesItsTextInPlace()
    {
        var result = Compile("public sealed record Fmt(int V) { public static string ToString(int v) => v.ToString(); }", "Fmt");

        result.Errors.Should().BeEmpty();
        result.TypeScript.Should().Contain("$eq.text.record(this, 'Fmt'", "the record's text is still the one C# synthesizes")
            .And.Contain("static toString(");
    }
}
