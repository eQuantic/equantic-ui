using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// The design system's Banner / Alert (spec B18): an inline status surface that scrolls with the
/// content it describes — Radius.Lg, the status variant's Subtle pair fill, 12/14 padding, 13dp text
/// with a bold lead-in instead of a separate title row, ≤ 2 text actions. v1 fence: the bold lead-in renders
/// as two Text nodes until rich-text spans exist.
/// </summary>
public sealed class Banner : StatelessComponent
{
    public Banner(Variant status, string title, string? body = null)
    {
        Status = status;
        Title = title;
        Body = body;
    }

    public Variant Status { get; init; }
    public string Title { get; init; }
    public string? Body { get; init; }

    /// <summary>Up to two text actions (spec) — rendered as Link buttons in the status tint.</summary>
    public Button? PrimaryAction { get; init; }
    public Button? SecondaryAction { get; init; }

    /// <summary>Dismiss affordance (spec: 20dp close, 48dp hit) — fires when the user closes the banner;
    /// the OWNER removes it from the tree (banners hold no internal state).</summary>
    public Action? OnDismiss { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var tint = context.Theme.Colors(Status);
        var glyph = Status switch
        {
            Variant.Success => Icons.CheckCircle,
            Variant.Warning => Icons.Warning,
            Variant.Destructive => Icons.Error,
            _ => Icons.Info,
        };

        var column = new Column(gap: Space.S1);
        column.Add(new Text(Title, TypeRole.Caption, tint.OnSubtle, maxLines: 2)
        {
            StyleOverride = new TypeStyle(13, 18, FontWeight.SemiBold, 0, 1.3f),
        });
        if (Body != null)
        {
            column.Add(new Text(Body, TypeRole.Caption, tint.OnSubtle, maxLines: 4)
            {
                StyleOverride = new TypeStyle(13, 18, FontWeight.Regular, 0, 1.3f),
            });
        }
        if (PrimaryAction != null || SecondaryAction != null)
        {
            var actions = new Row(gap: Space.S2);
            if (PrimaryAction != null) actions.Add(PrimaryAction);
            if (SecondaryAction != null) actions.Add(SecondaryAction);
            column.Add(actions);
        }

        // Spec B18: status icon Md 20 + gap 10, top-aligned with the lead-in.
        var content = new Row(gap: 10) { Cross = CrossAlign.Start };
        content.Add(new Icon(glyph, IconSize.Dense, tint.OnSubtle));
        content.Add(new Flexible(column));
        if (OnDismiss != null)
        {
            content.Add(new Pressable(new Icon(Icons.Close, IconSize.Dense, tint.OnSubtle), OnDismiss)
            {
                Label = SdkStrings.Dismiss,
            });
        }

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = new EdgeInsets(14, 12, 14, 12),
            Background = tint.Subtle,
            CornerRadius = new CornerRadii(context.Theme.Shape(ShapeScale.Large)),
        }, content);
    }
}
