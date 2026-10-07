using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// The twin of a type with a property that keeps a store (#591, #615): the store is state, the
/// property its accessors on the prototype, and the twin answers <c>toJSON</c> with <c>$eq.json</c>, so
/// a server action receives the property by its C# name. <c>JSON.stringify</c> writes an object's own
/// properties, so a store reached the server as <c>$name</c>, which it bound to nothing. A type with no
/// store keeps its own members and no <c>toJSON</c>.
/// </summary>
public class PropertyStoreEmissionTests
{
    private const string Source = """
        public class Shape { public virtual string Kind { get; set; } = "shape"; public int Sides = 3; }
        public class Square : Shape { public override string Kind { get; set; } = "square"; }
        public record Tagged { public int Count { get; set => field = value * 2; } }
        public class Plain { public string Name { get; set; } = "plain"; }
        """;

    private static string Module(string name) =>
        new ComponentCompiler().CompileSource(Source, "Probe.cs").Single(result => result.ComponentName == name).TypeScript;

    [Fact]
    public void AClassWithAStore_HoldsItAsState_ReadsItThroughAccessors_AndAnswersToJson()
    {
        var shape = Module("Shape");

        shape.Should().Contain("$kind!: string;").And.Contain("this.$kind = 'shape';");
        shape.Should().Contain("get kind(): string {").And.Contain("return this.$kind;");
        shape.Should().Contain("set kind(value: string) {").And.Contain("this.$kind = value;");
        shape.Should().Contain("toJSON() {").And.Contain("return $eq.json(this);");
    }

    [Fact]
    public void AnOverrideSharingItsBasesStore_DeclaresItForTypeScriptOnly()
    {
        var square = Module("Square");

        square.Should().Contain("declare $kind: string;", "the base's twin defines the store on the instance")
            .And.NotContain("$kind!:");
        square.Should().Contain("this.$kind = ").And.Contain("get kind(): string {");
    }

    [Fact]
    public void ARecordWithAStore_AnswersToJson_AndATypeWithNone_DoesNot()
    {
        Module("Tagged").Should().Contain("toJSON(): Record<string, unknown> { return $eq.json(this); }")
            .And.Contain("this.$count = value * 2");
        Module("Plain").Should().NotContain("toJSON").And.NotContain("$name");
    }
}
