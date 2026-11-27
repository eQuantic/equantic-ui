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
    public Chip(VisualNode child, ChipKind kind = ChipKind.Filter, bool selected = false,
        Action? onPressed = null, Action? onRemove = null, VisualNode? avatar = null)
    {
        Child = child;
        Avatar = avatar;
        Kind = kind;
        Selected = selected;
        OnPressed = onPressed;
        OnRemove = onRemove;
    }

    /// <summary>
    /// What the chip shows: its words, or a node of the developer's own (#1607). A <c>string</c> converts to a
    /// node, and a <see cref="Text"/> that names nothing is drawn in the chip's label style, on one line, in its
    /// text colour. A pressable chip is named by the words inside it, on both targets.
    /// </summary>
    public VisualNode Child { get; init; }

    /// <summary>
    /// Drawn before <see cref="Child"/>, at the chip's icon size and in its text colour: a person's picture, an
    /// icon (Flutter's <c>Chip.avatar</c>). A selected filter chip draws its check in this place, so the chip keeps
    /// its width when it toggles.
    /// </summary>
    public VisualNode? Avatar { get; init; }

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
        // The theme's Chip slot, then its control ladder, then the component's own (§3.10).
        var slot = theme.Chip;
        var quietFill = slot?.Fill ?? theme.SurfaceSubtle;

        // B8's frames: an unselected filter chip is OUTLINED (no fill, a BorderStrong edge), and the
        // only chip that rests on SurfaceSubtle is the removable Input chip.
        ColorToken? fill = Kind switch
        {
            ChipKind.Filter when Selected => primary.Subtle,
            ChipKind.Filter => null,
            ChipKind.Tag => tint.Subtle,
            _ => quietFill,
        };
        var textColor = Kind switch
        {
            ChipKind.Filter when Selected => primary.OnSubtle,
            ChipKind.Tag => tint.OnSubtle,
            _ => theme.TextPrimary,
        };

        // The chip's label look, which the box states for whatever the child and the avatar are (#1607): a
        // string or a bare Text is drawn as the label always was, an Icon at the size the check takes.
        var iconSize = slot?.IconSize ?? IconSize.Sm;
        var look = new ContentStyle
        {
            Role = TypeRole.Caption,
            Type = new TypeStyle(slot?.LabelSize ?? 13, slot?.LabelLineHeight ?? 16,
                slot?.LabelWeight ?? FontWeight.SemiBold, slot?.LabelTracking ?? 0, 1.3f),
            MaxLines = 1,
            IconSize = iconSize,
        };

        var content = new Row(gap: slot?.Gap ?? 6) { Main = MainAlign.Center, Height = SizeValue.Fill };
        if (Kind == ChipKind.Filter && Selected)
        {
            // The check takes the avatar's place, as Flutter's FilterChip draws it.
            content.Add(new Icon(Icons.Check, iconSize, textColor));
        }
        else if (Avatar is { } avatar)
        {
            content.Add(avatar);
        }
        content.Add(Child);
        if (Kind == ChipKind.Input && OnRemove != null)
        {
            // B8's frame draws the ✕ a tier below the label, TextMuted, so the name reads first.
            content.Add(new Pressable(new Icon(Icons.Close, slot?.RemoveIconSize ?? IconSize.Dense, theme.TextMuted), OnRemove)
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
                : quietFill
            : (ColorToken?)null;

        // Spec B8 pressed: "fill shift (SurfaceSubtle / Pressed-subtle)", on the chip's own surface, and
        // only for the kind that is pressed at all. The outlined chip gains SurfaceSubtle from no fill,
        // the fill its hover already shows, as an outline Button's press does (tokens.json, §10).
        var pressedFill = Kind == ChipKind.Filter && OnPressed != null
            ? Selected ? primary.Pressed.WithOpacity(slot?.PressOpacity ?? 0.24f) : quietFill
            : (ColorToken?)null;

        var box = new Box(new BoxStyle
        {
            Height = ControlMetrics.Height(context, SizeVariant.Small, slot?.Controls),
            Padding = EdgeInsets.Symmetric(slot?.PaddingX ?? Space.S3, 0),
            Background = fill,
            CornerRadius = new CornerRadii(slot?.Radius ?? theme.Shape(ShapeScale.Full)),
            BorderWidth = Kind == ChipKind.Filter
                ? Selected ? slot?.SelectedBorderWidth ?? 1f : slot?.OutlineWidth ?? 1f
                : 0f,
            BorderColor = Selected ? primary.Base : slot?.OutlineColor ?? theme.BorderStrong,
            Foreground = textColor,
            Content = look,
            Hover = hoverFill is null ? null : new StyleDiff { Background = hoverFill },
            Pressed = pressedFill is null ? null : new StyleDiff { Background = pressedFill },
        }, content);

        // Filter chips toggle; Tags are static annotations; Input chips act through the remove glyph.
        return Kind == ChipKind.Filter && OnPressed != null
            ? new Pressable(box, OnPressed)
            {
                // No Label: the chip is named by the words of its child, as both targets name a control that
                // states none (#1607).
                // B8: a filter chip is a TOGGLE button — "Income, filter, selected". A filter chip
                // always carries selection (that is what makes it a filter), so false is the honest
                // answer for an unselected one rather than silence.
                Selected = Selected,
            }
            : box;
    }
}
