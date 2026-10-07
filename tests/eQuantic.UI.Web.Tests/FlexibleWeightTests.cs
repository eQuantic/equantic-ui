using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The weight an app writes is the weight that renders (#680). <c>Flexible(child, flex: 0,
/// basis: 540)</c> rendered as <c>flex: 1 1 540px</c> on the server, because the constructor raised
/// a zero to 1 without a word, while the browser's twin, which never clamped, wrote
/// <c>flex: 0 1 540px</c>: the item grew, and the two realizers disagreed about it.
/// <para>
/// CROSS-PIN: the declarations below are asserted verbatim by the runtime's
/// <c>flex-weight.spec.ts</c>. The server render and the twin must write byte-identical styles, or
/// hydration repaints what the server drew.
/// </para>
/// </summary>
public class FlexibleWeightTests
{
    private static string StyleOf(Flexible flexible)
    {
        var row = new Row(gap: 0);
        row.Add(flexible);
        return WebRealizer.Lower(row, PhotonTheme.Instance).Render().Children[0].Attributes["style"]!;
    }

    /// <summary>"Start at 540 and never grow", as the issue measured it through the factory an app
    /// writes, and as the constructor takes it.</summary>
    [Fact]
    public void AZeroWeight_IsWrittenAsZero_AndKeepsItsBasis()
    {
        StyleOf(Components.UI.Flexible(new Text("t"), flex: 0, basis: 540)).Should().Contain("flex: 0 1 540px");
        StyleOf(new Flexible(new Text("t"), flex: 0, basis: 540, shrink: 0)).Should().Contain("flex: 0 0 540px");
    }

    /// <summary>
    /// Without a basis a zero weight starts from its CONTENT. <c>0%</c>, the basis a weighted child
    /// starts from, would size an item that never grows at nothing: measured in a browser, a 200px
    /// child in a <c>flex: 0 1 0%</c> item came out 0 wide, and in <c>flex: 0 1 auto</c> 200.
    /// </summary>
    [Fact]
    public void AZeroWeight_WithoutABasis_StartsFromItsContent()
    {
        StyleOf(new Flexible(new Text("t"), flex: 0)).Should().Contain("flex: 0 1 auto");
        StyleOf(new Flexible(new Text("t"), flex: 2)).Should().Contain("flex: 2 1 0%",
            "a weighted child without a basis keeps the historical shape");
    }

    /// <summary>The door a parameter check leaves open: an object initializer writes the property
    /// itself. It already kept a zero the constructor clamped, so the same node rendered two ways
    /// depending on how it was written; both doors now render the one the app wrote.</summary>
    [Fact]
    public void AZeroWeight_SetByAnInitializer_RendersAsTheConstructorsDoes()
    {
        var initialized = StyleOf(new Flexible(new Text("t")) { Flex = 0, Basis = 540 });

        initialized.Should().Contain("flex: 0 1 540px");
        initialized.Should().Be(StyleOf(new Flexible(new Text("t"), flex: 0, basis: 540)));
    }

    /// <summary>A negative weight, basis or shrink means nothing on any target (CSS drops the
    /// declaration whole), so it is refused where it is written, through the constructor and through
    /// an initializer, and never clamped into a number nobody wrote.</summary>
    [Fact]
    public void ANegativeNumber_IsRefused_AtEveryDoor()
    {
        Refused(() => new Flexible(new Text("t"), flex: -1), nameof(Flexible.Flex));
        Refused(() => new Flexible(new Text("t")) { Flex = -1 }, nameof(Flexible.Flex));
        Refused(() => Components.UI.Flexible(new Text("t"), flex: -1), nameof(Flexible.Flex));

        Refused(() => new Flexible(new Text("t"), basis: -20), nameof(Flexible.Basis));
        Refused(() => new Flexible(new Text("t")) { Basis = -20 }, nameof(Flexible.Basis));

        Refused(() => new Flexible(new Text("t"), shrink: -2), nameof(Flexible.Shrink));
        Refused(() => new Flexible(new Text("t")) { Shrink = -2 }, nameof(Flexible.Shrink));
    }

    /// <summary>A basis is a size: NaN read as "no basis" and an infinity would reach the browser as
    /// a declaration it drops, so neither is one.</summary>
    [Fact]
    public void ABasisThatIsNotAFiniteSize_IsRefused()
    {
        Refused(() => new Flexible(new Text("t"), basis: float.NaN), nameof(Flexible.Basis));
        Refused(() => new Flexible(new Text("t"), basis: float.PositiveInfinity), nameof(Flexible.Basis));
    }

    private static void Refused(Func<Flexible> build, string member) =>
        build.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be(member);
}
