using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The C# the element engine is written in, which E3a carries into the runtime, answers as .NET does.
/// <para>
/// A MEMBER NAMED <c>Component</c> IS THAT MEMBER. eqc read every <c>Component</c> its scope did not
/// bind as <c>this._component</c>, a field of the State classes the runtime no longer has, so the
/// engine's <c>Component = component;</c> wrote a field nothing reads and its property stayed null in
/// the browser, with no error anywhere. An app's own member of that name met the same.
/// </para>
/// </summary>
public class EngineEmissionConformanceTests
{
    private const string Types = """
        using System;
        public abstract class Base
        {
            protected Base(object configuration) { Configuration = configuration; }
            public object Configuration { get; private set; }
        }
        public sealed class Held : Base
        {
            internal Held(string component) : base(component) { Component = component; }
            public string Component { get; private set; }
            public string Shout() => Component.ToUpperInvariant();
            public void Replace(string next) { Component = next; }
        }
        public sealed class Open
        {
            public Open(string component) { Component = component; }
            public string Component { get; set; }
        }
        public sealed class Field
        {
            public string Component = "field";
            public string Read() => Component;
        }
        """;

    private static readonly (string Name, string Statements)[] Cases =
    [
        ("a property set in a constructor that calls its base's", "return new Held(\"x\").Component;"),
        ("the base's own property beside it", "return (string)new Held(\"x\").Configuration;"),
        ("the property read inside the class", "return new Held(\"x\").Shout();"),
        ("the property set again by a method", "var held = new Held(\"x\"); held.Replace(\"y\"); return held.Component;"),
        ("a property with a public setter", "return new Open(\"x\").Component;"),
        ("a field of that name, read inside the class", "return new Field().Read();"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AMemberNamedComponent_IsThatMember(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Types, typeAnnotations, Cases);
}
