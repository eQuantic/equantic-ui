using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// <see cref="VisualNode.Key"/> is identity among siblings, and both realizers read the one
/// property: Photon spells it into the layout path, the web hands it to the reconciler's keyed
/// diff. Until this landed the web funnel dropped it — a DataTable had keyed its rows for months
/// and the client had diffed them by position all along.
/// </summary>
public class KeyedIdentityTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static HtmlNode Render(VisualNode node) => WebRealizer.Lower(node, Theme).Render();

    [Fact]
    public void AKeyReachesTheNodeTheReconcilerDiffs()
    {
        Render(new Box(new BoxStyle()) { Key = "17" }).Key.Should().Be("17");
    }

    [Fact]
    public void AnyNodeCanCarryOne_BecauseItIsWrittenInTheFunnel()
    {
        Render(new Text("Row", TypeRole.BodyM) { Key = "row" }).Key.Should().Be("row");
        Render(new Text("Row", TypeRole.BodyM)).Key.Should().BeNull("no key means positional identity, as before");
    }

    [Fact]
    public void AKeyIsNeverAnAttribute()
    {
        // The DOM the two funnels produce is unchanged by a key: SSR and hydration stay identical.
        var rendered = Render(new Box(new BoxStyle()) { Key = "17" });
        rendered.Attributes.Keys.Should().NotContain(k => k.Contains("key", StringComparison.OrdinalIgnoreCase));
    }
}
