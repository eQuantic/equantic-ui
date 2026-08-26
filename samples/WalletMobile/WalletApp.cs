using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using Microsoft.Extensions.Configuration;

namespace eQuantic.Wallet;

/// <summary>
/// eQUANTIC WALLET — the six-screen iOS sample from the handoff. No custom widgets: every screen is
/// catalog components over the shared token core, which is the whole point of the exercise.
/// <para>
/// Written in the write-once vocabulary, so the same trees serve the browser — including the two
/// gestures: the transactions list pulls to refresh and its first row swipes to reveal, both on the
/// same <see cref="Draggable"/> the vocabulary now carries.
/// </para>
/// <para>
/// SIGNPOST — this file predates the declarative factory surface and composes nodes imperatively
/// (<c>new Column</c>, <c>.Add</c>). Do not learn app authoring here: the canonical idiom is
/// factories with no <c>new</c> — injected by both SDKs — and
/// <c>DefaultUIDashboard/Screens/DeclarativeScreen.cs</c> is the proof screen.
/// </para>
/// </summary>
public sealed class WalletApp : StatefulComponent
{
    /// <summary>The device the handoff draws — a 390×844 phone with a notch and a home indicator.</summary>
    public static readonly EdgeInsets PhoneInsets = new(0, 54, 0, 34);

    private WalletScreen _screen;
    private int _tab;
    private int _filter;
    private int _tab2;
    private bool _faceId = true;
    private bool _pushAlerts;
    private float _limit = 0.4f;
    private int _cardsPerInvoice = 2;
    private int _theme;
    private string _pixKey = "marcos@ribeiro.dev";
    private bool _refreshing;
    private bool _rowOpen;

    private readonly IWalletLedger _ledger;

    /// <summary>
    /// Both arguments come from the container. The ledger is the app's own service; the
    /// configuration is the one the host already assembled from appsettings.json, the environment
    /// and the command line — so `--screen cards` opens on the cards screen with nothing parsed here.
    /// </summary>
    public WalletApp(IWalletLedger ledger, IConfiguration configuration)
    {
        _ledger = ledger;
        _screen = Enum.TryParse<WalletScreen>(configuration["screen"], ignoreCase: true, out var screen)
            ? screen
            : WalletScreen.Home;
        _tab = Math.Max(0, Array.FindIndex(WalletData.Tabs, tab => tab.Screen == _screen));
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        var body = _screen switch
        {
            WalletScreen.Home => Home(theme),
            WalletScreen.Transactions => Transactions(theme),
            WalletScreen.Detail => Detail(theme),
            WalletScreen.Settings => Settings(theme),
            WalletScreen.Cards => Cards(theme),
            _ => Onboarding(theme),
        };

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = theme.Background,
        }, body);
    }

    // ---- 01 Home ---------------------------------------------------------------------------

    private VisualNode Home(IAppTheme theme)
    {
        var greeting = new Column(gap: 0);
        greeting.Add(new Text("Good morning", TypeRole.BodyM, theme.TextMuted, maxLines: 1));
        greeting.Add(new Text(_ledger.HolderName, TypeRole.Heading, theme.TextPrimary, maxLines: 1));

        var head = new Row(gap: Space.S3) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        head.Add(new Flexible(greeting, 1));
        head.Add(new Box(new BoxStyle
        {
            Width = 44,
            Height = 44,
            Background = theme.Surface,
            CornerRadius = new CornerRadii(Radius.Md),
            BorderWidth = 1,
            BorderColor = theme.Border,
        }, new Icon(Icons.Notifications, IconSize.Md, theme.TextPrimary).Centered()));

        var filters = new Row(gap: Space.S2) { Cross = CrossAlign.Center };
        var names = new[] { "All", "Income", "Bills" };
        for (var i = 0; i < names.Length; i++)
        {
            var index = i;
            filters.Add(new Chip(names[i], ChipKind.Filter, index == _filter,
                () => SetState(() => _filter = index)));
        }

        var recentHead = new Row(gap: Space.S2) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        recentHead.Add(new Flexible(new Text("Recent", TypeRole.Title, theme.TextPrimary, maxLines: 1), 1));
        recentHead.Add(new Text("See all", TypeRole.Label, theme.LinkColor, maxLines: 1));

        var content = new Column(gap: Space.S4) { Width = SizeValue.Fill };
        content.Add(head);
        content.Add(BalanceCard(theme));
        content.Add(filters);
        content.Add(recentHead);
        content.Add(EntryCard(theme, _ledger.Recent));
        content.Add(new Banner(Variant.Warning, "Limit almost reached",
            "92% of April's transfer limit used."));

        // The SAME body, laid out twice — once under a bar and once beside a rail. Two instances
        // rather than one node used twice: a node belongs to one tree, and both variants are real
        // trees (the web emits both behind media queries, Photon lays out the one that fits).
        VisualNode Body() => new ScrollView(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.Symmetric(Space.S4, Space.S2),
        }, content));

        var phone = new Column(gap: 0) { Width = SizeValue.Fill, Height = SizeValue.Fill };
        phone.Add(new Flexible(Body(), 1));
        phone.Add(TabBar(theme));

        // Past 600dp the destinations stand up on the leading edge instead of eating a band of a
        // screen that is now wider than it is tall. Same four destinations, same handler, same
        // state: the shell follows the WINDOW, and nothing above it is told which one it got.
        var wide = new Row(gap: 0) { Width = SizeValue.Fill, Height = SizeValue.Fill };
        wide.Add(Rail(theme));
        wide.Add(new Flexible(Body(), 1));

        // The top inset only: each navigation keeps its own edge clear, so the page must not add it
        // twice — that is the composition SafeArea exists to make possible.
        return new SafeArea(new AdaptiveNode(phone, wide), SafeEdges.Top);
    }

    /// <summary>The balance, on the one gradient in the app — a wallet's single hero surface.</summary>
    private static VisualNode BalanceCard(IAppTheme theme)
    {
        var primary = theme.Colors(Variant.Primary);

        var label = new Row(gap: Space.S2) { Cross = CrossAlign.Center };
        label.Add(new Text("Total balance", TypeRole.Caption, primary.OnBase, maxLines: 1));
        label.Add(new Icon(Icons.Close, IconSize.Sm, primary.OnBase));

        var actions = new Row(gap: Space.S2) { Width = SizeValue.Fill };
        actions.Add(new Flexible(HeroAction(theme, Icons.ChevronUp, "Send"), 1));
        actions.Add(new Flexible(HeroAction(theme, Icons.Search, "Pay"), 1));

        var column = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        column.Add(label);
        column.Add(new Text(WalletData.Balance, TypeRole.Display, primary.OnBase, maxLines: 1)
        {
            Tabular = true,
        });
        column.Add(new Box(new BoxStyle { Height = Space.S2 }));
        column.Add(actions);

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S5),
            CornerRadius = new CornerRadii(Radius.Xl),
            Gradient = new LinearGradient(primary.Base, primary.Pressed, GradientDirection.ToBottomRight),
            Elevation = 2,
        }, column);
    }

    private static VisualNode HeroAction(IAppTheme theme, Icons glyph, string label)
    {
        var onPrimary = theme.Colors(Variant.Primary).OnBase;

        var row = new Row(gap: 6)
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Main = MainAlign.Center,
            Cross = CrossAlign.Center,
        };
        row.Add(new Icon(glyph, IconSize.Dense, onPrimary));
        row.Add(new Text(label, TypeRole.Label, onPrimary, maxLines: 1));

        var box = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = Sizing.Height(SizeVariant.Medium),
            Background = onPrimary.WithOpacity(0.16f),
            CornerRadius = new CornerRadii(Radius.Md),
        }, row);

        return new Pressable(box, null) { Label = label };
    }

    /// <summary>A grouped card of ledger rows, hairline-separated — the iOS list idiom.</summary>
    private static VisualNode EntryCard(IAppTheme theme, IReadOnlyList<Entry> entries)
    {
        var column = new Column(gap: 0) { Width = SizeValue.Fill };
        for (var i = 0; i < entries.Count; i++)
        {
            if (i > 0) column.Add(new Divider(DividerInset.Leading));
            column.Add(EntryRow(theme, entries[i]));
        }

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Background = theme.Surface,
            CornerRadius = new CornerRadii(Radius.Lg),
            BorderWidth = 1,
            BorderColor = theme.Border,
            Clip = true,
        }, column);
    }

    private static VisualNode EntryRow(IAppTheme theme, Entry entry)
    {
        var colors = theme.Colors(entry.Tone);

        var avatar = new Box(new BoxStyle
        {
            Width = 40,
            Height = 40,
            Background = entry.Tone == Variant.Secondary ? theme.SurfaceSubtle : colors.Subtle,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Full)),
        }, new Icon(entry.Icon, IconSize.Dense,
            entry.Tone == Variant.Secondary ? theme.TextSecondary : colors.OnSubtle).Centered());

        var text = new Column(gap: 1) { Width = SizeValue.Fill };
        text.Add(new Text(entry.Title, TypeRole.BodyM, theme.TextPrimary, maxLines: 1));
        text.Add(new Text(entry.Subtitle, TypeRole.Caption, theme.TextMuted, maxLines: 1));

        var row = new Row(gap: Space.S3)
        {
            Width = SizeValue.Fill,
            Height = 62,
            Cross = CrossAlign.Center,
        };
        row.Add(avatar);
        row.Add(new Flexible(text, 1));

        if (entry.Badge is { } badge)
        {
            row.Add(Pill(theme, badge, Variant.Warning));
        }
        else
        {
            row.Add(new Text(entry.Amount, TypeRole.Label,
                entry.Incoming ? theme.Colors(Variant.Success).OnSubtle : theme.TextPrimary, maxLines: 1)
            {
                Tabular = true,
            });
        }

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.Symmetric(Space.S4, Space.S2),
        }, row);
    }

    private static VisualNode Pill(IAppTheme theme, string text, Variant variant)
    {
        var colors = theme.Colors(variant);
        return new Box(new BoxStyle
        {
            Padding = EdgeInsets.Symmetric(Space.S2, 2),
            Background = colors.Subtle,
            CornerRadius = new CornerRadii(Radius.Sm),
        }, new Text(text, TypeRole.Caption, colors.OnSubtle, maxLines: 1));
    }

    /// <summary>The four destinations, with the bottom inset kept clear by the bar itself.</summary>
    private VisualNode TabBar(IAppTheme theme)
    {
        var bar = new BottomNavigation(
            WalletData.Tabs.Select(t => new NavItem(t.Icon, t.Label) { BadgeCount = t.Badge }).ToArray(),
            _tab,
            Go);

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Background = theme.Surface,
            BorderWidth = 1,
            BorderColor = theme.Border,
        }, new SafeArea(bar, SafeEdges.Bottom));
    }

    /// <summary>The same four destinations as a rail, for a window wide enough to give them a side
    /// of their own. It shares TabBar's items and its handler — a destination that behaved
    /// differently after a rotation would be a different app, not a wider one.</summary>
    private VisualNode Rail(IAppTheme theme)
    {
        var rail = new NavigationRail(
            WalletData.Tabs.Select(t => new NavItem(t.Icon, t.Label) { BadgeCount = t.Badge }).ToArray(),
            _tab,
            Go);

        // The rail paints its own fill and its own trailing edge (C16) — this used to wrap it in a
        // bordered Box, which drew the framework's line a second time.
        return new SafeArea(rail, SafeEdges.Start | SafeEdges.Bottom);
    }

    /// <summary>What a destination DOES, written once for both shells.</summary>
    private void Go(int index) => SetState(() =>
    {
        _tab = index;
        // Navigation NAVIGATES. Anything else is a control that lights up and lies — and the
        // destination bar is the one thing a user trusts to take them somewhere.
        _screen = WalletData.Tabs[index].Screen;
    });

    // ---- 02 Transactions -------------------------------------------------------------------

    private VisualNode Transactions(IAppTheme theme)
    {
        var bar = new AppBar("Transactions")
        {
            Leading = new IconButton(Icons.ChevronLeft, "Back"),
            Actions = [new IconButton(Icons.Search, "Filter")],
        };

        var search = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.Symmetric(Space.S3, 0),
        }, new SearchField("", null, placeholder: "Search transactions"));

        var tabs = new Tabs(["All", "Income", "Expenses", "Bills"], _tab2,
            i => SetState(() => _tab2 = i));

        var header = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        header.Add(bar);
        header.Add(search);
        header.Add(tabs);

        var list = new Column(gap: 0) { Width = SizeValue.Fill };
        list.Add(SectionLabel(theme, "Today"));

        // The first row reveals its action on a swipe; the rest are plain.
        list.Add(new SwipeableRow(EntryRow(theme, _ledger.Today[0]), "Dispute", Icons.Warning,
            () => SetState(() => _rowOpen = false))
        {
            Open = _rowOpen,
            OnOpenChanged = open => SetState(() => _rowOpen = open),
        });
        foreach (var entry in _ledger.Today.Skip(1)) list.Add(EntryRow(theme, entry));
        list.Add(SectionLabel(theme, "Yesterday"));
        foreach (var entry in _ledger.Yesterday) list.Add(EntryRow(theme, entry));
        list.Add(PendingRow(theme));

        var page = new Column(gap: 0) { Width = SizeValue.Fill, Height = SizeValue.Fill };
        page.Add(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Background = theme.Surface,
            Elevation = 2,
        }, header));
        page.Add(new Flexible(new PullToRefresh(new ScrollView(list),
            () => SetState(() => _refreshing = true))
        {
            Refreshing = _refreshing,
        }, 1));

        return new SafeArea(page);
    }

    private static VisualNode SectionLabel(IAppTheme theme, string text) =>
        new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.Symmetric(Space.S4, Space.S2),
            Background = theme.Surface,
        }, new Text(text.ToUpperInvariant(), TypeRole.Caption, theme.TextMuted, maxLines: 1));

    private static VisualNode PendingRow(IAppTheme theme)
    {
        var text = new Column(gap: 6) { Width = SizeValue.Fill };
        text.Add(new Skeleton(SkeletonShape.Line, 160));
        text.Add(new Skeleton(SkeletonShape.Line, 96));

        var row = new Row(gap: Space.S3) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        row.Add(new Skeleton(SkeletonShape.Circle, 40));
        row.Add(new Flexible(text, 1));

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.Symmetric(Space.S4, Space.S2),
        }, row);
    }

    // ---- 03 Detail + sheet -----------------------------------------------------------------

    private VisualNode Detail(IAppTheme theme)
    {
        var identity = new Column(gap: Space.S2) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        identity.Add(new Avatar("MR", SizeVariant.XLarge, "Marcos Ribeiro"));
        identity.Add(new Text("Marcos Ribeiro", TypeRole.Title, theme.TextPrimary, maxLines: 1));
        identity.Add(new Text("+ R$ 640,00", TypeRole.Display,
            theme.Colors(Variant.Success).OnSubtle, maxLines: 1) { Tabular = true });
        identity.Add(Pill(theme, "Completed", Variant.Success));

        var rows = new Column(gap: 0) { Width = SizeValue.Fill };
        for (var i = 0; i < WalletData.DetailRows.Length; i++)
        {
            if (i > 0) rows.Add(new Divider());
            var (label, value, mono) = WalletData.DetailRows[i];

            var row = new Row(gap: Space.S3)
            {
                Width = SizeValue.Fill,
                Height = 48,
                Cross = CrossAlign.Center,
            };
            row.Add(new Flexible(new Text(label, TypeRole.BodyM, theme.TextMuted, maxLines: 1), 1));
            row.Add(new Text(value, TypeRole.BodyM, theme.TextPrimary, maxLines: 1) { Mono = mono });
            rows.Add(new Box(new BoxStyle
            {
                Width = SizeValue.Fill,
                Padding = EdgeInsets.Symmetric(Space.S4, 0),
            }, row));
        }

        var content = new Column(gap: Space.S4) { Width = SizeValue.Fill };
        content.Add(new AppBar("") { Leading = new IconButton(Icons.ChevronLeft, "Back") });
        content.Add(identity);
        content.Add(Grouped(theme, rows));
        content.Add(new Accordion([
            new AccordionItem("Receipt")
            {
                Content = new Text(
                    "The PDF receipt was issued on 17 Jul and is available for 5 years under "
                    + "Brazilian retention rules.", TypeRole.BodyM, theme.TextSecondary, maxLines: 4),
            },
        ], 0));

        var page = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Padding = EdgeInsets.Symmetric(Space.S4, 0),
        }, content);

        // The sheet is OVER the page, with the scrim between: the stack IS the layering.
        var stack = new Stack();
        stack.Add(new SafeArea(page, SafeEdges.Top));
        stack.Add(new Overlay(RepeatSheet(theme)));
        return stack;
    }

    private VisualNode RepeatSheet(IAppTheme theme)
    {
        var amounts = new Row(gap: Space.S2) { Cross = CrossAlign.Center };
        amounts.Add(new Chip("R$ 100", ChipKind.Filter));
        amounts.Add(new Chip("R$ 500", ChipKind.Filter));
        amounts.Add(new Chip("R$ 640", ChipKind.Filter, selected: true));

        var body = new Column(gap: Space.S4) { Width = SizeValue.Fill };
        body.Add(new TextInput("R$ 640,00", null, "Amount"));
        body.Add(amounts);
        body.Add(new Banner(Variant.Info, "Instant transfers settle in up to 10 seconds."));
        body.Add(new Button("Confirm R$ 640,00", Variant.Primary, SizeVariant.XLarge)
        {
            Expand = true,
            Leading = CuratedIcons.Resolve(Icons.Check),
        });

        var sheet = new Column(gap: Space.S4) { Width = SizeValue.Fill };
        sheet.Add(new Text("Repeat this transfer", TypeRole.Title, theme.TextPrimary, maxLines: 1));
        sheet.Add(body);
        return new BottomSheet(new SafeArea(sheet, SafeEdges.Bottom));
    }

    private static VisualNode Grouped(IAppTheme theme, VisualNode child) =>
        new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Background = theme.Surface,
            CornerRadius = new CornerRadii(Radius.Lg),
            BorderWidth = 1,
            BorderColor = theme.Border,
            Clip = true,
        }, child);

    // ---- 04 Settings -----------------------------------------------------------------------

    private VisualNode Settings(IAppTheme theme)
    {
        var account = new Row(gap: Space.S3) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        account.Add(new Avatar("AB", SizeVariant.Large, "Ana Beatriz Nogueira"));
        var who = new Column(gap: 1) { Width = SizeValue.Fill };
        who.Add(new Text("Ana Beatriz Nogueira", TypeRole.BodyM, theme.TextPrimary, maxLines: 1));
        who.Add(new Text("Business account · verified", TypeRole.Caption, theme.TextMuted, maxLines: 1));
        account.Add(new Flexible(who, 1));
        account.Add(new Icon(Icons.ChevronRight, IconSize.Dense, theme.TextMuted));

        var security = new Column(gap: 0) { Width = SizeValue.Fill };
        security.Add(SettingRow(theme, Icons.Person, "Face ID",
            new Switch(_faceId, () => SetState(() => _faceId = !_faceId))));
        security.Add(new Divider(DividerInset.Leading));
        security.Add(SettingRow(theme, Icons.Notifications, "Push alerts",
            new Switch(_pushAlerts, () => SetState(() => _pushAlerts = !_pushAlerts))));
        security.Add(new Divider(DividerInset.Leading));
        security.Add(SettingRow(theme, Icons.Search, "Change password",
            new Icon(Icons.ChevronRight, IconSize.Dense, theme.TextMuted)));

        var limitHead = new Row(gap: Space.S2) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        limitHead.Add(new Flexible(new Text("Daily transfer limit", TypeRole.BodyM, theme.TextPrimary,
            maxLines: 1), 1));
        limitHead.Add(new Text($"R$ {_limit * 10000:0}", TypeRole.BodyM, theme.TextPrimary, maxLines: 1)
        {
            Mono = true,
            Tabular = true,
        });

        var ends = new Row(gap: Space.S2) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        ends.Add(new Flexible(new Text("R$ 0", TypeRole.Caption, theme.TextMuted, maxLines: 1), 1));
        ends.Add(new Text("R$ 10.000", TypeRole.Caption, theme.TextMuted, maxLines: 1));

        var cards = new Row(gap: Space.S3) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        cards.Add(new Flexible(new Text("Cards per invoice", TypeRole.BodyM, theme.TextPrimary,
            maxLines: 1), 1));
        cards.Add(new Stepper(_cardsPerInvoice, v => SetState(() => _cardsPerInvoice = v))
        {
            Min = 1,
            Max = 5,
            Size = SizeVariant.Small,
            Label = "Cards per invoice",
        });

        var limits = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        limits.Add(limitHead);
        limits.Add(new Slider(_limit, v => SetState(() => _limit = v))
        {
            Step = 0.05f,
            Label = "Daily transfer limit",
        });
        limits.Add(ends);
        limits.Add(new Divider());
        limits.Add(cards);

        var appearance = new Row(gap: Space.S3) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        appearance.Add(new Flexible(new Text("Theme", TypeRole.BodyM, theme.TextPrimary, maxLines: 1), 1));
        appearance.Add(new SegmentedControl(["Light", "Dark"], _theme,
            i => SetState(() => _theme = i))
        {
            Size = SizeVariant.Small,
            Stretch = false,
        });

        var content = new Column(gap: Space.S5) { Width = SizeValue.Fill };
        content.Add(new Text("Settings", TypeRole.Heading, theme.TextPrimary, maxLines: 1));
        content.Add(Grouped(theme, new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S3),
        }, account)));
        content.Add(Section(theme, "Security", Grouped(theme, security)));
        content.Add(Section(theme, "Limits", Grouped(theme, new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S4),
        }, limits))));
        content.Add(Section(theme, "Appearance", Grouped(theme, new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S4),
        }, appearance))));
        content.Add(new Button("Sign out", Variant.Outline) { Expand = true });

        return new SafeArea(new ScrollView(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.Symmetric(Space.S4, Space.S3),
        }, content)));
    }

    private static VisualNode Section(IAppTheme theme, string label, VisualNode child)
    {
        var column = new Column(gap: 6) { Width = SizeValue.Fill };
        column.Add(new Text(label.ToUpperInvariant(), TypeRole.Caption, theme.TextMuted, maxLines: 1));
        column.Add(child);
        return column;
    }

    private static VisualNode SettingRow(IAppTheme theme, Icons glyph, string label, VisualNode trailing)
    {
        var row = new Row(gap: Space.S3)
        {
            Width = SizeValue.Fill,
            Height = 52,
            Cross = CrossAlign.Center,
        };
        row.Add(new Icon(glyph, IconSize.Md, theme.TextSecondary));
        row.Add(new Flexible(new Text(label, TypeRole.BodyM, theme.TextPrimary, maxLines: 1), 1));
        row.Add(trailing);

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.Symmetric(Space.S4, 0),
        }, row);
    }

    // ---- 05 Cards: empty + dialog + toast ---------------------------------------------------

    private VisualNode Cards(IAppTheme theme)
    {
        var empty = new EmptyState(Icons.Mail, "No cards yet",
            "Add a physical or virtual card to start paying with eQuantic Wallet.")
        {
            Action = new Button("Add card", Variant.Primary)
            {
                Leading = CuratedIcons.Resolve(Icons.Plus),
            },
            SecondaryAction = new Button("Learn about virtual cards", Variant.Link),
        };

        var page = new Column(gap: 0) { Width = SizeValue.Fill, Height = SizeValue.Fill };
        page.Add(new AppBar("Cards")
        {
            Leading = new IconButton(Icons.ChevronLeft, "Back"),
            Actions = [new IconButton(Icons.Plus, "Add card")],
        });
        page.Add(new Flexible(empty, 1));
        page.Add(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S4),
        }, new Toast("Card removed", Variant.Info) { ActionLabel = "Undo" }));

        var stack = new Stack();
        stack.Add(new SafeArea(page));
        stack.Add(new Overlay(new Dialog("Delete card?",
            "\"Work Visa ··· 4821\" will be removed and its automatic payments will stop.",
            [
                new DialogAction("Keep card", () => { }),
                new DialogAction("Delete", () => { }, Variant.Destructive),
            ])));
        return stack;
    }

    // ---- 06 Onboarding step ----------------------------------------------------------------

    private VisualNode Onboarding(IAppTheme theme)
    {
        var head = new Row(gap: Space.S2) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        head.Add(new IconButton(Icons.Close, "Cancel"));
        head.Add(new Spacer(1));
        head.Add(new Text("Step 2 of 4", TypeRole.Caption, theme.TextMuted, maxLines: 1));

        var people = new Row(gap: Space.S4) { Cross = CrossAlign.Start };
        foreach (var (initials, name) in WalletData.RecentPeople)
        {
            var person = new Column(gap: 6) { Cross = CrossAlign.Center };
            person.Add(new Avatar(initials, SizeVariant.Large, name));
            person.Add(new Text(name, TypeRole.Caption, theme.TextPrimary, maxLines: 1));
            people.Add(person);
        }

        var content = new Column(gap: Space.S4) { Width = SizeValue.Fill };
        content.Add(head);
        content.Add(new ProgressBar(0.5f));
        content.Add(new Text("Who are you\nsending to?", TypeRole.Heading, theme.TextPrimary, maxLines: 2));
        content.Add(new TextInput(_pixKey, v => SetState(() => _pixKey = v), "PIX key",
            helper: "Email, phone, CPF/CNPJ or random key"));
        content.Add(new Text("RECENT", TypeRole.Caption, theme.TextMuted, maxLines: 1));
        content.Add(people);
        content.Add(new Spacer(1));
        content.Add(new Button("Continue", Variant.Primary, SizeVariant.XLarge) { Expand = true });

        return new SafeArea(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Padding = EdgeInsets.Symmetric(Space.S4, Space.S2),
        }, content));
    }
}
