using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The half of the unlabelled-radio-group fix that matters most, on the side that does NOT guard.
/// <para>
/// <c>RadioGroup.Label</c> is <c>string?</c> and <c>Adjustable.Label</c> is declared <c>string</c>
/// with an empty default, so a group with no label used to write null over a value its own type
/// forbids. The web realizer survived that by accident — <c>is { Length: > 0 }</c> rejects null and
/// empty alike — while <c>Semantics.cs</c> passes the label into the node exactly as it finds it.
/// A web-side test alone would have proved the fix on the realizer that never needed it.
/// </para>
/// </summary>
public class UnlabelledGroupSemanticsTests
{
    private static IReadOnlyList<SemanticNode> SemanticsOf(VisualNode root)
    {
        var host = new PhotonHost(root, PhotonTheme.Instance, ThemeMode.Light, 400, 300);
        host.RenderFrame(new DisplayListBuilder());
        return host.Semantics();
    }

    [Fact]
    public void AnUnlabelledGroup_ReachesTheAccessibilityTreeWithAnEmptyName_NotNull()
    {
        var nodes = SemanticsOf(new RadioGroup(["Monthly", "Yearly"], 0, onChanged: _ => { }));

        var group = nodes.Should().ContainSingle(n => n.Role == SemanticRole.Slider).Subject;
        group.Label.Should().NotBeNull("the vocabulary says an absent label is \"\", and this is what a platform bridge reads");
        group.Label.Should().BeEmpty();
    }

    [Fact]
    public void ALabelledGroup_CarriesItsNameThrough()
    {
        var nodes = SemanticsOf(new RadioGroup(["Monthly", "Yearly"], 0, onChanged: _ => { }) { Label = "Billing" });

        nodes.Should().ContainSingle(n => n.Role == SemanticRole.Slider)
            .Which.Label.Should().Be("Billing");
    }
}
