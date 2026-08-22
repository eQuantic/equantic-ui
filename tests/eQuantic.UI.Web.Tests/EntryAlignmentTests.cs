using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A text entry sits in the MIDDLE of the box it was given, and an autofilled one keeps the
/// field's own surface.
/// <para>
/// Both were reported from a real form. The entry hugged the top edge — measured in the browser at
/// 0px above and 24px below inside a 44dp box, caret and placeholder included — because the
/// <c>.eq-field</c> shell was a plain block of content height, sitting at the top of whatever
/// container it was dropped into. The framework's own TextInput escaped it by wrapping the entry
/// in a centred Row, which is exactly the kind of thing every hand-built field has to remember and
/// nobody does.
/// </para>
/// <para>
/// The autofill half is Chrome painting its own background and text colour over an autofilled
/// control: a pale rectangle floating inside a themed field, in the one flow where a form looks
/// least finished. Clipping that background to the TEXT makes it invisible without needing to know
/// the surrounding colour, which is what lets one rule serve every theme.
/// </para>
/// </summary>
public class EntryAlignmentTests
{
    private static string Sheet() => PhotonCssGenerator.Generate(PhotonTheme.Instance);

    [Fact]
    public void TheFieldShellFillsItsBoxAndCentresTheEntry()
    {
        var css = Sheet();
        var rule = css.Split('\n').Single(line => line.TrimStart().StartsWith(".eq-field {"));

        rule.Should().Contain("display: flex")
            .And.Contain("align-items: center")
            .And.Contain("height: 100%");
    }

    [Fact]
    public void AnAutofilledEntryKeepsTheFieldsSurfaceAndTheThemesInk()
    {
        var css = Sheet();

        // The state sticks through hover, focus and active — Chrome re-asserts it on each, so a
        // rule that names only the base selector loses the field again the moment it is touched.
        foreach (var state in new[] { ":-webkit-autofill", ":-webkit-autofill:hover",
                                      ":-webkit-autofill:focus", ":-webkit-autofill:active" })
            css.Should().Contain($".eq-entry{state}");

        css.Should().Contain("-webkit-background-clip: text")
            .And.Contain("-webkit-text-fill-color: var(--eq-color-text-primary)")
            .And.Contain("caret-color: var(--eq-color-text-primary)");
    }
}
