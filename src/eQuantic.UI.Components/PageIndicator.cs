using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// The dot row under a pager (spec B16): WHERE you are in a short, ordered set — onboarding, a
/// carousel, a wizard. It is a readout of the pager's state, never the primary way to move through
/// it; the dots take taps only as a courtesy, so <see cref="OnSelected"/> is optional.
/// <para>
/// The current dot is a wider capsule rather than a colour-only change — position must survive
/// greyscale and low vision. Past eight pages a dot row stops being readable, so the control shows
/// "n of m" instead; that threshold is the component's, not the caller's, so nobody has to remember it.
/// </para>
/// </summary>
public sealed class PageIndicator : StatelessComponent
{
    /// <summary>Beyond this many pages the dots become an "n of m" readout.</summary>
    public const int MaxDots = 8;

    public PageIndicator(int count, int currentIndex, Action<int>? onSelected = null)
    {
        Count = count;
        CurrentIndex = currentIndex;
        OnSelected = onSelected;
    }

    public int Count { get; init; }
    public int CurrentIndex { get; init; }
    public Action<int>? OnSelected { get; init; }

    /// <summary>Tints the active dot with a variant instead of the default primary.</summary>
    public Variant Variant { get; init; } = Variant.Primary;

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var active = theme.Colors(Variant).Base;
        var label = $"Page {CurrentIndex + 1} of {Count}";

        if (Count > MaxDots)
        {
            return new Text(label, TypeRole.Caption, theme.TextSecondary, maxLines: 1)
            {
                Tabular = true,
            };
        }

        // Non-interactive dots are a DECORATIVE echo of the pager's own state — the accessible
        // name lives on each dot only when the dots take taps and become real controls.
        var row = new Row(gap: Space.S1) { Cross = CrossAlign.Center };
        for (var i = 0; i < Count; i++)
        {
            var index = i;
            var current = index == CurrentIndex;

            var dot = new Box(new BoxStyle
            {
                Width = current ? 18 : 6,
                Height = 6,
                Background = current ? active : theme.BorderStrong,
                CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Full)),
                Transition = TransitionSpec.Of(StyleChannels.Colors | StyleChannels.Size, Motion.State),
            });

            row.Add(OnSelected is null
                ? dot
                : new Pressable(HitPadded(dot), () => OnSelected(index))
                {
                    Label = $"Page {index + 1}",
                    Selected = current,
                });
        }

        return row;
    }

    /// <summary>A 6dp dot is not a target — the press area grows to the touch minimum around it.</summary>
    private static VisualNode HitPadded(VisualNode dot)
    {
        var centered = new Row(gap: 0)
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Main = MainAlign.Center,
            Cross = CrossAlign.Center,
        };
        centered.Add(dot);
        return new Box(new BoxStyle { Height = Touch.MinTarget, Padding = EdgeInsets.Symmetric(Space.S1, 0) }, centered);
    }
}
