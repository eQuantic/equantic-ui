using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>One step of a <see cref="Breadcrumb"/>. A crumb with no <see cref="Crumb.Destination"/> is not a link
/// — the last crumb is where you already are, and a link to here is a lie.</summary>
public sealed record Crumb(string Label, string? Destination = null);

/// <summary>
/// WHERE you are in a hierarchy, and the way back up (spec B20). It is not navigation between peers
/// — that is <see cref="Tabs"/> — and it is not a history trail: it shows the path, not the route
/// the user actually took.
/// <para>
/// The last crumb is the current page: rendered in TextPrimary, never a link, and announced as the
/// current location. Separators are decorative and stay out of the accessible name.
/// </para>
/// </summary>
public sealed class Breadcrumb : StatelessComponent
{
    public Breadcrumb(IReadOnlyList<Crumb> crumbs, NavigationTheme? style = null, Func<int, VisualNode>? crumbBuilder = null)
    {
        Crumbs = crumbs;
        Style = style;
        CrumbBuilder = crumbBuilder;
    }

    public IReadOnlyList<Crumb> Crumbs { get; init; }

    /// <summary>This component's own look, laid over the theme's <see cref="IAppTheme.Navigation"/> slot with
    /// <see cref="NavigationTheme.Merge"/>: a member it sets wins, one it leaves null is the theme's.</summary>
    public NavigationTheme? Style { get; init; }

    /// <summary>
    /// Draws a crumb's CONTENT, by its index in <see cref="Crumbs"/>. The breadcrumb keeps the chrome (the
    /// separators between the crumbs and an ancestor's link) and hands the content the colour and the type the
    /// crumb's own text is drawn in; an ancestor's text stays its link's name, and the current crumb is named by
    /// the text its content carries. Null draws the text.
    /// </summary>
    public Func<int, VisualNode>? CrumbBuilder { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        // The instance's style over the theme's Navigation slot, then the component's own (§3.10).
        var slot = NavigationTheme.Merge(theme.Navigation, Style);
        var row = new Row(gap: slot?.CrumbGap ?? Space.S1) { Cross = CrossAlign.Center };

        for (var i = 0; i < Crumbs.Count; i++)
        {
            var crumb = Crumbs[i];
            var last = i == Crumbs.Count - 1;

            if (i > 0)
                row.Add(new Icon(Icons.ChevronRight, slot?.SeparatorSize ?? IconSize.Sm,
                    slot?.SeparatorColor ?? theme.BorderStrong));

            var tint = last ? slot?.CurrentCrumbColor ?? theme.TextPrimary : slot?.CrumbColor ?? theme.TextSecondary;

            // The developer's content, when they drew it, in the colour and the type the crumb's text takes;
            // the crumb's text otherwise.
            VisualNode content = CrumbBuilder is { } builder
                ? new Box(new BoxStyle
                {
                    Foreground = tint,
                    Content = new ContentStyle { Role = TypeRole.Caption, MaxLines = 1 },
                }, builder(i))
                : new Text(crumb.Label, TypeRole.Caption, tint, maxLines: 1);

            row.Add(!last && crumb.Destination is { } destination
                ? new Link(destination, content) { Label = crumb.Label }
                : content);
        }

        return row;
    }
}
