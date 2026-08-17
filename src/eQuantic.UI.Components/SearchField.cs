using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// The design system's SearchField (spec B10): one size — a 40dp Radius.Full pill, SurfaceSubtle
/// fill, no border (E0) — leading search glyph (Md, TextMuted), BodyM entry, and a clear button
/// while the query is non-empty (clears through <see cref="OnChanged"/> — the controlled model).
/// Enter fires <see cref="OnSubmit"/> immediately. v1 fences: the focused Cancel slide-in rides the
/// state-transition motion system; the 300ms onChanged debounce is the app's until a shared timer
/// primitive exists. Don't add labels/helpers; don't use it as a generic TextInput.
/// </summary>
public sealed class SearchField : StatelessComponent
{
    // The default cannot live IN the signature: a default parameter is a compile-time constant,
    // and the placeholder is SDK vocabulary the localization seam owns (I18N-PLAN D14).
    public SearchField(string query, Action<string>? onChanged = null,
        string? placeholder = null, Action? onSubmit = null)
    {
        Query = query;
        OnChanged = onChanged;
        Placeholder = placeholder ?? SdkStrings.SearchPlaceholder;
        OnSubmit = onSubmit;
    }

    public string Query { get; init; }
    public Action<string>? OnChanged { get; init; }
    public string Placeholder { get; init; }
    public Action? OnSubmit { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        // Fill the pill's content box so Cross=Center truly centers within the 40dp frame.
        var row = new Row(gap: 10) { Height = SizeValue.Fill, Cross = CrossAlign.Center };
        row.Add(new Icon(Icons.Search, IconSize.Dense, theme.TextMuted)); // spec B10: Md-20 glyph
        row.Add(new Flexible(new TextEntry(Query, OnChanged)
        {
            Placeholder = Placeholder,
            // The pill has no visible label, so the placeholder text is PROMOTED to the real
            // accessible name — a placeholder alone vanishes under text and names nothing.
            Label = Placeholder.Length > 0 ? Placeholder : null,
            OnSubmit = OnSubmit,
            Role = TypeRole.BodyM,
        }, 1));
        if (Query.Length > 0)
        {
            // B10: "glyph 20 in Full circle, hit 48". It was a bare Pressable around the glyph, so
            // the hit rect WAS the 20dp glyph — a target you have to aim at, on the one control a
            // one-handed user pokes at while walking.
            //
            // The circle takes the pill's whole height and, horizontally, the §08 minimum. The
            // vertical falls 8dp short of 48 because the pill is 40 tall and nothing can escape it:
            // Touch.MinTarget's own doc says "the framework expands hit-slop symmetrically" and NO
            // realizer implements that — a framework-wide gap this component cannot close alone, and
            // the reason Slider and PageIndicator each size their own target by hand.
            var side = Sizing.Height(SizeVariant.Medium, context.Density);
            row.Add(new Pressable(
                new Box(new BoxStyle
                {
                    Width = Touch.MinTarget,
                    Height = side,
                    CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Full)),
                    Hover = new StyleDiff { Background = theme.SurfaceSubtle.MidpointWith(theme.Border) },
                }, new Icon(Icons.Close, IconSize.Dense, theme.TextMuted).Centered()),
                () => OnChanged?.Invoke(""))
            {
                Label = SdkStrings.ClearSearch,
                PressedBackground = theme.Border,
            });
        }

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = Sizing.Height(SizeVariant.Medium, context.Density),
            Background = theme.SurfaceSubtle,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Full)),
            Padding = EdgeInsets.Symmetric(14, 0),
        }, row);
    }
}
