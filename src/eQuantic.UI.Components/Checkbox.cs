using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// The design system's Checkbox (spec B11), CONTROLLED: the owner holds the value and passes
/// <see cref="Checked"/> + <see cref="OnChanged"/>. Box 22×22 · Radius.Xs — unchecked: 2dp
/// BorderStrong; checked: Primary fill + 16dp check glyph. The WHOLE row (box + 15/400 label,
/// gap 12) is the target, hit ≥ 48 via the Pressable contract. The label wraps to every line it
/// needs, never clipped, with the box level with its first line. Tristate is real: a "select all"
/// that is only partly true shows the DASH, never a tick. v1 fence: the scale-pop motion joins
/// later; Error tints the border Destructive.
/// </summary>
public sealed class Checkbox : StatelessComponent
{
    public Checkbox(bool @checked, Action? onChanged = null, string? label = null)
    {
        Checked = @checked;
        OnChanged = onChanged;
        Label = label;
    }

    public bool Checked { get; init; }
    public Action? OnChanged { get; init; }
    public string? Label { get; init; }
    public bool Disabled { get; init; }

    /// <summary>Validation state (e.g. required terms) — Destructive border while unchecked.</summary>
    public bool Error { get; init; }

    /// <summary>
    /// PARTLY true — the "select all" box over a mixed set. It fills like a checked box but shows a
    /// dash: a tick would claim every child is selected, which is exactly the thing that is not so.
    /// Pressing it is the caller's call (usually: select all, then clear). Wins over
    /// <see cref="Checked"/>, since indeterminate is a statement about the whole set.
    /// </summary>
    public bool Indeterminate { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var primary = theme.Colors(Variant.Primary);
        // The theme's Selection slot, then the component's own (§3.10); the box varies by density alone.
        var slot = theme.SelectionControls;
        var side = ControlMetrics.AtDensity(slot?.Box, context.Density) ?? Sizing.SelectionBox(context.Density);

        var borderColor = Error ? theme.Colors(Variant.Destructive).Base : slot?.BorderColor ?? theme.BorderStrong;
        var filled = Checked || Indeterminate;
        VisualNode? glyph = Indeterminate ? new Icon(Icons.Minus, IconSize.Sm, primary.OnBase)
            : Checked ? new Icon(Icons.Check, IconSize.Sm, primary.OnBase)
            : null;
        var boxContent = glyph?.Centered();

        var box = new Box(new BoxStyle
        {
            Width = side,
            Height = side,
            Background = filled ? primary.Base : null,
            CornerRadius = new CornerRadii(slot?.CheckboxRadius ?? theme.Shape(ShapeScale.ExtraSmall)),
            BorderWidth = filled ? 0f : slot?.BorderWidth ?? 2f,
            BorderColor = borderColor,
        }, boxContent);

        // The label is read whole, on as many lines as it needs: a consent cut at its second line is
        // a consent nobody read. It keeps its own width and SHRINKS, wrapping, when the row has less
        // (weight 0, shrink 1): a weight would stretch every checkbox across whatever row it sits
        // in. The box sits on its FIRST line, as Banner's glyph does (spec B18), with their centres
        // level: whichever of the two is shorter drops by half the difference, so a one-line label
        // draws as it always did.
        var row = new Row(gap: slot?.LabelGap ?? Space.S3) { Cross = CrossAlign.Start };
        if (Label is { } label)
        {
            var drop = (theme.Type(TypeRole.BodyM).LineHeight - side) / 2;
            row.Add(drop > 0 ? new Box(new BoxStyle { Padding = new EdgeInsets(0, drop, 0, 0) }, box) : box);
            VisualNode text = new Text(label, TypeRole.BodyM, Disabled ? theme.TextMuted : theme.TextPrimary);
            row.Add(new Flexible(drop < 0 ? new Box(new BoxStyle { Padding = new EdgeInsets(0, -drop, 0, 0) }, text) : text, flex: 0));
        }
        else
        {
            row.Add(box);
        }

        // The STATE goes in aria-checked, never in the name. A label-less checkbox announcing
        // "Checked" twice over was noise, and a name that CHANGED with the state read as a
        // different control to assistive tech every time it toggled.
        return new Pressable(row, Disabled ? null : OnChanged)
        {
            Disabled = Disabled,
            Role = PressableRole.Checkbox,
            Selected = Checked,
            Mixed = Indeterminate,
            Label = Label,
        };
    }
}
