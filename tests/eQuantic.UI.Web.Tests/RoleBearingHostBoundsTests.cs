using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A host that CARRIES A ROLE has the bounds of the thing it names.
///
/// <para>
/// A screen reader draws its touch-exploration rectangle and a browser draws its focus ring from
/// the role-bearing element's box, so the box is part of the announcement rather than a styling
/// detail. Both of these lower to a <c>div</c>, which is block-level: with a child that hugs, the
/// host stretched to the container while the control stayed its own width, and the two stopped
/// describing the same thing. Photon has never had the defect — <c>MeasureWrapper</c> gives a
/// layout-transparent wrapper exactly <c>inner.Bounds</c> — so this was a web-only divergence in
/// the one place the two targets must agree.
/// </para>
///
/// <para>
/// The component library hid it: <c>ProgressBar</c> and <c>Slider</c> both hand their wrapper a
/// FILL track, which takes the <c>width: 100%</c> branch. It is reachable only through the
/// vocabulary directly — <c>new Progress(...)</c>, <c>UI.Progress(...)</c> — which is public
/// surface, and through any future component whose track hugs.
/// </para>
///
/// <para>
/// <b><c>SheetSurface</c> is the third instance and is deliberately NOT here.</b> It carries
/// <c>role="grid"</c> and a tab stop over a bare block div, so it has this defect too — and a wider
/// one: it forwards neither the child's fill nor its cap, so it is absent from
/// <see cref="WrapperLayoutTransparencyTests"/> as well. Giving it the bounds rule means giving it
/// the width contract first, which changes a Spreadsheet's layout rather than only its
/// announcement. Named here rather than waved through, and filed; a hole a sweep does not mention
/// is how a rule quietly stops being one.
/// </para>
/// </summary>
public class RoleBearingHostBoundsTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static HtmlNode Render(VisualNode node) => WebRealizer.Lower(node, Theme).Render();

    /// <summary>A track that states its own size — the case the fill branch never reaches.</summary>
    private static Box HugTrack() => new(new BoxStyle { Width = 120, Height = 8 });

    private static Box FillTrack() => new(new BoxStyle { Width = SizeValue.Fill, Height = 8 });

    public static TheoryData<string, VisualNode, VisualNode> RoleBearingWrappers() => new()
    {
        { "Progress", new Progress(HugTrack()) { Label = "Uploading" }, new Progress(FillTrack()) },
        { "Adjustable", new Adjustable(HugTrack(), _ => { }), new Adjustable(FillTrack(), _ => { }) },
    };

    [Theory]
    [MemberData(nameof(RoleBearingWrappers))]
    public void AHugChildLeavesTheRoleBearingHostHuggingToo(string name, VisualNode hug, VisualNode fill)
    {
        var hugHost = Render(hug);
        var fillHost = Render(fill);

        hugHost.Attributes.Should().ContainKey("role", $"{name} is here because its host announces one");
        hugHost.Attributes.GetValueOrDefault("style", "").Should().Contain("width: fit-content",
            $"{name}'s host is what a reader outlines, so a child that hugs leaves it hugging — a "
            + "block div would announce the container's width for a control that is 120px wide");

        fillHost.Attributes.GetValueOrDefault("style", "").Should().Contain("width: 100%",
            "and the fill branch is untouched: a filling track still makes the host fill, or the "
            + "child's own 100% would resolve against a shrink-to-fit box and collapse");
        fillHost.Attributes.GetValueOrDefault("style", "").Should().NotContain("fit-content",
            "the two are exclusive — a host that said both would be deciding nothing");
    }
}
