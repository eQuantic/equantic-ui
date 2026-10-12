using eQuantic.UI.Primitives;
using SharedLibrary.Common.Resources;

namespace SharedLibrary.Common;

/// <summary>
/// The brand's mark beside its name: a component every app of the solution places in its pages. Its
/// two strings come from this library's own catalogue, so each app shows them in the language of the
/// request without holding a copy. The app that places it may have a catalogue named Strings too,
/// with a Title of its own: each read shows its own project's text.
/// </summary>
public sealed class Wordmark : StatelessComponent
{
    public override VisualNode Build(ComponentContext context) =>
        Row(gap: Space.S2, cross: CrossAlign.Center, children:
        [
            Box(new BoxStyle
            {
                Width = SizeValue.Fixed(14),
                Height = SizeValue.Fixed(14),
                Background = Palette.Cobalt,
                CornerRadius = new CornerRadii(3),
            }),
            Column(gap: Space.S1, children:
            [
                Text(Strings.Brand, TypeRole.Title, context.Theme.TextPrimary, maxLines: 1),
                Text(Strings.Title, TypeRole.Caption, context.Theme.TextMuted, maxLines: 1),
            ]),
        ]);
}
