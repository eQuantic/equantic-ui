using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// The design system's EmptyState (spec B17): a centered Column — icon Xl 32 in a 64dp Radius.Full
/// SurfaceSubtle well → 16 → title 20/600 → 6 → body (15, TextSecondary, ≤ 2 lines) → 20 → one
/// Medium Button (+ optional Ghost secondary). Vertical padding S12; it centers in the space the
/// missing content would occupy. Three flavors share the anatomy: first-use, no-results, error.
/// </summary>
public sealed class EmptyState : StatelessComponent
{
    public EmptyState(Icon icon, string title, string? body = null)
    {
        Icon = icon;
        Title = title;
        Body = body;
    }

    /// <summary>The icon, as a NODE rather than a curated enum member — <c>Icon(Icons.Inbox)</c> or
    /// <c>Icon(MaterialSymbolsIcons.FolderOffRounded)</c>, so a pack's 16,284 glyphs are reachable
    /// here and not only the design system's own set. Its SIZE and COLOR are this component's to
    /// decide (a 48dp muted well), so only the glyph travels.</summary>
    public Icon Icon { get; init; }
    public string Title { get; init; }
    public string? Body { get; init; }
    public Button? Action { get; init; }
    public Button? SecondaryAction { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        // The theme's Badge slot, then the component's own (§3.10).
        var slot = theme.Badge;
        var wellSize = slot?.WellSize ?? 64;

        var wellContent = new Icon(Icon.Glyph, slot?.WellIconSize ?? IconSize.Lg, slot?.WellIconColor ?? theme.TextMuted)
            .Centered();
        var well = new Box(new BoxStyle
        {
            Width = wellSize,
            Height = wellSize,
            Background = slot?.WellFill ?? theme.SurfaceSubtle,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Full)),
        }, wellContent);

        var column = new Column(gap: 0)
        {
            Width = SizeValue.Fill,
            Cross = CrossAlign.Center,
            Padding = EdgeInsets.Symmetric(slot?.EmptyPaddingX ?? Space.S4, slot?.EmptyPaddingY ?? Space.S12),
        };
        column.Add(well);
        column.Add(Spacer.Fixed(slot?.WellGap ?? Space.S4));
        column.Add(new Text(Title, TypeRole.Title, slot?.EmptyTitleColor ?? theme.TextPrimary, maxLines: 2)
        {
            StyleOverride = new TypeStyle(slot?.EmptyTitleSize ?? 20, slot?.EmptyTitleLineHeight ?? 26,
                slot?.EmptyTitleWeight ?? FontWeight.SemiBold, 0, 1.3f),
        });
        if (Body is { } body)
        {
            column.Add(Spacer.Fixed(slot?.BodyGap ?? 6));
            column.Add(new Text(body, TypeRole.BodyM, slot?.EmptyBodyColor ?? theme.TextSecondary, maxLines: 2));
        }
        if (Action is not null || SecondaryAction is not null)
        {
            column.Add(Spacer.Fixed(slot?.ActionsGap ?? Space.S5));
            var actions = new Row(gap: slot?.ActionSpacing ?? Space.S2) { Main = MainAlign.Center };
            if (Action is { } action) actions.Add(action);
            if (SecondaryAction is { } secondary) actions.Add(secondary);
            column.Add(actions);
        }
        return column;
    }
}

/// <summary>Skeleton shapes (spec B16).</summary>
public enum SkeletonShape : byte
{
    /// <summary>12dp bar, Radius.Full.</summary>
    Line = 0,
    /// <summary>Avatar-sized circle.</summary>
    Circle = 1,
    /// <summary>Radius.Md block.</summary>
    Block = 2,
}

/// <summary>
/// The design system's Skeleton (spec B16): SurfaceSubtle placeholders that mirror the real
/// layout's dimensions — content must replace them with ZERO shift — under the translating shimmer:
/// a split 2-stop gradient layer (transparent→highlight | highlight→transparent) sweeping
/// -100%→100% on the 1.4s clock, clipped by the placeholder's own rrect. Reduce Motion hides the
/// decorative layer entirely (HideAtRest) — the spec's plain-SurfaceSubtle behavior.
/// </summary>
public sealed class Skeleton : StatelessComponent
{
    /// <summary>Spec B16 shimmer clock: every skeleton shares the phase (one document/frame clock).</summary>
    private const int ShimmerDurationMs = 1400;

    public Skeleton(SkeletonShape shape, float width, float height = 0)
    {
        Shape = shape;
        Width = width;
        Height = height;
    }

    public SkeletonShape Shape { get; init; }
    public float Width { get; init; }
    public float Height { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        // The progress slot (docs/ELEMENT-TREE-PLAN.md §3.10): each value the theme names, or today's.
        var slot = theme.Progress;
        var highlight = slot?.SkeletonHighlight ?? theme.SurfaceHighlight;
        var height = Shape switch
        {
            SkeletonShape.Line => slot?.SkeletonLineHeight ?? 12f,
            SkeletonShape.Circle => Width,
            _ => Height > 0 ? Height : Width,
        };
        var radius = Shape switch
        {
            SkeletonShape.Line => theme.Shape(ShapeScale.Full),
            SkeletonShape.Circle => theme.Shape(ShapeScale.Full),
            _ => theme.Shape(slot?.SkeletonShape ?? ShapeScale.Medium),
        };

        // The glint: two mirrored 2-stop gradients make the symmetric highlight band inside the
        // engine fence (no 3-stop gradients exist). The layer is full-size, so LoopMotion's
        // own-width fractions equal placeholder fractions.
        var glint = new Row(gap: 0) { Width = SizeValue.Fill, Height = height };
        glint.Add(new Flexible(new Box(new BoxStyle
        {
            Height = height,
            Gradient = new LinearGradient(new ColorToken(Color.Transparent), highlight),
        }), 1));
        glint.Add(new Flexible(new Box(new BoxStyle
        {
            Height = height,
            Gradient = new LinearGradient(highlight, new ColorToken(Color.Transparent)),
        }), 1));

        return new Box(new BoxStyle
        {
            Width = Width,
            Height = height,
            Background = slot?.SkeletonFill ?? theme.SurfaceSubtle,
            CornerRadius = new CornerRadii(radius),
            Clip = true,
        }, new LoopMotion(glint, LoopEffect.SlideX, -1f, 1f, ShimmerDurationMs) { HideAtRest = true });
    }
}
