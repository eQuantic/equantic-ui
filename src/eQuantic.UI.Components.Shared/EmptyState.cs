using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components.Shared;

/// <summary>
/// The design system's EmptyState (spec B17): a centered Column — icon Xl 32 in a 64dp Radius.Full
/// SurfaceSubtle well → 16 → title 20/600 → 6 → body (15, TextSecondary, ≤ 2 lines) → 20 → one
/// Medium Button (+ optional Ghost secondary). Vertical padding S12; it centers in the space the
/// missing content would occupy. Three flavors share the anatomy: first-use, no-results, error.
/// </summary>
public sealed class EmptyState : StatelessComponent
{
    public EmptyState(Icons icon, string title, string? body = null)
    {
        Icon = icon;
        Title = title;
        Body = body;
    }

    public Icons Icon { get; init; }
    public string Title { get; init; }
    public string? Body { get; init; }
    public Button? Action { get; init; }
    public Button? SecondaryAction { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        var wellContent = new Row(gap: 0) { Main = MainAlign.Center, Height = SizeValue.Fill };
        wellContent.Add(new Icon(Icon, IconSize.Lg, theme.TextMuted));
        var well = new Box(new BoxStyle
        {
            Width = 64,
            Height = 64,
            Background = theme.SurfaceSubtle,
            CornerRadius = new CornerRadii(Radius.Full),
        }, wellContent);

        var column = new Column(gap: 0)
        {
            Width = SizeValue.Fill,
            Cross = CrossAlign.Center,
            Padding = EdgeInsets.Symmetric(Space.S4, Space.S12),
        };
        column.Add(well);
        column.Add(Spacer.Fixed(Space.S4));
        column.Add(new Text(Title, TypeRole.Title, theme.TextPrimary, maxLines: 2)
        {
            StyleOverride = new TypeStyle(20, 26, FontWeight.SemiBold, 0, 1.3f),
        });
        if (Body is { } body)
        {
            column.Add(Spacer.Fixed(6));
            column.Add(new Text(body, TypeRole.BodyM, theme.TextSecondary, maxLines: 2));
        }
        if (Action is not null || SecondaryAction is not null)
        {
            column.Add(Spacer.Fixed(Space.S5));
            var actions = new Row(gap: Space.S2) { Main = MainAlign.Center };
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
/// layout's dimensions — content must replace them with ZERO shift. v1 fence: the translating
/// shimmer gradient (single 1.4s global clock) joins the animation system; static SurfaceSubtle
/// today is also exactly the spec's Reduce Motion behavior.
/// </summary>
public sealed class Skeleton : StatelessComponent
{
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
        var height = Shape switch
        {
            SkeletonShape.Line => 12f,
            SkeletonShape.Circle => Width,
            _ => Height > 0 ? Height : Width,
        };
        var radius = Shape switch
        {
            SkeletonShape.Line => Radius.Full,
            SkeletonShape.Circle => Radius.Full,
            _ => Radius.Md,
        };
        return new Box(new BoxStyle
        {
            Width = Width,
            Height = height,
            Background = context.Theme.SurfaceSubtle,
            CornerRadius = new CornerRadii(radius),
        });
    }
}
