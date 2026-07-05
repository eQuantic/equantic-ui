using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// The design system's Badge (spec B7): a non-interactive count/dot annotation. Dot 8dp; count pill
/// 16dp min height, Radius.Full, 10/700 label, 4dp X padding, "99+" past <see cref="Max"/>.
/// Destructive pair = attention (default), neutral SurfaceSubtle = passive counts, status pairs
/// allowed. v1 renders INLINE — the −4/−4 overlay attach composes via Stack + Positioned when the
/// Stack primitive lands; the 2dp Surface ring applies only to that overlay case.
/// </summary>
public sealed class Badge : StatelessComponent
{
    public Badge(int count = 0, int max = 99, Variant variant = Variant.Destructive)
    {
        Count = count;
        Max = max;
        Variant = variant;
    }

    public int Count { get; init; }
    public int Max { get; init; }
    public Variant Variant { get; init; }

    /// <summary>Neutral passive-count styling (SurfaceSubtle pair) instead of a variant pair.</summary>
    public bool Neutral { get; init; }

    /// <summary>The 8dp presence dot — no count.</summary>
    public bool Dot { get; init; }

    /// <summary>The 2dp Surface ring the spec requires when the badge overlays content
    /// (attach via Stack + Positioned(top: −4, end: −4)).</summary>
    public bool Ring { get; init; }

    public static Badge AsDot(Variant variant = Variant.Destructive) => new(0, 99, variant) { Dot = true };

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var fill = Neutral ? theme.SurfaceSubtle : theme.Colors(Variant).Base;
        var textColor = Neutral ? theme.TextSecondary : theme.Colors(Variant).OnBase;

        if (Dot)
        {
            return new Box(new BoxStyle
            {
                Width = Ring ? 12 : 8,
                Height = Ring ? 12 : 8,
                Background = fill,
                CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Full)),
                BorderWidth = Ring ? 2f : 0f,
                BorderColor = theme.Surface,
            });
        }

        var label = Count > Max ? $"{Max}+" : $"{Count}";
        var text = new Text(label, TypeRole.Caption, textColor, maxLines: 1)
        {
            StyleOverride = new TypeStyle(10, 12, FontWeight.Bold, 0, 1.3f),
        };

        var content = new Row(gap: 0) { Main = MainAlign.Center, Height = SizeValue.Fill };
        content.Add(text);

        return new Box(new BoxStyle
        {
            Height = Ring ? 20 : 16,
            MinWidth = Ring ? 20 : 16,
            Padding = EdgeInsets.Symmetric(Space.S1, 0),
            Background = fill,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Full)),
            BorderWidth = Ring ? 2f : 0f,
            BorderColor = theme.Surface,
        }, content);
    }
}
