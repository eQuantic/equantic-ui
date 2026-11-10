using eQuantic.UI.Compiler;
using FluentAssertions;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A component's own member named <c>Component</c> is that member (#1299): its constructor wrote
/// <c>this._component</c>, a field of the State classes the runtime no longer has, while the props, the
/// adoption of a new configuration and the hydration go by <c>component</c>.
/// </summary>
public class AComponentsMemberNamedComponentTests
{
    [Fact]
    public void AStatefulComponentsMemberNamedComponent_IsWrittenAsThatMember()
    {
        var result = new ComponentCompiler().CompileSource(
            "using eQuantic.UI.Primitives; using eQuantic.UI.Components; namespace App; "
            + "public sealed class Card : StatefulComponent { "
            + "  public Card(string component) { Component = component; } "
            + "  public string Component { get; set; } "
            + "  public override VisualNode Build(ComponentContext context) => new Text(Component); }").Single();

        result.Success.Should().BeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
        result.TypeScript.Should().Contain("this.component = component").And.NotContain("_component");
    }
}
