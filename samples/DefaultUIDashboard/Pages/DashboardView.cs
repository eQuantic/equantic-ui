using eQuantic.UI.Components;
using eQuantic.UI.Lucide;
using eQuantic.UI.Primitives;

namespace DefaultUIDashboard.Pages;

/// <summary>
/// The home page as a WRITE-ONCE SHOWROOM: a believable analytics console assembled entirely from the
/// shared (Photon-vocabulary) component library — AppBar · KPI Cards · Tabs · List · Banner · Switch ·
/// Dialog · BottomNavigation — the SAME C# the native realizer rasterizes verbatim. Nothing here knows
/// which theme is active (Photon by default; swap <c>UseTheme</c> in Program.cs to rebrand every pixel).
/// State is hoisted to the page and threaded in as parameters (the SharedCounterView pattern).
/// </summary>
public static class DashboardView
{
    public static VisualNode Build(
        int tab, int nav, int refreshes, bool realtime, bool emailAlerts,
        bool confirming, string outcome,
        Action<int> selectTab, Action<int> selectNav, Action refresh,
        Action toggleRealtime, Action toggleEmail, Action openReset, Action<string> resolveReset)
    {
        var liveUsersLabel = Thousands(1284 + refreshes * 7);

        var page = new Column(gap: 0) { Width = SizeValue.Fill };

        page.Add(new AppBar("Console")
        {
            Leading = new Icon(LucideIcons.LayoutDashboard, IconSize.Md),
            Actions =
            [
                new IconButton(Icons.Search, "Search"),
                new IconButton(Icons.Notifications, "Alerts", onPressed: refresh),
            ],
        });

        var body = new Column(gap: Space.S4) { Padding = EdgeInsets.All(Space.S4), Width = SizeValue.Fill };

        // Header — a title and the live/refresh affordance.
        var header = new Row(gap: Space.S3) { Main = MainAlign.SpaceBetween, Cross = CrossAlign.Center };
        header.Add(new Text("Overview", TypeRole.Title));
        var headerRight = new Row(gap: Space.S2) { Cross = CrossAlign.Center };
        headerRight.Add(new Chip(realtime ? "Realtime" : "Paused", ChipKind.Filter,
            selected: realtime, onPressed: toggleRealtime));
        headerRight.Add(new Button("Refresh", Variant.Outline, onPressed: refresh));
        header.Add(headerRight);
        body.Add(header);

        // KPI grid — four equal Flexible columns of Cards.
        var kpis = new Row(gap: Space.S3) { Cross = CrossAlign.Stretch };
        kpis.Add(new Flexible(StatCard("Live users", liveUsersLabel, "+12%", Variant.Success, LucideIcons.Users)));
        kpis.Add(new Flexible(StatCard("Server load", "42%", "stable", Variant.Warning, LucideIcons.Cpu, 0.42f)));
        kpis.Add(new Flexible(StatCard("Response", "12ms", "-3ms", Variant.Info, LucideIcons.Zap)));
        kpis.Add(new Flexible(StatCard("Error rate", "0.02%", "2 today", Variant.Destructive, LucideIcons.TriangleAlert)));
        body.Add(kpis);

        body.Add(new Tabs(["Overview", "Activity", "Settings"], tab, selectTab));

        body.Add(tab switch
        {
            1 => ActivityPanel(),
            2 => SettingsPanel(realtime, emailAlerts, toggleRealtime, toggleEmail, openReset, outcome),
            _ => OverviewPanel(),
        });

        if (confirming)
        {
            body.Add(new Dialog("Reset demo data?",
                "This clears the showroom's in-memory state and starts the counters over.",
                [
                    new DialogAction("Cancel", () => resolveReset("cancelled"), Variant.Ghost),
                    new DialogAction("Reset", () => resolveReset("done"), Variant.Destructive),
                ]));
        }

        page.Add(body);

        page.Add(new BottomNavigation(
        [
            new NavItem(Icons.Person, "Home"),
            new NavItem(Icons.Search, "Explore"),
            new NavItem(Icons.Notifications, "Alerts") { BadgeCount = 3 },
            new NavItem(Icons.Mail, "Inbox"),
        ], nav, selectNav));

        return page;
    }

    /// <summary>One KPI: a glyph + delta Chip header, a Heading value, a Caption label, an optional bar.</summary>
    private static VisualNode StatCard(string label, string value, string delta, Variant tone,
        IconGlyph glyph, float? progress = null)
    {
        var col = new Column(gap: Space.S2);
        var head = new Row(gap: Space.S2) { Main = MainAlign.SpaceBetween, Cross = CrossAlign.Center };
        head.Add(new Icon(glyph, IconSize.Md));
        // Spec S1 in anger: the delta chip gets a sticker tilt — group opacity + a center-anchored
        // static transform, authored once, realized as CSS here and Photon pixels on native.
        head.Add(new Box(new BoxStyle
        {
            Opacity = 0.92f,
            Transform = Transform2D.Rotate(-4f).WithScale(1.04f),
        }, new Chip(delta, ChipKind.Tag) { Variant = tone }));
        col.Add(head);
        col.Add(new Text(value, TypeRole.Heading));
        col.Add(new Text(label, TypeRole.Caption));
        if (progress is { } p) col.Add(new ProgressBar(p, tone));
        return new Card(col, CardKind.Filled) { Width = SizeValue.Fill };
    }

    private static VisualNode OverviewPanel()
    {
        var col = new Column(gap: Space.S3);
        col.Add(new Text("System health", TypeRole.Title));
        col.Add(new Banner(Variant.Success, "All systems operational", "Last incident 42 days ago."));
        col.Add(MetricRow("CPU", 0.42f, Variant.Warning));
        col.Add(MetricRow("Memory", 0.61f, Variant.Info));
        col.Add(MetricRow("Disk", 0.28f, Variant.Success));
        col.Add(new Divider());

        var team = new Row(gap: Space.S2) { Cross = CrossAlign.Center };
        team.Add(new Text("On call", TypeRole.Label));
        team.Add(new Avatar("AB", SizeVariant.Small, name: "Ana Beatriz"));
        team.Add(new Avatar("JC", SizeVariant.Small, name: "João Carlos"));
        team.Add(new Avatar("MR", SizeVariant.Small, name: "Maria Rosa"));
        col.Add(team);
        return new Card(col) { Width = SizeValue.Fill };
    }

    private static VisualNode MetricRow(string label, float value, Variant tone)
    {
        var col = new Column(gap: Space.S1);
        var head = new Row { Main = MainAlign.SpaceBetween };
        head.Add(new Text(label, TypeRole.Label));
        head.Add(new Text($"{(int)(value * 100)}%", TypeRole.Label));
        col.Add(head);
        col.Add(new ProgressBar(value, tone));
        return col;
    }

    private static VisualNode ActivityPanel()
    {
        var items = new ListItem[]
        {
            new ListItem("Deploy succeeded", "api-gateway · 2m ago")
                { Leading = new Icon(LucideIcons.Rocket, IconSize.Lg), Trailing = new Icon(Icons.ChevronRight, IconSize.Md) },
            new ListItem("128 new sign-ups", "in the last hour")
                { Leading = new Icon(LucideIcons.Users, IconSize.Lg), Trailing = new Icon(Icons.ChevronRight, IconSize.Md) },
            new ListItem("CPU spike resolved", "worker-3 · 18m ago")
                { Leading = new Icon(LucideIcons.Cpu, IconSize.Lg), Trailing = new Icon(Icons.ChevronRight, IconSize.Md) },
            new ListItem("Nightly backup completed", "snapshots · 1h ago")
                { Leading = new Icon(LucideIcons.CircleCheck, IconSize.Lg), Trailing = new Icon(Icons.ChevronRight, IconSize.Md) },
        };
        return new Card(new List(items)) { Width = SizeValue.Fill };
    }

    /// <summary>Group digits with a thin separator — kept dependency-free (no culture APIs) so it
    /// transpiles to plain JS for the client re-render.</summary>
    private static string Thousands(int n)
    {
        var s = n.ToString();
        var result = "";
        for (var i = 0; i < s.Length; i++)
        {
            if (i > 0 && (s.Length - i) % 3 == 0) result += ",";
            result += s[i];
        }
        return result;
    }

    private static VisualNode SettingsPanel(bool realtime, bool emailAlerts,
        Action toggleRealtime, Action toggleEmail, Action openReset, string outcome)
    {
        var col = new Column(gap: Space.S2);
        col.Add(new Text("Preferences", TypeRole.Title));
        col.Add(new ListItem("Realtime updates", "Stream metrics as they change")
        {
            Leading = new Icon(LucideIcons.Activity, IconSize.Lg),
            Trailing = new Switch(realtime, toggleRealtime),
        });
        col.Add(new ListItem("Email alerts", "Notify me on error-rate spikes")
        {
            Leading = new Icon(LucideIcons.Bell, IconSize.Lg),
            Trailing = new Checkbox(emailAlerts, toggleEmail),
        });
        col.Add(new Divider());

        var reset = new Row(gap: Space.S3) { Cross = CrossAlign.Center };
        reset.Add(new Button("Reset demo data", Variant.Destructive, onPressed: openReset));
        reset.Add(new Text($"outcome: {outcome}", TypeRole.Caption));
        col.Add(reset);
        return new Card(col) { Width = SizeValue.Fill };
    }
}
