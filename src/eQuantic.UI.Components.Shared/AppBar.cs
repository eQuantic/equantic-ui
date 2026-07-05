using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components.Shared;

/// <summary>
/// The design system's AppBar (spec B3): a 56dp bar — title 20/600 single-line ellipsis, optional
/// leading (48×48 back IconButton by convention), up to THREE trailing Standard IconButtons
/// (overflow goes to an ActionSheet, Phase C — more than 3 throws). Horizontal padding 4dp; the
/// title gets 12dp when there is no leading slot. v1 fences: the scrolled Surface+E2 elevation
/// swap joins the scroll-linking system; safe-area top painting joins the host insets; titleAlign
/// Platform (iOS center) joins the platform services — v1 renders leading-aligned.
/// </summary>
public sealed class AppBar : StatelessComponent
{
    public AppBar(string title)
    {
        Title = title;
    }

    public string Title { get; init; }

    /// <summary>Leading slot — a back IconButton by convention (48×48).</summary>
    public VisualNode? Leading { get; init; }

    /// <summary>Up to three Standard IconButtons (spec) — more must overflow to an ActionSheet.</summary>
    public IReadOnlyList<IconButton>? Actions { get; init; }

    /// <summary>Scrolled state (owner-driven until scroll linking): Surface fill under the content.</summary>
    public bool Scrolled { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        if (Actions is { Count: > 3 })
            throw new ArgumentException("AppBar takes at most 3 actions (spec B3) — overflow belongs in an ActionSheet.");

        var row = new Row(gap: 0)
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Cross = CrossAlign.Center,
        };
        if (Leading is { } leading) row.Add(leading);

        var title = new Text(Title, TypeRole.Title, theme.TextPrimary, maxLines: 1)
        {
            StyleOverride = new TypeStyle(20, 26, FontWeight.SemiBold, 0, 1.3f),
        };
        var titlePad = new Box(new BoxStyle
        {
            Padding = new EdgeInsets(Leading is null ? Space.S3 : Space.S2, 0, Space.S2, 0),
        }, title);
        row.Add(new Flexible(titlePad));

        if (Actions is { } actions)
        {
            foreach (var action in actions) row.Add(action);
        }

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = 56,
            Padding = EdgeInsets.Symmetric(4, 0),
            Background = Scrolled ? theme.Surface : null,
        }, row);
    }
}
