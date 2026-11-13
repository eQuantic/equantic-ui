using eQuantic.UI.Components;
using eQuantic.UI.Primitives;

namespace eQuantic.Console;

/// <summary>
/// The console's frame: a sidebar of destinations on the left, a toolbar across the top, and the page
/// in between. It is the app's LAYOUT, declared once in <c>Program.cs</c> with
/// <c>UseLayout&lt;ConsoleLayout&gt;()</c>: the server renders every page inside it, and the browser
/// builds it once and keeps it across client navigations, handing it each page in turn (#1129). So
/// what the frame holds outlives the page under it: the compact drawer opened on one screen is still
/// open on the next, and the screens return their content only.
/// <para>
/// It is the WEB anatomy (a pointer-sized, dense, multi-column shell) built from the same vocabulary
/// and the same tokens the macOS Studio resolves from. Nothing here is re-derived for the browser:
/// control geometry comes from <see cref="Sizing"/>, animation from the named <see cref="Motion"/>
/// roles, and the realizer emits <c>light-dark()</c> so the page renders once for both themes.
/// </para>
/// <para>
/// The layout knows which page it shows by where the reader is, <c>Navigator.Of(context).Location</c>,
/// which is the path the app's own links name: it lights that destination and names it in the
/// breadcrumb. What a page's toolbar used to carry (the payments' search, its palette, its sandbox
/// switch and its "New payment") changes that page's state, so it is drawn by that page.
/// </para>
/// </summary>
public sealed class ConsoleLayout(VisualNode body) : StatefulComponent
{
    public const float SidebarWidth = 224;
    public const float ToolbarHeight = 56;

    // Every destination is a screen this sample actually declares. A demo whose sidebar advertises
    // routes that 404 teaches the wrong thing about the router, and it left /rows, /sheet and
    // /declarative reachable only by typing the address.
    public static readonly NavEntry[] Operate =
    [
        new(Icons.Mail, "Payments", "/", 7),
        new(Icons.Table, "Rows", "/rows"),
        new(Icons.Copy, "Spreadsheet", "/sheet"),
        new(Icons.Table, "Table", "/table"),
    ];

    public static readonly NavEntry[] Explore =
    [
        new(Icons.Plus, "Declarative", "/declarative"),
        new(Icons.Info, "Markdown", "/markdown"),
        new(Icons.Copy, "Code editor", "/code"),
        new(Icons.Copy, "Code diff", "/diff"),
        new(Icons.Refresh, "Time", "/clock"),
        // What a service on the server publishes, arriving over the page's one stream.
        new(Icons.Notifications, "Live", "/live"),
        // Declared screens that the nav never listed: reachable only by typing the address, which
        // is the same defect as advertising a route that 404s, pointing the other way.
        new(Icons.Check, "Forms", "/form"),
        new(Icons.Search, "Localization", "/i18n"),
        new(Icons.Table, "Charts", "/charts"),
        // A page with no state of its own, hosting a component that has a clock: the shape whose
        // teardown the framework used to skip.
        new(Icons.Refresh, "Ticker", "/ticker"),
        // Values a prefetch loaded on the server, which the browser has to show after it hydrates and
        // after a client navigation brings them.
        new(Icons.Info, "Server data", "/server-data"),
        // A service the container hands the page, which crosses as only what the page reads of it.
        new(Icons.Person, "Identity", "/identity"),
        // Colours a component derives in its browser-side code, which the browser holds as data.
        new(Icons.Copy, "Colors", "/colors"),
        // Files sent to the server in parts, which a Server Action reads as a stream.
        new(Icons.Plus, "Uploads", "/uploads"),
    ];

    /// <summary>Whether the compact drawer is up. The layout's, so a navigation keeps it.</summary>
    private bool _navOpen;

    /// <summary>
    /// The frame around <c>body</c>, the page.
    /// <para>
    /// The frame ADAPTS, and only the frame does. Below the expanded width class the 224dp sidebar
    /// and the wide toolbar would eat a phone whole, so two SLOTS change what they hold: the nav is
    /// docked beside the page or rides a Drawer, and the toolbar is the wide one or a slim bar with a
    /// menu trigger. The page is in neither slot: it sits once, at the same position at every width.
    /// </para>
    /// <para>
    /// That placement is the contract, and both wrong ones were measured. The page used to hang in
    /// BOTH arms of one AdaptiveNode, and the web emits every arm: one component instance mounted
    /// twice, so a selection in the code editor drew its bands in the hidden copy as well and ⌘F
    /// opened in both. Building the page once PER arm trades that for two worse bugs: the window
    /// hands a chord to the binding declared LAST, which is the expanded arm's, so ⌘F on a phone
    /// opened a find bar nobody could see, and an edit typed at one width was missing at the other,
    /// each arm holding its own document. Outside the arms there is one mount and one position, so
    /// what the page holds crosses the breakpoint. It is the cure AdaptiveNode's own remarks name:
    /// put the shared subtree in the tree once and make only the varying part adaptive.
    /// </para>
    /// </summary>
    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var (title, lit) = Destination(Navigator.Of(context).Location);
        IReadOnlyList<Crumb> crumbs = [new Crumb("Workspace", "/"), new Crumb(title)];

        // One gutter at every width: the handoff's 20. Padding is a property of the box that holds
        // the page, and AdaptiveNode varies SUBTREES: a gutter per width would put the page back
        // inside the arms.
        var main = Column(gap: 0, width: SizeValue.Fill, height: SizeValue.Fill, children: [
            AdaptiveNode(CompactBar(theme, crumbs), expanded: Toolbar(theme, crumbs)),
            Flexible(ScrollView(Box(new BoxStyle
            {
                Width = SizeValue.Fill,
                Padding = EdgeInsets.All(Space.S5),
            }, body)), 1),
        ]);

        return Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = theme.Background,
        },
        Row(gap: 0, cross: CrossAlign.Stretch, width: SizeValue.Fill, height: SizeValue.Fill, children: [
            Navigation(theme, lit),
            Flexible(main, 1),
        ]));
    }

    /// <summary>
    /// What the breadcrumb calls the page at <paramref name="location"/>, and the destination it
    /// lights. A destination of the sidebar is its own. The screens the sidebar does not list are
    /// reached from another one, and the passkeys and the recovery codes light the identity they
    /// belong to. A path no screen declares is the not-found page's, so a screen added to the app is
    /// added to the sidebar or here.
    /// </summary>
    private static (string Title, string Lit) Destination(string location)
    {
        foreach (var entry in Operate)
            if (entry.Href == location) return (entry.Label, entry.Href);
        foreach (var entry in Explore)
            if (entry.Href == location) return (entry.Label, entry.Href);

        return location switch
        {
            "/map" => ("Map", ""),
            "/passkeys" => ("Passkeys", "/identity"),
            "/recovery-codes" => ("Recovery codes", "/identity"),
            _ => ("Not found", ""),
        };
    }

    /// <summary>
    /// The nav's slot: one nav, two homes. Wide, it is docked beside the page. Narrow, it rides a
    /// Drawer, which needs no layer from the frame, because an open drawer is an Overlay laid out
    /// against the viewport and a closed one is an empty box. Each home builds its own copy of the
    /// list, since each is a mount of its own.
    /// </summary>
    private VisualNode Navigation(IAppTheme theme, string lit) =>
        AdaptiveNode(
            new Drawer(SidebarContent(theme, lit), _navOpen, () => SetState(() => _navOpen = !_navOpen))
            {
                Width = 264,
            },
            expanded: Sidebar(theme, lit));

    // ---- Toolbar ---------------------------------------------------------------------------

    /// <summary>The compact toolbar: the drawer's trigger and where the reader is.</summary>
    private VisualNode CompactBar(IAppTheme theme, IReadOnlyList<Crumb> crumbs) =>
        Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = ToolbarHeight,
            Padding = EdgeInsets.Symmetric(Space.S3, 0),
            Background = theme.Surface,
            BorderWidth = 1,
            BorderColor = theme.Border,
        },
        Row(gap: Space.S2, width: SizeValue.Fill, height: SizeValue.Fill, children: [
            IconButton(Icon(Icons.Menu), "Navigation", IconButtonKind.Standard, SizeVariant.Small,
                onPressed: () => SetState(() => _navOpen = !_navOpen)),
            Flexible(new Breadcrumb(crumbs), 1),
        ]));

    private static VisualNode Toolbar(IAppTheme theme, IReadOnlyList<Crumb> crumbs) =>
        Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = ToolbarHeight,
            Padding = EdgeInsets.Symmetric(Space.S5, 0),
            Background = theme.Surface,
            BorderWidth = 1,
            BorderColor = theme.Border,
        },
        // Height=Fill, or Cross=Center centers nothing: a hugging row inside a fixed-height Box sits
        // at its TOP, and a 40dp row in a 56dp bar reads as "glued to the top". The same contract
        // AppBar, SearchField and SegmentedControl state in the library.
        Row(gap: Space.S2, width: SizeValue.Fill, height: SizeValue.Fill, children: [
            new Breadcrumb(crumbs),
            Spacer(),
            // A Menu owns its own open/close and dismisses on an outside tap, so the frame states
            // WHAT the entries are and nothing about when the panel is up.
            new Menu(
                IconButton(Icon(Icons.Notifications), "Notifications", IconButtonKind.Standard, SizeVariant.Small),
                [new MenuItem("Julia Lemos approved #3841"), new MenuItem("Risk engine flagged #3838"),
                 new MenuItem("Webhook retried payment.settled"), new MenuItem("Dispute opened by Loja Verde")])
            {
                Placement = AnchorPlacement.BottomEnd,
            },
            new Menu(
                IconButton(Icon(Icons.Info), "Help", IconButtonKind.Standard, SizeVariant.Small),
                [new MenuItem("Documentation") { Icon = Icons.Info },
                 new MenuItem("Keyboard shortcuts") { Icon = Icons.Search },
                 new MenuItem("Contact support") { Icon = Icons.Mail }])
            {
                Placement = AnchorPlacement.BottomEnd,
            },
        ]));

    // ---- Sidebar ---------------------------------------------------------------------------

    private static VisualNode Sidebar(IAppTheme theme, string lit) =>
        Box(new BoxStyle
        {
            Width = SidebarWidth,
            Height = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S3),
            Background = theme.Surface,
            BorderWidth = 1,
            BorderColor = theme.Border,
        }, SidebarContent(theme, lit));

    /// <summary>The nav itself, homeless: the persistent rail boxes it, the compact Drawer hosts it
    /// bare. ONE list of destinations, wherever it lives.</summary>
    private static VisualNode SidebarContent(IAppTheme theme, string lit)
    {
        // Inset by S1 INSIDE the scroller, as Flutter pads a ListView rather than its parent: a
        // scroller clips at its own edge, and the focus ring is drawn 4dp outside the control, so a
        // destination flush with that edge lost both sides of its ring to the clip.
        var destinations = Column(gap: Space.S2, padding: EdgeInsets.All(Space.S1), width: SizeValue.Fill);
        destinations.Add(GroupLabel(theme, "Operate"));
        foreach (var entry in Operate) destinations.Add(NavItem(theme, entry, lit));
        destinations.Add(GroupLabel(theme, "Explore"));
        foreach (var entry in Explore) destinations.Add(NavItem(theme, entry, lit));

        // The destinations take what the brand and the footer leave, and SCROLL in it. With twenty of
        // them the rail is taller than a laptop's window, and a rail that cannot scroll overflows its
        // box: the document grew to 1034px in a 900px window, so the wheel and a link to a fragment
        // scrolled the WINDOW, and took the toolbar off the top with them. Where they fit, the share
        // pushes the footer to the bottom.
        return Column(gap: Space.S2, width: SizeValue.Fill, height: SizeValue.Fill, children: [
            Brand(theme),
            Flexible(ScrollView(destinations), 1),
            Divider(),
            Account(theme),
        ]);
    }

    private static VisualNode Brand(IAppTheme theme) =>
        Row(gap: Space.S2, width: SizeValue.Fill, children: [
            Box(new BoxStyle
            {
                Width = 26,
                Height = 26,
                Background = theme.Colors(Variant.Primary).Base,
                CornerRadius = new CornerRadii(Radius.Sm),
            }, Box(new BoxStyle
            {
                Width = 10,
                Height = 10,
                Background = theme.Colors(Variant.Success).Base,
                CornerRadius = new CornerRadii(Radius.Xs),
            }).Centered()),
            Flexible(Text("Console", TypeRole.Label, theme.TextPrimary, maxLines: 1), 1),
            Icon(Icons.ChevronDown, IconSize.Sm, theme.TextMuted),
        ]);

    private static VisualNode GroupLabel(IAppTheme theme, string text) =>
        Box(new BoxStyle { Padding = EdgeInsets.Symmetric(Space.S2, Space.S1) },
            Text(text.ToUpperInvariant(), TypeRole.Caption, theme.TextMuted, maxLines: 1));

    private static VisualNode NavItem(IAppTheme theme, NavEntry entry, string lit)
    {
        var current = entry.Href == lit;
        var primary = theme.Colors(Variant.Primary);
        var tone = current ? primary.OnSubtle : theme.TextSecondary;

        var row = Row(gap: Space.S2, width: SizeValue.Fill, height: SizeValue.Fill, children: [
            Icon(entry.Icon, IconSize.Sm, tone),
            Flexible(Text(entry.Label, TypeRole.Label, tone, maxLines: 1), 1),
        ]);
        if (entry.Badge > 0) row.Add(Badge(entry.Badge, variant: Variant.Primary));

        // The fill and the tint say "you are here" to everyone who can see them; current says it to
        // everyone else, as aria-current="page".
        return Link(entry.Href, Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = Sizing.Height(SizeVariant.Small),
            Padding = EdgeInsets.Symmetric(Space.S2, 0),
            Background = current ? primary.Subtle : null,
            CornerRadius = new CornerRadii(Sizing.Radius(SizeVariant.Small)),
            Hover = current ? null : new StyleDiff { Background = theme.SurfaceSubtle },
            Transition = TransitionSpec.Of(StyleChannels.Colors, Motion.Press),
        }, row), label: entry.Label, current: current);
    }

    private static VisualNode Account(IAppTheme theme) =>
        Row(gap: Space.S2, width: SizeValue.Fill, children: [
            Avatar("AB", SizeVariant.Small, "Ana Beatriz"),
            Flexible(Column(gap: 0, children: [
                Text("Ana Beatriz", TypeRole.Label, theme.TextPrimary, maxLines: 1),
                Text("Admin", TypeRole.Caption, theme.TextMuted, maxLines: 1),
            ]), 1),
            new Menu(
                IconButton(Icon(Icons.ChevronDown), "Account menu", IconButtonKind.Standard, SizeVariant.Small),
                [new MenuItem("Profile") { Icon = Icons.Person },
                 new MenuItem("Workspace settings") { Icon = Icons.ChevronRight },
                 new MenuItem("Sign out") { Destructive = true }])
            {
                Placement = AnchorPlacement.TopEnd,
            },
        ]);
}
