using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// A single choice out of two to five, all of them VISIBLE (spec B9) — the difference from a Select,
/// which hides its options until asked. Use it when the options are short, fixed and worth comparing
/// at a glance ("All · Income · Expenses"); past five segments the labels stop fitting and the
/// control becomes a Select. CONTROLLED: the caller owns <see cref="SelectedIndex"/>.
/// <para>
/// The selected segment is a raised thumb over a recessed track — the same Surface/SurfaceSubtle
/// pairing a Card uses, so selection reads as elevation rather than as a colour the user must learn.
/// Segments share the width equally, so the thumb never resizes as the choice moves.
/// </para>
/// </summary>
public sealed class SegmentedControl : StatelessComponent
{
    public SegmentedControl(IReadOnlyList<string> segments, int selectedIndex,
        Action<int>? onChanged = null)
    {
        Segments = segments;
        SelectedIndex = selectedIndex;
        OnChanged = onChanged;
    }

    public IReadOnlyList<string> Segments { get; init; }
    public int SelectedIndex { get; init; }
    public Action<int>? OnChanged { get; init; }

    public SizeVariant Size { get; init; } = SizeVariant.Medium;
    public bool Disabled { get; init; }

    /// <summary>Fills the available width; otherwise the control hugs its widest segment.</summary>
    public bool Stretch { get; init; } = true;

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var height = Sizing.Height(Size);
        var inset = 3f;                                   // the track's lip around the thumb
        var trackRadius = Sizing.Radius(Size);

        var row = new Row(gap: 0) { Height = SizeValue.Fill, Cross = CrossAlign.Stretch };
        for (var i = 0; i < Segments.Count; i++)
        {
            var index = i;
            var selected = index == SelectedIndex;

            var label = new Row(gap: 0) { Main = MainAlign.Center, Cross = CrossAlign.Center };
            label.Add(new Text(Segments[index], TypeRole.Label,
                selected ? theme.TextPrimary : theme.TextSecondary, maxLines: 1)
            {
                StyleOverride = theme.Type(TypeRole.Label) with { Size = Sizing.LabelSize(Size) },
                Transition = TransitionSpec.Of(StyleChannels.Colors, Motion.Press),
            });

            var segment = new Box(new BoxStyle
            {
                Width = SizeValue.Fill,
                Height = SizeValue.Fill,
                Background = selected ? theme.Surface : null,
                CornerRadius = new CornerRadii(trackRadius - inset),
                Elevation = selected ? 1 : 0,
                Transition = TransitionSpec.Of(StyleChannels.Colors | StyleChannels.Shadow, Motion.Press),
            }, label);

            var press = new Pressable(segment, Disabled || selected ? null : () => OnChanged?.Invoke(index))
            {
                Disabled = Disabled,
                Label = Segments[index],
                Selected = selected,
            };

            row.Add(Stretch ? new Flexible(press, 1) : press);
        }

        return new Box(new BoxStyle
        {
            Width = Stretch ? SizeValue.Fill : SizeValue.Hug,
            Height = height,
            Padding = EdgeInsets.All(inset),
            Background = theme.SurfaceSubtle,
            CornerRadius = new CornerRadii(trackRadius),
            Opacity = Disabled ? theme.DisabledOpacity : 1f,
        }, row);
    }
}
