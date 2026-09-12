using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A RadioGroup without a label must not hand a NULL one down. <c>Adjustable.Label</c> is declared
/// <c>string</c> with an empty default, and the group was writing its own <c>string?</c> straight
/// over it — a value the type says cannot occur, which is exactly the kind nobody guards for.
/// <para>
/// The web realizer survived it by accident (<c>is { Length: > 0 }</c> rejects null and empty
/// alike). The Photon side does not guard: <c>Semantics.cs</c> passes the label into a
/// <c>SemanticRole.Slider</c> node as it finds it. Same shape as <c>Text.Align</c> — one realizer
/// carrying the other's assumption — and it was reported as a compiler warning nobody could see,
/// because every build command in this repo greps for errors.
/// </para>
/// </summary>
public class UnlabelledGroupTests
{
    /// <summary>The group builds a Column: the optional label Text, then the one Tab stop.</summary>
    private static Adjustable? GroupOf(RadioGroup group) =>
        (group.Build(new ComponentContext(PhotonTheme.Instance)) as Column)?
            .Children.OfType<Adjustable>().FirstOrDefault();

    [Fact]
    public void AGroupWithNoLabel_HandsDownTheEmptyString_NotNull()
    {
        var group = new RadioGroup(["a", "b"], 0, onChanged: _ => { });

        var adjustable = GroupOf(group);
        adjustable.Should().NotBeNull("the group is one Tab stop and that stop is the Adjustable");
        adjustable!.Label.Should().NotBeNull("the declared type says it cannot be null");
        adjustable.Label.Should().BeEmpty();
    }

    [Fact]
    public void AGroupWithALabel_StillCarriesIt()
    {
        var group = new RadioGroup(["a", "b"], 0, onChanged: _ => { }) { Label = "Plan" };

        GroupOf(group)!.Label.Should().Be("Plan");
    }
}
