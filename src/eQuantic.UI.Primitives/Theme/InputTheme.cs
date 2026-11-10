namespace eQuantic.UI.Primitives;

/// <summary>
/// The slot of the fields (<c>docs/ELEMENT-TREE-PLAN.md</c> §3.10): TextInput, SearchField, Select and
/// TimePicker, every member OPTIONAL. A member left null is the next step of the slot order: for the
/// height, the slot's own <see cref="Controls"/>, then the theme's <see cref="IAppTheme.Controls"/>,
/// then <see cref="Sizing"/>; for the rest, the component's default. The text fields (TextInput and
/// SearchField) and the pickers (Select and TimePicker) pad and space their row differently, so each
/// pair has its own member.
/// <para>
/// <em>How does Flutter solve it?</em> <c>InputDecorationTheme</c>: the field's borders, paddings and
/// label styles, nullable, on <c>ThemeData</c>.
/// </para>
/// </summary>
public sealed record InputTheme
{
    /// <summary>A ladder for fields alone, ahead of the theme's: a field's height.</summary>
    public ControlsTheme? Controls { get; init; }

    /// <summary>The text fields' horizontal padding at rest (14); a thicker focus border takes its
    /// extra width from it, so the text does not move.</summary>
    public float? PaddingX { get; init; }

    /// <summary>The pickers' horizontal padding, in the field and in each row of the panel (12).</summary>
    public float? PickerPaddingX { get; init; }

    /// <summary>The gap between a text field's glyph and its text (10).</summary>
    public float? Gap { get; init; }

    /// <summary>The gap between a picker's glyph, its value and its chevron (8).</summary>
    public float? PickerGap { get; init; }

    /// <summary>The border width at rest (1).</summary>
    public float? BorderWidth { get; init; }

    /// <summary>The text field's border width while focused (2).</summary>
    public float? FocusBorderWidth { get; init; }

    /// <summary>The border colour at rest (<see cref="IAppTheme.BorderStrong"/>).</summary>
    public ColorToken? BorderColor { get; init; }

    /// <summary>The corner radius of a bordered field (the theme's medium shape).</summary>
    public float? Radius { get; init; }

    /// <summary>The corner radius of the search field (the theme's full shape: a pill).</summary>
    public float? SearchRadius { get; init; }

    /// <summary>A bordered field's fill (<see cref="IAppTheme.Surface"/>).</summary>
    public ColorToken? Fill { get; init; }

    /// <summary>A disabled text field's fill (<see cref="IAppTheme.SurfaceSubtle"/>).</summary>
    public ColorToken? DisabledFill { get; init; }

    /// <summary>The search field's fill (<see cref="IAppTheme.SurfaceSubtle"/>).</summary>
    public ColorToken? SearchFill { get; init; }

    /// <summary>The size of a field's leading glyph (20).</summary>
    public float? IconSize { get; init; }

    /// <summary>The gap between the text field's label and its container (6).</summary>
    public float? LabelGap { get; init; }

    /// <summary>The gap between the text field and its helper or error line (5).</summary>
    public float? CaptionGap { get; init; }

    /// <summary>The type of the text field's label (<see cref="TypeRole.Label"/>).</summary>
    public TypeRole? LabelRole { get; init; }

    /// <summary>The type of the text field's helper or error line (<see cref="TypeRole.Caption"/>).</summary>
    public TypeRole? CaptionRole { get; init; }

    /// <summary>The elevation of a picker's open panel (2).</summary>
    public int? PanelElevation { get; init; }

    /// <summary>The border colour of a picker's open panel (<see cref="IAppTheme.Border"/>).</summary>
    public ColorToken? PanelBorderColor { get; init; }
}
