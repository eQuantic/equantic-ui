using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>Chip kinds (spec B8): Filter toggles, removable Inputs, non-interactive status Tags.</summary>
public enum ChipKind : byte
{
    Filter = 0,
    Input = 1,
    Tag = 2,
}

/// <summary>
/// The design system's Chip / Tag (spec B8): height 32, Radius.Full, 12dp X padding, 13/600 label —
/// one size (chips don't scale with the Size enum). Filter selected = Primary-subtle fill + Primary
/// border; Filter unselected = outlined, BorderStrong edge and no fill; Input = SurfaceSubtle;
/// Tag = Subtle pair per variant. Filter selected shows the check (Sm 16);
/// Input close is a real glyph (20dp visual, 48dp hit through Pressable).
/// </summary>
public sealed class Chip : StatelessComponent
{
    public Chip(string label, ChipKind kind = ChipKind.Filter, bool selected = false,
        Action? onPressed = null, Action? onRemove = null)
    {
        Label = label;
        Kind = kind;
        Selected = selected;
        OnPressed = onPressed;
        OnRemove = onRemove;
    }

    public string Label { get; init; }
    public ChipKind Kind { get; init; }
    public bool Selected { get; init; }
    public Action? OnPressed { get; init; }
    public Action? OnRemove { get; init; }

    /// <summary>Status tint for Tag chips (Subtle pair); ignored by Filter (Primary-driven).</summary>
    public Variant Variant { get; init; } = Variant.Secondary;

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var primary = theme.Colors(Variant.Primary);
        var tint = theme.Colors(Variant);

        // B8's frames: an unselected filter chip is OUTLINED (no fill, a BorderStrong edge), and the
        // only chip that rests on SurfaceSubtle is the removable Input chip.
        ColorToken? fill = Kind switch
        {
            ChipKind.Filter when Selected => primary.Subtle,
            ChipKind.Filter => null,
            ChipKind.Tag => tint.Subtle,
            _ => theme.SurfaceSubtle,
        };
        var textColor = Kind switch
        {
            ChipKind.Filter when Selected => primary.OnSubtle,
            ChipKind.Tag => tint.OnSubtle,
            _ => theme.TextPrimary,
        };

        var label = new Text(Label, TypeRole.Caption, textColor, maxLines: 1)
        {
            StyleOverride = new TypeStyle(13, 16, FontWeight.SemiBold, 0, 1.3f),
        };

        var content = new Row(gap: 6) { Main = MainAlign.Center, Height = SizeValue.Fill };
        if (Kind == ChipKind.Filter && Selected)
        {
            content.Add(new Icon(Icons.Check, IconSize.Sm, textColor));
        }
        content.Add(label);
        if (Kind == ChipKind.Input && OnRemove != null)
        {
            // B8's frame draws the ✕ a tier below the label, TextMuted, so the name reads first.
            content.Add(new Pressable(new Icon(Icons.Close, IconSize.Dense, theme.TextMuted), OnRemove)
            {
                Label = SdkStrings.Remove,
            });
        }

        // §10 hover, pointer-only. B8: "outlined/quiet chips = SurfaceSubtle; filled chips =
        // fill→pressed midpoint". The outlined chip hovers where an outline Button does. Only
        // pressable kinds hover at all — a Tag is an annotation, and a cursor over a label that
        // reacts is a promise of a press that never comes.
        var hoverFill = Kind == ChipKind.Filter && OnPressed != null
            ? Selected
                ? primary.Subtle.MidpointWith(primary.Base)
                : theme.SurfaceSubtle
            : (ColorToken?)null;

        // Spec B8 pressed: "fill shift (SurfaceSubtle / Pressed-subtle)", on the chip's own surface, and
        // only for the kind that is pressed at all. The outlined chip gains SurfaceSubtle from no fill,
        // the fill its hover already shows, as an outline Button's press does (tokens.json, §10).
        var pressedFill = Kind == ChipKind.Filter && OnPressed != null
            ? Selected ? primary.Pressed.WithOpacity(0.24f) : theme.SurfaceSubtle
            : (ColorToken?)null;

        var box = new Box(new BoxStyle
        {
            Height = ControlMetrics.Height(context, SizeVariant.Small),
            Padding = EdgeInsets.Symmetric(Space.S3, 0),
            Background = fill,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Full)),
            BorderWidth = Kind == ChipKind.Filter ? 1f : 0f,
            BorderColor = Selected ? primary.Base : theme.BorderStrong,
            Hover = hoverFill is null ? null : new StyleDiff { Background = hoverFill },
            Pressed = pressedFill is null ? null : new StyleDiff { Background = pressedFill },
        }, content);

        // Filter chips toggle; Tags are static annotations; Input chips act through the remove glyph.
        return Kind == ChipKind.Filter && OnPressed != null
            ? new Pressable(box, OnPressed)
            {
                Label = Label,
                // B8: a filter chip is a TOGGLE button — "Income, filter, selected". A filter chip
                // always carries selection (that is what makes it a filter), so false is the honest
                // answer for an unselected one rather than silence.
                Selected = Selected,
            }
            : box;
    }
}
