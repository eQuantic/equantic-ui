using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The call a reader reaches for has to compile. This one did not, and the evidence was in this
/// repo the whole time: every one of the EIGHT in-tree call sites wrapped a glyph in an
/// <see cref="Icon"/> node to hand it over — <c>new IconButton(new Icon(Icons.X), …)</c> — the
/// `dotnet new` template included.
/// <para>
/// A component whose own library fights its signature is one a first-time consumer concludes is
/// missing. That is not a guess: the IDE building on this SDK reported "there is no IconButton" and
/// went and wrote its own, and four reasons were found for why it could not be found. This is the
/// first of them, and the cheapest.
/// </para>
/// </summary>
public class IconButtonSurfaceTests
{
    /// <summary>
    /// ONE constructor, and this is the assertion that keeps it that way. A transpiled component
    /// gets one JS constructor, so C# overloads do not survive the crossing: adding
    /// `IconButton(Icons …)` produced a twin that assigned whatever it was handed straight to
    /// `glyph` — the delegation to `new Icon(...)` simply vanished — and every caller passing a
    /// glyph died on `undefined.viewBox` in the browser while the .NET suite stayed green.
    /// <para>
    /// `Icon` has two constructors and gets away with it because its twin is HAND-WRITTEN and takes
    /// a `string | IconGlyph` union. That is why its precedent does not transfer, and the reason
    /// belongs in a test rather than in someone's memory.
    /// </para>
    /// </summary>
    [Fact]
    public void ATranspiledComponent_HasExactlyOneConstructor()
    {
        typeof(IconButton).GetConstructors().Should().ContainSingle(
            "the twin is JavaScript — a second constructor is a delegation that disappears");
    }

    /// <summary>
    /// Both factory names resolve, and they are two names because a factory takes no overloads —
    /// the same split <c>Icon</c> and <c>Glyph</c> already use for one type.
    /// </summary>
    [Fact]
    public void BothFactories_MirrorTheirConstructors()
    {
        Components.UI.IconButton(Icons.Menu, "Open navigation").Label.Should().Be("Open navigation");
        Components.UI.GlyphButton(CuratedIcons.Resolve(Icons.Menu), "Open navigation").Label
            .Should().Be("Open navigation");
    }

    /// <summary>The label is positional and required — an icon-only button is the one control with
    /// no text of its own to fall back on, and the only moment to catch that is the call site.</summary>
    [Fact]
    public void TheAccessibleName_IsNotOptional()
    {
        typeof(IconButton).GetConstructors()
            .Should().OnlyContain(c => c.GetParameters().Length >= 2
                && c.GetParameters()[1].ParameterType == typeof(string)
                && !c.GetParameters()[1].IsOptional);
    }
}
