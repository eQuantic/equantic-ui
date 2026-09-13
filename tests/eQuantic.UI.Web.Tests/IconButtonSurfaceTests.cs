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
    [Fact]
    public void ACuratedGlyph_IsTheCallAReaderWrites()
    {
        var button = new IconButton(Icons.Close, "Close");

        button.Glyph.Should().NotBeNull("the node is built for the caller, not by them");
        button.Label.Should().Be("Close");
    }

    [Fact]
    public void APackGlyph_IsTheSameCall()
    {
        // Any pack: the catalogs are IconGlyph, which is what a consumer already holds.
        var glyph = CuratedIcons.Resolve(Icons.Search);
        var button = new IconButton(glyph, "Search");

        button.Glyph.Glyph.Should().Be(glyph);
    }

    /// <summary>
    /// The NODE form stays. Adding a constructor is free; removing one is a MissingMethodException
    /// at load for anything already compiled against it — the lesson the type scale paid for.
    /// </summary>
    [Fact]
    public void TheNodeForm_StillExists()
    {
        typeof(IconButton).GetConstructors()
            .Where(c => c.GetParameters() is [{ ParameterType.Name: nameof(Icon) }, ..])
            .Should().ContainSingle("a released signature is not removed");
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
