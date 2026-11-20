using eQuantic.UI.Primitives;

namespace eQuantic.Console;

/// <summary>
/// A header that swaps what it shows as the page scrolls (#1769), with nothing rebuilt: every box in
/// it says what it looks like while the header is scrolled, in <see cref="BoxStyle.Scrolled"/>, and
/// its own <see cref="BoxStyle.Transition"/> glides the swap.
/// <para>
/// At the top of the page the badge is large; once the page has scrolled past the threshold it
/// shrinks and fades while the small mark with the name fades in where it was. The link turns from
/// its resting fill to the scrolled one, and still lights under the pointer. Under the bar, a list
/// with its own pinned group header follows ITS scroll, not the page's: the nearest header decides.
/// </para>
/// <para>
/// No destination leads here: it is the page the browser suite reads the swap on
/// (<c>tests/e2e/tests/header-scrolled.spec.ts</c>).
/// </para>
/// </summary>
[Page("/header", Title = "Scrolled header — eQuantic Console")]
public sealed class HeaderScreen : StatelessComponent
{
    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var brand = theme.Colors(Variant.Primary);
        var glide = new TransitionSpec(StyleChannels.Opacity | StyleChannels.Transform, Motion.BaseMs);

        // The two layers share the place, one over the other: the large badge, and the small mark.
        var badge = Group(Box(new BoxStyle
        {
            Width = 72, Height = 72,
            CornerRadius = new CornerRadii(36),
            Background = brand.Base,
            Transition = glide,
            Scrolled = new StyleDiff { Opacity = 0, Transform = Transform2D.Scale(0.6f) },
        }, Text("F", TypeRole.Display, brand.OnBase).Centered()), "Badge");

        var mark = Group(Box(new BoxStyle
        {
            Opacity = 0,
            Transition = glide,
            Scrolled = new StyleDiff { Opacity = 1 },
        }, Row(gap: Space.S2, cross: CrossAlign.Center, children: [
            Box(new BoxStyle
            {
                Width = 32, Height = 32,
                CornerRadius = new CornerRadii(16),
                Background = brand.Base,
            }, Text("F", TypeRole.Label, brand.OnBase).Centered()),
            Column(gap: 0, children: [
                Text("eQuantic", TypeRole.TitleSmall, theme.TextPrimary),
                Text("Console", TypeRole.Caption, theme.TextSecondary),
            ]),
        ])), "Mark");

        var link = Pressable(Box(new BoxStyle
        {
            Padding = EdgeInsets.Symmetric(Space.S3, Space.S2),
            CornerRadius = new CornerRadii(8),
            Background = theme.SurfaceSubtle,
            Transition = TransitionSpec.Colors(),
            Scrolled = new StyleDiff { Background = brand.Subtle },
            Hover = new StyleDiff { Background = brand.Hover },
        }, Text("Docs", TypeRole.Label, theme.TextPrimary)), () => { }, label: "Docs");

        // A list with its own pinned group header, inside the bar: it follows the list, not the page.
        var list = ScrollView(Column(gap: 0, width: SizeValue.Fill, children: [
            Pinned(Group(Box(new BoxStyle
            {
                Width = SizeValue.Fill,
                Padding = EdgeInsets.Symmetric(Space.S3, Space.S1),
                Background = theme.SurfaceSubtle,
                Scrolled = new StyleDiff { Background = brand.Subtle },
            }, Text("Recent", TypeRole.Label, theme.TextSecondary)), "Recent")),
            Box(new BoxStyle { Width = SizeValue.Fill, Height = 480 }),
        ]), height: 96);

        return Column(gap: 0, width: SizeValue.Fill, children: [
            Pinned(Box(new BoxStyle
            {
                Width = SizeValue.Fill,
                Padding = EdgeInsets.All(Space.S3),
                Background = theme.Surface,
            }, Column(gap: Space.S3, width: SizeValue.Fill, children: [
                Row(gap: Space.S4, main: MainAlign.SpaceBetween, cross: CrossAlign.Center, width: SizeValue.Fill,
                    children: [Stack(Alignment.CenterStart, children: [badge, mark]), link]),
                Group(list, "Rail"),
            ]))),
            Box(new BoxStyle { Width = SizeValue.Fill, Height = 2400 },
                Text("Scroll the page: the badge gives way to the mark.", TypeRole.BodyM, theme.TextSecondary)),
        ]);
    }
}
