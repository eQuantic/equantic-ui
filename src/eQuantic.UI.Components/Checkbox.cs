using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// The design system's Checkbox (spec B11), CONTROLLED: the owner holds the value and passes
/// <see cref="Checked"/> + <see cref="OnChanged"/>. Box 22×22 · Radius.Xs — unchecked: 2dp
/// BorderStrong; checked: Primary fill + 16dp check glyph. The WHOLE row (box + 15/400 label,
/// gap 12) is the target, hit ≥ 48 via the Pressable contract. Tristate is real: a "select all"
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

        var borderColor = Error ? theme.Colors(Variant.Destructive).Base : theme.BorderStrong;
        var filled = Checked || Indeterminate;
        VisualNode? glyph = Indeterminate ? new Icon(Icons.Minus, IconSize.Sm, primary.OnBase)
            : Checked ? new Icon(Icons.Check, IconSize.Sm, primary.OnBase)
            : null;
        var boxContent = glyph?.Centered();

        var box = new Box(new BoxStyle
        {
            Width = Sizing.SelectionBox(context.Density),
            Height = Sizing.SelectionBox(context.Density),
            Background = filled ? primary.Base : null,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.ExtraSmall)),
            BorderWidth = filled ? 0f : 2f,
            BorderColor = borderColor,
        }, boxContent);

        var row = new Row(gap: Space.S3) { Cross = CrossAlign.Center };
        row.Add(box);
        if (Label is { } label)
            row.Add(new Text(label, TypeRole.BodyM, Disabled ? theme.TextMuted : theme.TextPrimary, maxLines: 2));

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
