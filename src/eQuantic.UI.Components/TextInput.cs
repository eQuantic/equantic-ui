using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// The design system's TextInput (spec B9): single-line entry in a stateful container — label above
/// (Label role, 6dp gap), Radius.Md box with 1dp BorderStrong on Surface (2dp Primary when focused,
/// padding compensates -1dp; Destructive when errored, focus keeps it), leading icon slot, and the
/// helper/error line ALWAYS reserved below (5dp gap) so error swaps never shift layout. The FIRST
/// stateful library component: focus is INTERNAL state fed by the entry's focus callbacks — the
/// positional reconciler retains the instance across the form's controlled re-renders while
/// <see cref="AdoptConfig"/> carries each fresh value in. v1 fences: keyboard hints and the trailing
/// slot (clear/eye/counter) land with IME at M4; disabled shows the SurfaceSubtle fill (the 38%
/// opacity group joins with the engine's opacity primitive).
/// </summary>
public sealed class TextInput : StatefulComponent
{
    private bool _focused;

    public TextInput(string value, Action<string>? onChanged = null, string label = "",
        string? placeholder = null, string? helper = null, string? error = null,
        Icons? leading = null, SizeVariant size = SizeVariant.Large)
    {
        if (size == SizeVariant.Small)
            throw new ArgumentOutOfRangeException(nameof(size),
                "TextInput has no Small size — text + padding can't fit 32dp (spec B9).");
        Value = value;
        OnChanged = onChanged;
        Label = label;
        Placeholder = placeholder;
        Helper = helper;
        Error = error;
        Leading = leading;
        Size = size;
    }

    public string Value { get; private set; }
    public Action<string>? OnChanged { get; private set; }
    public string Label { get; private set; }
    public string? Placeholder { get; private set; }
    public string? Helper { get; private set; }

    /// <summary>Non-null switches the container/helper to Destructive (validation is the APP's
    /// call — on blur or submit, never per keystroke on first entry; spec B9).</summary>
    public string? Error { get; private set; }

    public Icons? Leading { get; private set; }
    public SizeVariant Size { get; private set; }
    public bool Disabled { get; init; }

    /// <summary>Claims the caret when it first appears — a search overlay's field wants typing
    /// to start immediately. Honoured once, exactly as the underlying <see cref="TextEntry"/> does.</summary>
    public bool Autofocus { get; init; }
    public bool Obscure { get; init; }

    public override void AdoptConfig(UiComponent next)
    {
        if (next is not TextInput fresh) return;
        Value = fresh.Value;
        OnChanged = fresh.OnChanged;
        Label = fresh.Label;
        Placeholder = fresh.Placeholder;
        Helper = fresh.Helper;
        Error = fresh.Error;
        Leading = fresh.Leading;
        Size = fresh.Size;
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var height = Sizing.Height(Size);

        var hasError = !string.IsNullOrEmpty(Error);
        var borderColor = hasError
            ? theme.Colors(Variant.Destructive).Base
            : _focused ? theme.Colors(Variant.Primary).Base : theme.BorderStrong;
        var borderWidth = _focused ? 2f : 1f;
        var paddingX = _focused ? 13f : 14f; // spec: the 2dp focus border compensates with -1dp padding

        // Fill the container's content box so Cross=Center centers within the 48dp frame —
        // a hug-height row would sit at the top of the Box on both targets.
        var row = new Row(gap: 10) { Height = SizeValue.Fill, Cross = CrossAlign.Center };
        if (Leading is { } leading)
        {
            row.Add(new Icon(leading, IconSize.Dense, theme.TextMuted)); // spec B9: 20dp leading glyph
        }
        row.Add(new Flexible(new TextEntry(Value, OnChanged)
        {
            Placeholder = Placeholder,
            Disabled = Disabled,
            Obscure = Obscure,
            Autofocus = Autofocus,
            OnFocusChanged = focused => SetState(() => _focused = focused),
        }, 1));

        var container = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = height,
            Background = Disabled ? theme.SurfaceSubtle : theme.Surface,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Medium)),
            BorderWidth = borderWidth,
            BorderColor = borderColor,
            Padding = EdgeInsets.Symmetric(paddingX, 0),
        }, row);

        var top = new Column(gap: 6) { Width = SizeValue.Fill };
        if (Label.Length > 0) top.Add(new Text(Label, TypeRole.Label, theme.TextSecondary));
        top.Add(container);

        // The helper/error line is ALWAYS reserved — swaps never shift the form (spec B9).
        var caption = hasError ? Error! : Helper ?? "";
        var captionColor = hasError ? theme.Colors(Variant.Destructive).Base : theme.TextMuted;

        var column = new Column(gap: 5) { Width = SizeValue.Fill };
        column.Add(top);
        column.Add(new Text(caption, TypeRole.Caption, captionColor, maxLines: 1));
        return column;
    }
}
