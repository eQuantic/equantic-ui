using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A flexible <see cref="Spacer"/>'s weight is 1 or more (#691). The constructor raised a zero or
/// a negative weight to 1 without a word, and so did the browser's twin; a spacer with no share has
/// no size, which is why Flutter asserts <c>flex &gt; 0</c> for its own. A weight below 1 is now
/// refused where it is written, and the rigid form, <see cref="Spacer.Fixed"/>, keeps the one zero
/// a spacer holds.
/// <para>
/// CROSS-PIN: the runtime's <c>flex-weight.spec.ts</c> refuses the same weights on the twin.
/// </para>
/// </summary>
public class SpacerWeightTests
{
    [Fact]
    public void AWeightBelowOne_IsRefused_AtEveryDoor()
    {
        Refused(() => new Spacer(0));
        Refused(() => new Spacer(-3));
        Refused(() => new Spacer { Flex = 0 });
        Refused(() => Components.UI.Spacer(0));
    }

    [Fact]
    public void TheRigidForm_KeepsItsZeroWeight_AndAWeightRendersAsWritten()
    {
        var rigid = Spacer.Fixed(24);
        rigid.Flex.Should().Be(0, "the rigid form has no weight, only a length");
        rigid.FixedLength.Should().Be(24);
        Components.UI.Gap(24).Flex.Should().Be(0);

        var row = new Row(gap: 0);
        row.Add(new Spacer(3));
        WebRealizer.Lower(row, PhotonTheme.Instance).Render().Children[0].Attributes["style"]
            .Should().Contain("flex: 3 1 0%");
    }

    private static void Refused(Func<Spacer> build) =>
        build.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be(nameof(Spacer.Flex));
}
