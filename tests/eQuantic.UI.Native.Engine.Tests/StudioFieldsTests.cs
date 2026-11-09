using eQuantic.Studio;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The desktop sample's own form, typed into through its host, where #996's rule meets real
/// components: a <c>TextInput</c> and a <c>SearchField</c>, each a stateful component of its own that
/// holds a field and not its value, under the Studio's shell, whose page keeps the values with
/// <c>SetState</c>. The scenarios of <see cref="AFieldKeepsWhatWasTypedTests"/> on a page someone wrote.
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

        // The button's trailing part: the field overflows its cell, and the next column's stepper
        // covers the rest of the button and takes a press at its centre (#1256).
        var clear = host.LastFrame!.HitRegions.Single(r => r.Node.Label == "Clear search").Bounds;
        host.PressDown(clear.Right - 6, clear.Center.Y);
        host.PressUp(clear.Right - 6, clear.Center.Y);
        Frame(host);

        Heard(host, "Search transactions").Should().BeEmpty(
            "the page built the field again holding an empty query, and the field shows what the page holds");
    }
}
