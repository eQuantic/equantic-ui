using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// Every kind of class declares its type parameters in its module, in TypeScript, and none in plain JavaScript (#1845).
/// <para>
/// A plain generic class, a generic record and a generic struct named <c>T</c> in their members while their class
/// declared none, which the runtime's <c>tsc</c> refuses (<c>Cannot find name 'T'</c>). That kept a generic class out
/// of the runtime-provided library, where #1563 meets it. Only the component path wrote the list. The plain class's
/// writer took none, and the record's wrote its header by hand. Every kind now reads one list
/// (<c>TwinTypeParameters</c>). The class writer writes it only in TypeScript, as it writes <c>abstract</c>: in plain
/// JavaScript, <c>class Box&lt;T&gt; {</c> is a parse error that costs the whole module.
/// </para>
/// </summary>
public class GenericTypeParametersTests
{
    private const string Source = """
        using System;
        using eQuantic.UI.Components;
        using eQuantic.UI.Primitives;

        namespace Generic;

        public sealed class Factory<T>
        {
            private readonly Func<T> _create;
            public Factory(Func<T> create) { _create = create; }
            public T Build() => _create();
        }

        public sealed record Pair<TKey>(TKey Key, int Rank);

        public struct Slot<TValue>
        {
            public TValue Value;
            public Slot(TValue value) { Value = value; }
        }

        public sealed class Listing<TItem> : StatelessComponent
        {
            public TItem? First { get; init; }
            public override VisualNode Build(ComponentContext context) => new Text("" + First, TypeRole.BodyM);
        }
        """;

    private static string Module(string name, bool typeAnnotations) =>
        new ComponentCompiler { TypeAnnotations = typeAnnotations }.CompileSource(Source, "Generic.cs")
            .First(result => result.ComponentName == name).TypeScript;

    [Theory]
    [InlineData("Factory", "T")]
    [InlineData("Pair", "TKey")]
    [InlineData("Slot", "TValue")]
    [InlineData("Listing", "TItem")]
    public void AGenericClass_DeclaresItsTypeParameters_InTypeScript(string name, string parameter) =>
        Assert.Contains($"class {name}<{parameter}>", Module(name, typeAnnotations: true), StringComparison.Ordinal);

    [Theory]
    [InlineData("Factory")]
    [InlineData("Pair")]
    [InlineData("Slot")]
    [InlineData("Listing")]
    public void AGenericClass_DeclaresNone_InPlainJavaScript(string name)
    {
        var module = Module(name, typeAnnotations: false);
        Assert.Contains($"class {name} ", module, StringComparison.Ordinal);
        Assert.DoesNotContain($"class {name}<", module, StringComparison.Ordinal);
    }

    /// <summary>A record's members name its own type by its closure, and a static one, which TypeScript forbids to read
    /// a class's type parameters, by an open one.</summary>
    [Fact]
    public void AGenericRecord_NamesItsOwnTypeByItsClosure()
    {
        var module = Module("Slot", typeAnnotations: true);
        Assert.Contains("with(patch: any): Slot<TValue>", module, StringComparison.Ordinal);
        Assert.Contains("$clone(): Slot<TValue>", module, StringComparison.Ordinal);
        Assert.Contains("): Slot<any>", module, StringComparison.Ordinal);
    }
}
