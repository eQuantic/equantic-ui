using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// The design system's list-detail pane (spec B4's third shape): a list beside a detail on a wide
/// window, one pane at a time on a phone. Mail, settings trees, file browsers and inboxes are all
/// this, and it is the one layout where a phone and a tablet genuinely disagree about what should be
/// on screen at all.
/// <para>
/// The app owns the DATA: it hands over how to BUILD the list and, when something is chosen, the
/// detail. This owns the ADAPTIVE RULE, which is the part every app was about to write again — and
/// would have written twice, because the two widths look like two different screens and are not.
/// </para>
/// <para>
/// Builders and not nodes, because the web mounts BOTH shapes at once and lets CSS show one. A node
/// handed to both was one component instance mounted twice — one state driving two copies, a
/// selection drawn in the hidden one — so each shape builds its own panes, the way Flutter builds
/// only the branch the width calls for. What must survive crossing <see cref="TwoPaneFrom"/> belongs
/// to the app, and the part that matters most already does: WHICH item is chosen. A pane's own state
/// (how far its list is scrolled, a draft in its detail) is each shape's own.
/// </para>
/// <para>
/// <b>Compact</b> (narrower than <see cref="TwoPaneFrom"/>): the list IS the screen while
/// <see cref="Detail"/> is null, and the detail replaces it once there is one, with a back
/// affordance when <see cref="OnBack"/> is given. <b>Wide</b>: both panes, always — the list at
/// <see cref="ListWidth"/>, a hairline, and the detail (or <see cref="Placeholder"/>) filling the
/// rest. Nothing here listens for a resize: it is one <see cref="AdaptiveNode"/>, so the web emits
/// both trees behind build-time media queries and Photon lays out the one that fits.
/// </para>
/// <para>
/// The FIRST component in the catalog that is itself adaptive, and deliberately so: an app should
/// not have to know the vocabulary of window size classes to get this layout right.
/// </para>
/// <para>
/// v1 fences, both about the COMPACT pane swap, which is a navigation and not just a re-layout:
/// keyboard focus stays where it was rather than moving into the pane that just arrived (the
/// vocabulary has no target-neutral "focus this subtree" yet — <c>Autofocus</c> is a field's
/// property), and the platform back gesture is not wired to <see cref="OnBack"/> (a host concern,
/// per shell). A visible back affordance is drawn, so the pane is never a dead end.
/// </para>
/// </summary>
public sealed class ListDetail : StatelessComponent
{
    public ListDetail(Func<VisualNode> list, Func<VisualNode>? detail = null, Action? onBack = null)
    {
        List = list;
        Detail = detail;
        OnBack = onBack;
    }

    /// <summary>Builds the list pane — usually a <see cref="ScrollView"/> of <see cref="ListItem"/>s,
    /// with the chosen one marked <see cref="ListItem.Selected"/> so it says so to a screen reader and
    /// not only to the eye. Called once per shape that shows the list, and each call must return a
    /// NEW tree.</summary>
    public Func<VisualNode> List { get; init; }

    /// <summary>
    /// Builds the chosen item's pane, or null for "nothing chosen". On a phone that is a PLACE — null
    /// means the list is the screen — and on a wide window it is only an absence, filled by
    /// <see cref="Placeholder"/>. Called once per shape, like <see cref="List"/>.
    /// </summary>
    public Func<VisualNode>? Detail { get; init; }

    /// <summary>Un-chooses, which on a phone is going back. Null draws no back affordance, for a
    /// layout that never leaves the list (a wide-only console, a permanently chosen first row).</summary>
    public Action? OnBack { get; init; }

    /// <summary>The detail's title, shown in the compact bar beside the back affordance. Null draws
    /// no bar there, for a detail that carries its own header.</summary>
    public string? Title { get; init; }

    /// <summary>The list's own title, shown above it in both shapes. Null draws no bar.</summary>
    public string? ListTitle { get; init; }

    /// <summary>The list pane's width on a wide window (dp). 360 is the Material two-pane figure and
    /// fits a two-line row without truncating the subtitle.</summary>
    public float ListWidth { get; init; } = 360;

    /// <summary>
    /// What the detail pane shows on a wide window with nothing chosen. An
    /// <see cref="EmptyState"/> by convention: a blank half-screen reads as a bug, and the
    /// alternative — choosing the first row for the app — would fire its handler for a choice the
    /// user did not make. A node rather than a builder: only the wide shape ever shows it.
    /// </summary>
    public VisualNode? Placeholder { get; init; }

    /// <summary>Where the two panes take over (dp). Defaults to the spec's Expanded class, which is
    /// where a 360dp list still leaves a readable detail beside it; override when the DESIGN
    /// disagrees, exactly as on <see cref="AdaptiveNode.ExpandedFrom"/>.</summary>
    public float TwoPaneFrom { get; init; } = WindowSizeClasses.ExpandedMinDp;

    public override VisualNode Build(ComponentContext context)
    {
        // Built per variant, never shared — the app's panes included, which is why they arrive as
        // builders: a node belongs to one tree, and both of these are real trees (the web emits
        // both, Photon lays out one).
        static VisualNode Pane(string? title, VisualNode body, IconButton? leading = null)
        {
            var column = new Column(gap: 0) { Width = SizeValue.Fill, Height = SizeValue.Fill };
            if (title is not null) column.Add(new AppBar(title) { Leading = leading });
            column.Add(new Flexible(body));
            return column;
        }

        IconButton? Back() => OnBack is null
            ? null
            : new IconButton(new Icon(Icons.ChevronLeft), SdkStrings.Back, onPressed: OnBack);

        var compact = Detail is { } chosen
            ? Pane(Title, chosen(), Back())
            : Pane(ListTitle, List());

        var wide = new Row(gap: 0) { Width = SizeValue.Fill, Height = SizeValue.Fill };
        wide.Add(new Box(new BoxStyle { Width = ListWidth, Height = SizeValue.Fill },
            Pane(ListTitle, List())));
        wide.Add(new Divider(axis: DividerAxis.Vertical));
        // Wide, there is nowhere to go BACK to — both panes are already here, and an affordance that
        // returns you to something you can see is how an adaptive layout reads as broken.
        wide.Add(new Flexible(Pane(Title, Detail is { } open ? open() : Placeholder ?? Nothing())));

        return new AdaptiveNode(compact, medium: null, expanded: wide)
        {
            ExpandedFrom = TwoPaneFrom,
        };
    }

    /// <summary>The last resort when an app gave no <see cref="Placeholder"/>: still a designed
    /// screen rather than a blank half.</summary>
    private static VisualNode Nothing() => new EmptyState(new Icon(Icons.Info), SdkStrings.NothingSelected);
}
