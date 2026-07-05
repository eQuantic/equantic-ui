using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// The design system's Checkbox (spec B11), CONTROLLED: the owner holds the value and passes
/// <see cref="Checked"/> + <see cref="OnChanged"/>. Box 22×22 · Radius.Xs — unchecked: 2dp
/// BorderStrong; checked: Primary fill + 16dp check glyph. The WHOLE row (box + 15/400 label,
/// gap 12) is the target, hit ≥ 48 via the Pressable contract. v1 fences: the tristate dash glyph
/// and the scale-pop motion join later; Error tints the border Destructive.
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

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var primary = theme.Colors(Variant.Primary);

        var borderColor = Error ? theme.Colors(Variant.Destructive).Base : theme.BorderStrong;
        var boxContent = new Row(gap: 0) { Main = MainAlign.Center, Height = SizeValue.Fill };
        if (Checked) boxContent.Add(new Icon(Icons.Check, IconSize.Sm, primary.OnBase));

        var box = new Box(new BoxStyle
        {
            Width = 22,
            Height = 22,
            Background = Checked ? primary.Base : null,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.ExtraSmall)),
            BorderWidth = Checked ? 0f : 2f,
            BorderColor = borderColor,
        }, boxContent);

        var row = new Row(gap: Space.S3) { Cross = CrossAlign.Center };
        row.Add(box);
        if (Label is { } label)
            row.Add(new Text(label, TypeRole.BodyM, Disabled ? theme.TextMuted : theme.TextPrimary, maxLines: 2));

        return new Pressable(row, Disabled ? null : OnChanged)
        {
            Disabled = Disabled,
            Label = Label ?? (Checked ? "Checked" : "Unchecked"),
        };
    }
}
