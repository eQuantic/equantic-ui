using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// A bounded numeric field driven by two buttons (spec B14), CONTROLLED: the caller owns
/// <see cref="Value"/> and applies <see cref="OnChanged"/>. The frame is a control of
/// <see cref="Size"/> — height, radius and label size come from <see cref="Sizing"/>, so a Stepper
/// lines up with the TextInput beside it. The value reads in TABULAR figures: incrementing must not
/// make the row jiggle. An end of the range disables its button rather than clamping silently, so
/// the limit is visible before it is hit.
/// </summary>
public sealed class Stepper : StatelessComponent
{
    public Stepper(int value, Action<int>? onChanged = null)
    {
        Value = value;
        OnChanged = onChanged;
    }

    public int Value { get; init; }
    public Action<int>? OnChanged { get; init; }

    public int Min { get; init; } = int.MinValue;
    public int Max { get; init; } = int.MaxValue;
    public int Step { get; init; } = 1;
    public SizeVariant Size { get; init; } = SizeVariant.Medium;
    public bool Disabled { get; init; }

    /// <summary>Rendered after the number ("2 GB", "3×") — part of the value, not a separate label.</summary>
    public string Suffix { get; init; } = "";

    /// <summary>Announced by the control; the number alone says nothing about what it counts.</summary>
    public string Label { get; init; } = "";

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var height = Sizing.Height(Size);
        var canDecrement = !Disabled && Value - Step >= Min;
        var canIncrement = !Disabled && Value + Step <= Max;

        var row = new Row(gap: 0) { Height = SizeValue.Fill, Cross = CrossAlign.Center };
        row.Add(Arm(theme, Icons.Minus, height, canDecrement,
            canDecrement ? () => OnChanged?.Invoke(Value - Step) : null, $"Decrease {Label}"));

        row.Add(new Flexible(new Text($"{Value}{Suffix}", TypeRole.Label,
            Disabled ? theme.TextMuted : theme.TextPrimary, maxLines: 1)
        {
            Align = TextAlignment.Center,
            Tabular = true,
            StyleOverride = theme.Type(TypeRole.Label) with { Size = Sizing.LabelSize(Size) },
        }, 1));

        row.Add(Arm(theme, Icons.Plus, height, canIncrement,
            canIncrement ? () => OnChanged?.Invoke(Value + Step) : null, $"Increase {Label}"));

        return new Box(new BoxStyle
        {
            Height = height,
            MinWidth = height * 3,
            Background = Disabled ? theme.SurfaceSubtle : theme.Surface,
            CornerRadius = new CornerRadii(Sizing.Radius(Size)),
            BorderWidth = 1,
            BorderColor = theme.BorderStrong,
            Opacity = Disabled ? theme.DisabledOpacity : 1f,
        }, row);
    }

    /// <summary>One end of the control: a square press target the full height of the frame.</summary>
    private static VisualNode Arm(IAppTheme theme, Icons glyph, float height, bool enabled,
        Action? onPressed, string label)
    {
        var centered = new Row(gap: 0) { Main = MainAlign.Center, Cross = CrossAlign.Center };
        centered.Add(new Icon(glyph, IconSize.Dense, enabled ? theme.TextPrimary : theme.TextMuted));

        var box = new Box(new BoxStyle { Width = height, Height = SizeValue.Fill }, centered);

        return new Pressable(box, onPressed) { Disabled = !enabled, Label = label };
    }
}
