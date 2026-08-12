using eQuantic.UI.Components;
using eQuantic.UI.Primitives;

namespace eQuantic.Console;

/// <summary>One entry in the console's sidebar.</summary>
public sealed record NavEntry(Icons Icon, string Label, string Href, int Badge = 0);

/// <summary>
/// What the frame's own affordances DO. The shell draws them; the page owns the state they change,
/// so it hands the frame one object instead of a queue of parameters — the shape a .NET developer
/// already reaches for.
/// </summary>
public sealed record ShellActions(
    Action OnOpenPalette,
    Action OnNewPayment,
    Action<string> OnQueryChanged,
    Action<int> OnAccountMenu,
    Action<int> OnHelp,
    string Query,
    bool Sandbox,
    Action OnToggleSandbox);

/// <summary>
/// The console's persistent frame: a sidebar of destinations on the left, a toolbar across the top,
/// and the page in between. It is the WEB anatomy — a pointer-sized, dense, multi-column shell — but
/// built from the same vocabulary and the same tokens the macOS Studio resolves from. Nothing here
/// is re-derived for the browser: control geometry comes from <see cref="Sizing"/>, animation from
/// the named <see cref="Motion"/> roles, and the realizer emits <c>light-dark()</c> so the page
/// renders once for both themes.
/// </summary>
public static class ConsoleShell
{
    public const float SidebarWidth = 224;
    public const float ToolbarHeight = 56;

    // Every destination is a screen this sample actually declares. A demo whose sidebar advertises
    // routes that 404 teaches the wrong thing about the router — and it left /rows, /sheet and
    // /declarative reachable only by typing the address.
    public static readonly NavEntry[] Operate =
    [
        new(Icons.Mail, "Payments", "/", 7),
        new(Icons.Table, "Rows", "/rows"),
        new(Icons.Copy, "Spreadsheet", "/sheet"),
    ];

    public static readonly NavEntry[] Explore =
    [
        new(Icons.Plus, "Declarative", "/declarative"),
    ];

    /// <summary>Wraps a page in the frame. <paramref name="activeHref"/> lights one destination.</summary>
    public static VisualNode Wrap(IAppTheme theme, string activeHref, IReadOnlyList<Crumb> crumbs,
        VisualNode page, ShellActions actions)
    {
        var body = new Column(gap: 0) { Width = SizeValue.Fill, Height = SizeValue.Fill };
        body.Add(Toolbar(theme, crumbs, actions));
        body.Add(new Flexible(new ScrollView(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S5),
        }, page)), 1));

        var frame = new Row(gap: 0)
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Cross = CrossAlign.Stretch,
        };
        frame.Add(Sidebar(theme, activeHref, actions));
        frame.Add(new Flexible(body, 1));

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = theme.Background,
        }, frame);
    }

    // ---- Sidebar ---------------------------------------------------------------------------

    private static VisualNode Sidebar(IAppTheme theme, string activeHref, ShellActions actions)
    {
        var column = new Column(gap: Space.S2) { Width = SizeValue.Fill, Height = SizeValue.Fill };
        column.Add(Brand(theme));
        column.Add(SearchAffordance(theme, actions.OnOpenPalette));
        column.Add(GroupLabel(theme, "Operate"));
        foreach (var entry in Operate) column.Add(NavItem(theme, entry, activeHref));
        column.Add(GroupLabel(theme, "Explore"));
        foreach (var entry in Explore) column.Add(NavItem(theme, entry, activeHref));
        column.Add(new Spacer(1));
        column.Add(SandboxCard(theme, actions.Sandbox, actions.OnToggleSandbox));
        column.Add(new Divider());
        column.Add(Account(theme, actions.OnAccountMenu));

        return new Box(new BoxStyle
        {
            Width = SidebarWidth,
            Height = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S3),
            Background = theme.Surface,
            BorderWidth = 1,
            BorderColor = theme.Border,
        }, column);
    }

    private static VisualNode Brand(IAppTheme theme)
    {
        var mark = new Box(new BoxStyle
        {
            Width = 26,
            Height = 26,
            Background = theme.Colors(Variant.Primary).Base,
            CornerRadius = new CornerRadii(Radius.Sm),
        }, new Box(new BoxStyle
        {
            Width = 10,
            Height = 10,
            Background = theme.Colors(Variant.Success).Base,
            CornerRadius = new CornerRadii(Radius.Xs),
        }).Centered());

        var row = new Row(gap: Space.S2) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        row.Add(mark);
        row.Add(new Flexible(new Text("Console", TypeRole.Label, theme.TextPrimary, maxLines: 1), 1));
        row.Add(new Icon(Icons.ChevronDown, IconSize.Sm, theme.TextMuted));
        return row;
    }

    /// <summary>
    /// The search ENTRY POINT, not a field: it opens the command palette, and it shows the shortcut
    /// that does the same thing — the fastest way in is the one that gets advertised.
    /// </summary>
    private static VisualNode SearchAffordance(IAppTheme theme, Action onOpen)
    {
        // Fill the box's content height so Cross=Center truly centers within the 32dp frame —
        // a hugging row sits at the TOP of a fixed-height Box on both targets (the same contract
        // SearchField and SegmentedControl state in the library).
        var row = new Row(gap: Space.S2)
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Cross = CrossAlign.Center,
        };
        row.Add(new Icon(Icons.Search, IconSize.Sm, theme.TextMuted));
        row.Add(new Flexible(new Text("Search", TypeRole.Caption, theme.TextMuted, maxLines: 1), 1));
        row.Add(new Text("⌘K", TypeRole.Caption, theme.TextMuted, maxLines: 1) { Mono = true });

        var box = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = Sizing.Height(SizeVariant.Small),
            Padding = EdgeInsets.Symmetric(Space.S2, 0),
            Background = theme.SurfaceSubtle,
            CornerRadius = new CornerRadii(Sizing.Radius(SizeVariant.Small)),
            Hover = new StyleDiff { Background = theme.Border },
            Transition = TransitionSpec.Of(StyleChannels.Colors, Motion.Press),
        }, row);

        return new Pressable(box, onOpen) { Label = "Search — command K" };
    }

    private static VisualNode GroupLabel(IAppTheme theme, string text) =>
        new Box(new BoxStyle { Padding = EdgeInsets.Symmetric(Space.S2, Space.S1) },
            new Text(text.ToUpperInvariant(), TypeRole.Caption, theme.TextMuted, maxLines: 1));

    private static VisualNode NavItem(IAppTheme theme, NavEntry entry, string activeHref)
    {
        var current = entry.Href == activeHref;
        var primary = theme.Colors(Variant.Primary);

        var row = new Row(gap: Space.S2)
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Cross = CrossAlign.Center,
        };
        row.Add(new Icon(entry.Icon, IconSize.Sm, current ? primary.OnSubtle : theme.TextSecondary));
        row.Add(new Flexible(new Text(entry.Label, TypeRole.Label,
            current ? primary.OnSubtle : theme.TextSecondary, maxLines: 1), 1));
        if (entry.Badge > 0) row.Add(new Badge(entry.Badge, variant: Variant.Primary));

        var box = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = Sizing.Height(SizeVariant.Small),
            Padding = EdgeInsets.Symmetric(Space.S2, 0),
            Background = current ? primary.Subtle : null,
            CornerRadius = new CornerRadii(Sizing.Radius(SizeVariant.Small)),
            Hover = current ? null : new StyleDiff { Background = theme.SurfaceSubtle },
            Transition = TransitionSpec.Of(StyleChannels.Colors, Motion.Press),
        }, row);

        return new Link(entry.Href, box) { Label = entry.Label };
    }

    /// <summary>
    /// The one switch that changes what every figure on the page MEANS, so it sits in the frame and
    /// says what it does — "no real money moves" is the only wording that stops a costly mistake.
    /// </summary>
    private static VisualNode SandboxCard(IAppTheme theme, bool sandbox, Action onToggle)
    {
        var warning = theme.Colors(Variant.Warning);

        var toggle = new Row(gap: Space.S2) { Cross = CrossAlign.Center };
        toggle.Add(new Switch(sandbox, onToggle));
        toggle.Add(new Text(sandbox ? "On" : "Off", TypeRole.Caption,
            sandbox ? warning.OnSubtle : theme.TextMuted, maxLines: 1));

        var column = new Column(gap: Space.S1) { Width = SizeValue.Fill };
        column.Add(new Text("Sandbox mode", TypeRole.Label, theme.TextPrimary, maxLines: 1));
        column.Add(new Text("No real money moves while this is on.", TypeRole.Caption,
            theme.TextMuted, maxLines: 2));
        column.Add(toggle);

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S3),
            Background = sandbox ? warning.Subtle : theme.SurfaceSubtle,
            CornerRadius = new CornerRadii(Radius.Md),
            Transition = TransitionSpec.Of(StyleChannels.Colors, Motion.State),
        }, column);
    }

    private static VisualNode Account(IAppTheme theme, Action<int> onAccountMenu)
    {
        var text = new Column(gap: 0);
        text.Add(new Text("Ana Beatriz", TypeRole.Label, theme.TextPrimary, maxLines: 1));
        text.Add(new Text("Admin", TypeRole.Caption, theme.TextMuted, maxLines: 1));

        var row = new Row(gap: Space.S2) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        row.Add(new Avatar("AB", SizeVariant.Small, "Ana Beatriz"));
        row.Add(new Flexible(text, 1));
        row.Add(new Menu(
            new IconButton(Icons.ChevronDown, "Account menu", IconButtonKind.Standard, SizeVariant.Small),
            [new MenuItem("Profile") { Icon = Icons.Person },
             new MenuItem("Workspace settings") { Icon = Icons.ChevronRight },
             new MenuItem("Sign out") { Destructive = true }],
            onAccountMenu)
        { Placement = AnchorPlacement.TopEnd });
        return row;
    }

    // ---- Toolbar ---------------------------------------------------------------------------

    private static VisualNode Toolbar(IAppTheme theme, IReadOnlyList<Crumb> crumbs, ShellActions actions)
    {
        var row = new Row(gap: Space.S2) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        row.Add(new Breadcrumb(crumbs));
        row.Add(new Spacer(1));
        row.Add(new Box(new BoxStyle { Width = 240 },
            new SearchField(actions.Query, actions.OnQueryChanged, placeholder: "Search payments…")));

        // A Menu owns its own open/close and dismisses on an outside tap, so the frame states WHAT
        // the entries are and nothing about when the panel is up.
        row.Add(new Menu(
            new IconButton(Icons.Notifications, "Notifications", IconButtonKind.Standard, SizeVariant.Small),
            [new MenuItem("Julia Lemos approved #3841"), new MenuItem("Risk engine flagged #3838"),
             new MenuItem("Webhook retried payment.settled"), new MenuItem("Dispute opened by Loja Verde")],
            index => actions.OnHelp(index))
        { Placement = AnchorPlacement.BottomEnd });

        row.Add(new Menu(
            new IconButton(Icons.Info, "Help", IconButtonKind.Standard, SizeVariant.Small),
            [new MenuItem("Documentation") { Icon = Icons.Info },
             new MenuItem("Keyboard shortcuts") { Icon = Icons.Search },
             new MenuItem("Contact support") { Icon = Icons.Mail }],
            actions.OnHelp)
        { Placement = AnchorPlacement.BottomEnd });

        row.Add(new Button("New payment", Variant.Primary, SizeVariant.Small)
        {
            Leading = CuratedIcons.Resolve(Icons.Plus),
            OnPressed = actions.OnNewPayment,
        });

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = ToolbarHeight,
            Padding = EdgeInsets.Symmetric(Space.S5, 0),
            Background = theme.Surface,
            BorderWidth = 1,
            BorderColor = theme.Border,
        }, row);
    }
}
