using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Why <see cref="IconButton"/> has exactly ONE constructor taking a NODE, written down because I
/// changed it and two separate rules said no.
///
/// <para>
/// It began as a real report: the first external consumer of this SDK concluded there was no
/// IconButton and rebuilt it. So I gave the type `Icons` and `IconGlyph` constructors and pointed
/// the factories at them. Both halves were wrong, and neither showed up in 4,300 .NET tests.
/// </para>
///
/// <para>
/// The CONSTRUCTOR half: this component is transpiled, so its twin is generated from its source and
/// JS constructors do not overload. The extra constructors collapsed into one that assigned whatever
/// it was handed straight to `glyph` — the delegation to `new Icon(...)` simply vanished — and every
/// caller passing a glyph died in the browser on `undefined.viewBox`. `Icon` has two constructors
/// and gets away with it only because its twin is HAND-WRITTEN and takes a `string | IconGlyph`
/// union.
/// </para>
///
/// <para>
/// The FACTORY half: a factory mirrors its constructor parameter-for-parameter so named arguments
/// carry between `IconButton(...)` and `new IconButton(...)`. A factory that takes a glyph and wraps
/// it does not mirror anything, which `UiFactoryConformanceTests` says out loud.
/// </para>
///
/// <para>
/// Together those two rules FORCE the factory's parameter type to be the constructor's. So the
/// declarative call is `IconButton(Icon(Icons.Close), "Close")` — one nested factory, the same shape
/// every other node uses — and the discoverability problem the consumer hit is real but is not an
/// API shape problem.
/// </para>
/// </summary>
public class IconButtonSurfaceTests
{
    /// <summary>
    /// The rule, pinned. A transpiled component gets one JS constructor; a second is a delegation
    /// that disappears on the way across and fails in a browser while every .NET test stays green.
    /// </summary>
    [Fact]
    public void ATranspiledComponent_HasExactlyOneConstructor()
    {
        typeof(IconButton).GetConstructors().Should().ContainSingle(
            "the twin is JavaScript — a second constructor is a delegation that disappears");
    }

    [Fact]
    public void ItTakesTheGlyphAsANode()
    {
        typeof(IconButton).GetConstructors().Single()
            .GetParameters()[0].ParameterType.Should().Be<Icon>();
    }

    /// <summary>The label is positional and required — an icon-only button is the one control with
    /// no text of its own to fall back on, and the call site is the only moment to catch that.</summary>
    [Fact]
    public void TheAccessibleName_IsNotOptional()
    {
        var label = typeof(IconButton).GetConstructors().Single().GetParameters()[1];

        label.ParameterType.Should().Be<string>();
        label.IsOptional.Should().BeFalse();
    }

    [Fact]
    public void TheDeclarativeCall_IsOneNestedFactory()
    {
        var button = Components.UI.IconButton(Components.UI.Icon(Icons.Close), "Close");

        button.Label.Should().Be("Close");
        button.Glyph.Should().NotBeNull();
    }
}
