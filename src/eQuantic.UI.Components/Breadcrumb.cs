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
    public Breadcrumb(IReadOnlyList<Crumb> crumbs)
    {
        Crumbs = crumbs;
    }

    public IReadOnlyList<Crumb> Crumbs { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        // The theme's Navigation slot, then the component's own (§3.10).
        var slot = theme.Navigation;
        var row = new Row(gap: slot?.CrumbGap ?? Space.S1) { Cross = CrossAlign.Center };

        for (var i = 0; i < Crumbs.Count; i++)
        {
            var crumb = Crumbs[i];
            var last = i == Crumbs.Count - 1;

            if (i > 0)
                row.Add(new Icon(Icons.ChevronRight, slot?.SeparatorSize ?? IconSize.Sm,
                    slot?.SeparatorColor ?? theme.BorderStrong));

            var text = new Text(crumb.Label, TypeRole.Caption,
                last ? slot?.CurrentCrumbColor ?? theme.TextPrimary : slot?.CrumbColor ?? theme.TextSecondary,
                maxLines: 1);

            row.Add(!last && crumb.Destination is { } destination
                ? new Link(destination, text) { Label = crumb.Label }
                : text);
        }

        return row;
    }
}
