using eQuantic.UI.Components;
using eQuantic.UI.Primitives;

namespace eQuantic.Console;

/// <summary>
/// Ten thousand rows through the write-once <see cref="ListView"/> — the browser proof that the
/// window materializes a screenful and follows the scroll. The DOM under the scroll view holds
/// ~a screenful of rows between two spacers however far you are; watch it in devtools.
/// </summary>
[Page("/rows", Title = "10 000 rows — eQuantic Console")]
public sealed class RowsScreen : StatefulComponent
{
    private const int Count = 10_000;
    private const float Extent = 44f;

    /// <summary>Whether the compact drawer is up — page state wherever the page is.</summary>
    private bool _navOpen;

    /// <summary>
    /// The frame, then this screen inside it. Every route wraps itself: there is no separate
    /// layout, and none is needed — the reconciler matches the frame position for position across
    /// a navigation, so the sidebar is never rebuilt and only the middle changes.
    /// </summary>
    public override VisualNode Build(ComponentContext context) =>
        ConsoleShell.Frame(context.Theme, "/rows", "Rows", Content(context),
            _navOpen, () => SetState(() => _navOpen = !_navOpen));

    private VisualNode Content(ComponentContext context)
    {
        var theme = context.Theme;

        var page = new Column { Width = SizeValue.Fill, Height = SizeValue.Fill };
        page.Add(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = new EdgeInsets(Space.S4, Space.S4, Space.S3, Space.S4),
        }, new Text($"{Count:N0} rows, one screenful built", TypeRole.Title, theme.TextPrimary, maxLines: 1)));
        page.Add(new Flexible(
            new ListView(Count, Extent, Row) { Width = SizeValue.Fill, Height = SizeValue.Fill }, 1));

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = theme.Background,
        }, page);
    }

    private static VisualNode Row(int index)
    {
        var theme = PhotonTheme.Instance;
        var line = new Row(gap: Space.S3) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        line.Add(new Text($"#{index:D5}", TypeRole.Caption, theme.TextMuted, maxLines: 1) { Tabular = true });
        line.Add(new Flexible(
            new Text($"Ledger entry {index} — settled the moment it scrolled into being.",
                TypeRole.BodyM, theme.TextPrimary, maxLines: 1), 1));
        line.Add(new Text($"R$ {(index * 7) % 1000},{index % 100:D2}", TypeRole.LabelSmall,
            theme.TextSecondary, maxLines: 1) { Tabular = true });

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fixed(Extent),
            Padding = new EdgeInsets(0, Space.S4, 0, Space.S4),
            BorderColor = theme.Border,
            BorderWidth = 0.5f,
        }, line);
    }
}
