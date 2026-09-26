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
}
