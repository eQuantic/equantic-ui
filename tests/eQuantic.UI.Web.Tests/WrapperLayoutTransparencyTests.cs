using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The four wrappers that stand between a child and its flex parent — Pressable, Hoverable, Link
/// and Adjustable — carry the child's WIDTH CONTRACT, not half of it.
/// <para>
/// Each takes `width: 100%` from a Fill child, because a wrapper that hugged would collapse the
/// child's own 100% against a shrink-to-fit box. None of them took the child's MAX-WIDTH, so a
/// card that fills up to a limit became a full-width wrapper holding a narrower block: the card
/// hugged the left edge and the row's `justify-content: center` had a full-width item to centre,
/// which is nothing to centre.
/// </para>
/// <para>
/// Reported from a real page, measured there (wrapper 1392px against a card capped at 980), and
/// reduced to the case below by the site's own session. The SSR suite could not have seen it: the
/// markup is correct on both sides, and only the browser resolves what it means.
/// </para>
/// </summary>
public class WrapperLayoutTransparencyTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static HtmlNode Render(VisualNode node) => WebRealizer.Lower(node, Theme).Render();

    /// <summary>A card that fills its row UP TO a limit — the shape every centred panel has.</summary>
    private static Box CappedCard() => new(new BoxStyle
    {
        Width = SizeValue.Fill,
        MaxWidth = 980,
        Height = SizeValue.Fill,
        Background = Theme.Surface,
    }, new Text("panel"));

    public static TheoryData<string, VisualNode> Wrappers() => new()
    {
        { "Pressable", new Pressable(CappedCard(), () => { }) },
        { "Hoverable", new Hoverable(CappedCard(), _ => { }) },
        { "Link", new Link("/somewhere", CappedCard()) },
        { "Adjustable", new Adjustable(CappedCard(), _ => { }) },
    };

    [Theory]
    [MemberData(nameof(Wrappers))]
    public void AWrapperCarriesTheChildsCap_NotOnlyItsFill(string name, VisualNode wrapper)
    {
        var row = new Row(gap: 0) { Width = SizeValue.Fill, Main = MainAlign.Center };
        row.Add(wrapper);

        // The row's own element also says `width: 100%`, so the wrapper is taken by POSITION —
        // the row's single child — rather than by a style the parent happens to share.
        var lowered = Render(row).Children.Should().ContainSingle().Which;

        lowered.Attributes.GetValueOrDefault("style", "").Should().Contain("max-width: 980px",
            $"{name} stands in for its child in the row's layout, so it carries the whole width "
            + "contract — a wrapper that takes the fill and drops the cap is full width, and the "
            + "row has nothing left to centre");
    }

    [Theory]
    [MemberData(nameof(Wrappers))]
    public void AWrapperCarriesBothAxes(string name, VisualNode wrapper)
    {
        // Half a contract is its own divergence, and it bit twice in this PR from opposite sides:
        // the twin's Link was short a height, and this side's Adjustable was. A wrapper mirrors
        // the axes it stands in for, or the two targets lay the same tree out differently.
        var column = new Column(gap: 0) { Height = SizeValue.Fill };
        column.Add(wrapper);

        var lowered = Render(column).Children.Should().ContainSingle().Which;

        lowered.Attributes.GetValueOrDefault("style", "").Should().Contain("height: 100%",
            $"{name} stands in for a child that fills the cross axis");
    }

    [Fact]
    public void AChildWithNoCapIsUntouched()
    {
        // The pass-through only mirrors what the child declared. A plain Fill child must not
        // acquire a limit it never had.
        var row = new Row(gap: 0) { Width = SizeValue.Fill };
        row.Add(new Pressable(new Box(new BoxStyle { Width = SizeValue.Fill }, new Text("x")), () => { }));

        var lowered = Render(row).Children.Should().ContainSingle().Which;

        lowered.Attributes.GetValueOrDefault("style", "").Should().Contain("width: 100%");
        lowered.Attributes.GetValueOrDefault("style", "").Should().NotContain("max-width");
    }
}
