using eQuantic.UI.Primitives;

namespace eQuantic.Console;

/// <summary>
/// A gesture the catalog does not ship (#1563): cards in a scrolling list, each wrapped in a
/// <c>RawGestureDetector</c> that hosts the dashboard's own <see cref="HoldThenDrag"/>. A card held still lifts and
/// then follows the pointer sideways; a swipe at once scrolls the list; a tap presses the card's button. The
/// recognizer competes for the pointer with the button's press and the list's scroll in one arena, on the web and
/// on Photon alike.
/// <para>
/// The browser suite drives this screen with a finger and with a mouse (<c>tests/e2e/tests/gestures.spec.ts</c>),
/// reading what the screen reports under the heading.
/// </para>
/// </summary>
[Page("/gestures", Title = "Gestures — eQuantic Console")]
public sealed class GesturesScreen : StatefulComponent
{
    private static readonly string[] Cards =
    [
        "Draft the brief", "Review the copy", "Book the venue", "Order the prints", "Send the invites",
        "Confirm the speakers", "Test the sound", "Print the badges", "Brief the crew", "Open the doors",
        "Thank the guests", "Close the books",
    ];

    /// <summary>The card a hold lifted, or null.</summary>
    private string? _lifted;

    /// <summary>How far the lifted card is carried sideways.</summary>
    private float _carried;

    private string _moved = "No card moved yet.";
    private string _opened = "No card opened yet.";

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var cards = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        foreach (var card in Cards) cards.Add(Card(context, card));

        return Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = theme.Background,
            Padding = EdgeInsets.All(Space.S6),
        },
        Column(gap: Space.S4, children: [
            Text("Gestures", TypeRole.Heading, theme.TextPrimary),
            Text("Hold a card still, then drag it sideways. A swipe scrolls the list, and a tap opens the card.",
                TypeRole.BodyM, theme.TextSecondary),
            Group(Text(_moved, TypeRole.BodyL, theme.TextPrimary), "Moved"),
            Group(Text(_opened, TypeRole.BodyL, theme.TextPrimary), "Opened"),
            Group(ScrollView(Box(new BoxStyle { Width = SizeValue.Fill, Padding = EdgeInsets.All(Space.S2) }, cards),
                height: 360), "Cards"),
        ]));
    }

    private VisualNode Card(ComponentContext context, string title)
    {
        var theme = context.Theme;
        var lifted = _lifted == title;
        var face = Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S3),
            Background = lifted ? theme.SurfaceSubtle : theme.Surface,
            BorderWidth = 1,
            BorderColor = lifted ? theme.FocusRing : theme.Border,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Small)),
            Transform = lifted ? Transform2D.Translate(_carried) : null,
        },
        Row(gap: Space.S3, main: MainAlign.SpaceBetween, cross: CrossAlign.Center, width: SizeValue.Fill, children: [
            Text(title, TypeRole.BodyM, theme.TextPrimary),
            Button("Open", Variant.Secondary, SizeVariant.Small, () => SetState(() => _opened = $"Opened: {title}")),
        ]));

        return RawGestureDetector(Group(face, title), [
            new GestureRecognizerFactoryWithHandlers<HoldThenDrag>(() => new(), drag =>
            {
                drag.OnHeld = () => SetState(() =>
                {
                    _lifted = title;
                    _carried = 0;
                });
                drag.OnMoved = offset => SetState(() => _carried = offset.X);
                drag.OnReleased = offset => SetState(() =>
                {
                    _lifted = null;
                    _carried = 0;
                    _moved = $"Moved {title} by ({MathF.Round(offset.X)}, {MathF.Round(offset.Y)})";
                });
                drag.OnCancelled = () => SetState(() =>
                {
                    _lifted = null;
                    _carried = 0;
                });
            }),
        ]);
    }
}
