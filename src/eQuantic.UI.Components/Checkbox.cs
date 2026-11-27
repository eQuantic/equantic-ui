using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// The design system's Checkbox (spec B11), CONTROLLED: the owner holds the value and passes
/// <see cref="Checked"/> + <see cref="OnChanged"/>. Box 22×22 · Radius.Xs — unchecked: 2dp
/// BorderStrong; checked: Primary fill + 16dp check glyph. The WHOLE row (box + 15/400 title,
/// gap 12) is the target, hit ≥ 48 via the Pressable contract. Tristate is real: a "select all"
/// that is only partly true shows the DASH, never a tick. v1 fence: the scale-pop motion joins
/// later; Error tints the border Destructive.
/// </summary>
public sealed class Checkbox : StatelessComponent
{
    public Checkbox(bool @checked, Action? onChanged = null, VisualNode? title = null)
    {
        Checked = @checked;
        OnChanged = onChanged;
        Title = title;
    }

    public bool Checked { get; init; }
    public Action? OnChanged { get; init; }
    /// <summary>
    /// What is drawn beside the box: its words, or a node of the developer's own (#1607), Flutter's
    /// <c>CheckboxListTile.title</c>. A <c>string</c> converts to a node, and a <see cref="Text"/> that names
    /// nothing is drawn in <see cref="TypeRole.BodyM"/> on up to two lines, muted while disabled. The checkbox is
    /// named by the words inside it, on both targets.
    /// </summary>
    public VisualNode? Title { get; init; }
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

        var row = new Row(gap: slot?.LabelGap ?? Space.S3) { Cross = CrossAlign.Center };
        row.Add(box);
        if (Title is { } title) row.Add(title);

        // The title's look, stated around the row for whatever the title is (#1607): a string or a bare Text
        // is drawn as the label always was. The row stays the title's parent, so a long title still shrinks
        // and wraps inside it.
        var look = new Box(new BoxStyle
        {
            Foreground = Disabled ? theme.TextMuted : theme.TextPrimary,
            Content = new ContentStyle { Role = TypeRole.BodyM, MaxLines = 2 },
        }, row);

        // The STATE goes in aria-checked, never in the name. A label-less checkbox announcing
        // "Checked" twice over was noise, and a name that CHANGED with the state read as a
        // different control to assistive tech every time it toggled.
        return new Pressable(look, Disabled ? null : OnChanged)
        {
            Disabled = Disabled,
            Role = PressableRole.Checkbox,
            Selected = Checked,
            Mixed = Indeterminate,
            // No Label: the checkbox is named by the words of its title, as both targets name a control that
            // states none (#1607).
        };
    }
}
