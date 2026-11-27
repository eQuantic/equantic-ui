using eQuantic.Studio;
using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The desktop sample's own form, typed into through its host, where #996's rule meets real
/// components: a <c>TextInput</c>, a stateful component of its own that rebuilds its field on focus,
/// and a <c>SearchField</c>, a stateless one that builds its field only when its page does, under the
/// Studio's shell, whose page keeps the values with <c>SetState</c>. The scenarios of <see cref="AFieldKeepsWhatWasTypedTests"/> on a page someone wrote.
/// </summary>
public class StudioFieldsTests
{
    private const float Width = 1180;
    private const float Height = 820;

    private static PhotonHost OpenInputs()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["section"] = GallerySection.Inputs.ToString() })
            .Build();
        var host = new PhotonHost(new StudioShell(configuration), PhotonTheme.Instance, ThemeMode.Light, Width, Height);
        Frame(host);
        Frame(host);
        return host;
    }

    private static void Frame(PhotonHost host) => host.RenderFrame(new DisplayListBuilder());

    /// <summary>Presses the field's middle: its trailing edge is where a search field draws its
    /// clear button.</summary>
    private static void PressField(PhotonHost host, string label)
    {
        var field = host.LastFrame!.TextRegions.Single(r => r.Entry.Label == label).Bounds.Center;
        host.PressDown(field.X, field.Y);
        host.PressUp(field.X, field.Y);
        Frame(host);
    }

    private static string? Heard(PhotonHost host, string label) =>
        host.Semantics().Single(s => s.Role == SemanticRole.TextField && s.Label == label).Value;

    [Fact]
    public void ANameTypedIntoTheForm_ShowsKeyByKey()
    {
        var host = OpenInputs();
        var held = Heard(host, "Full name")!;
        PressField(host, "Full name");
        host.KeyDown("End");

        foreach (var key in "Ada")
        {
            host.TextInput(key.ToString());
            Frame(host);
        }

        Heard(host, "Full name").Should().Be(held + "Ada",
            "the form holds the name it opened with, and each key lands after it");
    }

    [Fact]
    public void ASearchClearedByItsButton_ShowsNothing()
    {
        var host = OpenInputs();
        PressField(host, "Search transactions");
        foreach (var key in "rent")
        {
            host.TextInput(key.ToString());
            Frame(host);
        }
        Heard(host, "Search transactions").Should().Be("rent");

        // At the button's centre: the field stays in its cell, so the next column's stepper no longer
        // covers the button and takes the press (#1256).
        var clear = host.LastFrame!.HitRegions.Single(r => r.Node.Label == "Clear search").Bounds;
        host.PressDown(clear.Center.X, clear.Center.Y);
        host.PressUp(clear.Center.X, clear.Center.Y);
        Frame(host);

        Heard(host, "Search transactions").Should().BeEmpty(
            "the page built the field again holding an empty query, and the field shows what the page holds");
    }

    /// <summary>
    /// The search field stays inside its grid cell, typed into or not, and its clear button with it: its entry is a
    /// Flexible between its icon and its button in a column that hugs its content, and the line offered it the
    /// whole room, so it ran 30dp past its cell before a key and 88dp after, under the next column's stepper (#1256).
    /// The widths are the grid's: 1180 wide, a cell of 308dp, the field's padding 14dp a side.
    /// </summary>
    [Fact]
    public void TheSearchField_StaysInsideItsGridCell_AndItsClearButtonWithIt()
    {
        var host = OpenInputs();
        var name = Field(host, "Full name");
        var search = Field(host, "Search transactions");
        search.Width.Should().Be(name.Width, "both fields fill a cell of the same column");
        search.Right.Should().Be(name.Right);
        Entry(host).Width.Should().Be(308 - 2 * 14 - 20 - 10, "the entry takes what the icon and its gap leave");

        PressField(host, "Search transactions");
        host.TextInput("r");
        Frame(host);

        Field(host, "Search transactions").Width.Should().Be(name.Width, "a query typed shows the clear button inside the field");
        Entry(host).Width.Should().Be(308 - 2 * 14 - 20 - 48 - 2 * 10, "the button and its gap come out of the entry's share");
        var clear = host.LastFrame!.HitRegions.Single(r => r.Node.Label == "Clear search").Bounds;
        var stepper = host.LastFrame!.HitRegions.Single(r => r.Node.Label == "Decrease Quantity").Bounds;
        clear.Right.Should().BeLessThanOrEqualTo(name.Right, "the clear button ends inside its cell");
        clear.Right.Should().BeLessThan(stepper.X, "and before the next column's stepper begins");
    }

    /// <summary>The field a labelled control draws in its cell: the SearchField or the TextInput, as laid out.</summary>
    private static Rect Field(PhotonHost host, string label)
    {
        var entry = host.LastFrame!.TextRegions.Single(r => r.Entry.Label == label).Element;
        for (var node = Laid(host.LastFrame!.Root, entry); node is not null; node = node.Parent)
            if (node.Source is SearchField or TextInput) return node.Bounds;
        throw new InvalidOperationException($"no field holds the entry '{label}'");
    }

    /// <summary>The search entry's slot, the Flexible its row lays it in.</summary>
    private static Rect Entry(PhotonHost host)
    {
        var entry = host.LastFrame!.TextRegions.Single(r => r.Entry.Label == "Search transactions").Element;
        for (var node = Laid(host.LastFrame!.Root, entry); node is not null; node = node.Parent)
            if (node.Source is Flexible) return node.Bounds;
        throw new InvalidOperationException("the search entry is in no Flexible");
    }

    private static LayoutNode? Laid(LayoutNode node, Element element)
    {
        if (node.Element == element) return node;
        foreach (var child in node.Children)
            if (Laid(child, element) is { } found) return found;
        return null;
    }
}
