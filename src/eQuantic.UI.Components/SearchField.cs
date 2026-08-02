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
    public SearchField(string query, Action<string>? onChanged = null,
        string placeholder = "Search…", Action? onSubmit = null)
    {
        Query = query;
        OnChanged = onChanged;
        Placeholder = placeholder;
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
            OnSubmit = OnSubmit,
            Role = TypeRole.BodyM,
        }, 1));
        if (Query.Length > 0)
        {
            row.Add(new Pressable(
                new Icon(Icons.Close, IconSize.Dense, theme.TextMuted),
                () => OnChanged?.Invoke(""))
            {
                Label = "clear search",
            });
        }

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = Sizing.Height(SizeVariant.Medium),
            Background = theme.SurfaceSubtle,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Full)),
            Padding = EdgeInsets.Symmetric(14, 0),
        }, row);
    }
}
