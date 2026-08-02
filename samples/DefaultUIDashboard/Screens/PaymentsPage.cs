using eQuantic.UI.Components;
using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using StatefulComponent = eQuantic.UI.Primitives.StatefulComponent;

namespace eQuantic.Console;

/// <summary>
/// eQUANTIC CONSOLE — the payments screen, and the web counterpart of the macOS Studio. It exercises
/// what the browser has and a phone does not: hover on rows and controls, dense pointer-sized
/// controls, sortable headings, popover menus, pagination, multi-column layout.
/// <para>
/// Everything underneath is the shared layer. The bar chart is plain rounded rects — the one chart
/// shape inside the mobile engine's fence — so this card ports to Photon unchanged.
/// </para>
/// </summary>
[Page("/", Title = "Payments — eQuantic Console")]
public sealed class PaymentsPage : StatefulComponent
{
    private DateRange _range = DateRange.Month;
    private bool _sandbox = true;
    private int _sortColumn = 3;
    private SortDirection _sortDirection = SortDirection.Descending;
    private readonly HashSet<string> _selected = ["#3841"];
    private int _page = 1;
    private int _activityTab;
    private int _schedule;
    private bool _receipts = true;
    private int _account;

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        var content = new Column(gap: Space.S4) { Width = SizeValue.Fill };
        content.Add(Header(theme));
        content.Add(Kpis(theme));
        content.Add(ChartAndActivity(theme));
        content.Add(TableCard(theme));
        content.Add(Footer(theme));

        return ConsoleShell.Wrap(theme, "/payments",
            [new Crumb("Workspace", "/"), new Crumb("Payments")],
            content, _sandbox, () => SetState(() => _sandbox = !_sandbox));
    }

    // ---- Header ----------------------------------------------------------------------------

    private VisualNode Header(IAppTheme theme)
    {
        var title = new Column(gap: 2);
        title.Add(new Text("Payments", TypeRole.Heading, theme.TextPrimary, maxLines: 1));
        title.Add(new Text($"3.482 transactions · {ConsoleData.LabelOf(_range)}", TypeRole.BodyM,
            theme.TextMuted, maxLines: 1));

        var row = new Row(gap: Space.S3) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        row.Add(new Flexible(title, 1));
        row.Add(new SegmentedControl(["7d", "30d", "90d"], (int)_range,
            i => SetState(() => _range = (DateRange)i))
        {
            Size = SizeVariant.Small,
            Stretch = false,
        });
        row.Add(new Button("Export", Variant.Outline, SizeVariant.Small)
        {
            Leading = CuratedIcons.Resolve(Icons.ChevronDown),
        });
        return row;
    }

    // ---- KPIs ------------------------------------------------------------------------------

    private VisualNode Kpis(IAppTheme theme)
    {
        var grid = new Grid(GridTrack.Repeat(4, GridTrack.Flex()), gap: Space.S3)
        {
            Width = SizeValue.Fill,
        };
        grid.Add(Kpi(theme, $"Volume · {ConsoleData.LabelOf(_range)}", ConsoleData.VolumeOf(_range),
            Chip(theme, "▲ 12,4%", Variant.Success), "vs prev."));
        grid.Add(Kpi(theme, "Success rate", "98,2%",
            new Box(new BoxStyle { Width = SizeValue.Fill }, new ProgressBar(0.982f, Variant.Success)), null));
        grid.Add(Kpi(theme, "Pending review", "7", Chip(theme, "3 over 24h", Variant.Warning), null));
        grid.Add(Kpi(theme, "Disputes", "2", Chip(theme, "R$ 1.840 at risk", Variant.Destructive), null));
        return grid;
    }

    private static VisualNode Kpi(IAppTheme theme, string label, string value, VisualNode footer,
        string? note)
    {
        var head = new Row(gap: Space.S2) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        head.Add(new Flexible(new Text(label, TypeRole.Caption, theme.TextMuted, maxLines: 1), 1));

        var foot = new Row(gap: Space.S2) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        foot.Add(footer);
        if (note is not null) foot.Add(new Text(note, TypeRole.Caption, theme.TextMuted, maxLines: 1));

        var column = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        column.Add(head);
        column.Add(new Text(value, TypeRole.Title, theme.TextPrimary, maxLines: 1) { Tabular = true });
        column.Add(foot);

        return new Card(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S4),
        }, column))
        {
            Width = SizeValue.Fill,
        };
    }

    /// <summary>A tinted delta pill — the same Subtle/OnSubtle pair every status uses.</summary>
    private static VisualNode Chip(IAppTheme theme, string text, Variant variant)
    {
        var colors = theme.Colors(variant);
        return new Box(new BoxStyle
        {
            Padding = EdgeInsets.Symmetric(Space.S2, 2),
            Background = colors.Subtle,
            CornerRadius = new CornerRadii(Radius.Sm),
        }, new Text(text, TypeRole.Caption, colors.OnSubtle, maxLines: 1));
    }

    // ---- Chart + activity ------------------------------------------------------------------

    private VisualNode ChartAndActivity(IAppTheme theme)
    {
        var grid = new Grid([GridTrack.Flex(1.55f), GridTrack.Flex()], gap: Space.S3)
        {
            Width = SizeValue.Fill,
        };
        grid.Add(Chart(theme));
        grid.Add(Activity(theme));
        return grid;
    }

    /// <summary>
    /// Volume by day, drawn as stacked rounded rects — the ONE chart shape inside the mobile
    /// engine's fence, so this card ports to Photon unchanged. Each day is a column of two flex
    /// weights, which is what makes the proportion survive any width.
    /// </summary>
    private VisualNode Chart(IAppTheme theme)
    {
        var primary = theme.Colors(Variant.Primary).Base;
        var success = theme.Colors(Variant.Success).Base;

        var bars = new Row(gap: Space.S2)
        {
            Width = SizeValue.Fill,
            Height = 150,
            Cross = CrossAlign.Stretch,
        };
        foreach (var day in ConsoleData.Volume)
        {
            var stack = new Column(gap: 2) { Height = SizeValue.Fill };
            stack.Add(new Flexible(new Box(new BoxStyle()), Weight(1 - day.Pix - day.Card)));
            stack.Add(new Flexible(Bar(success, top: true), Weight(day.Pix)));
            stack.Add(new Flexible(Bar(primary, top: false), Weight(day.Card)));
            bars.Add(new Flexible(stack, 1));
        }

        var axis = new Row(gap: Space.S2) { Width = SizeValue.Fill };
        foreach (var day in ConsoleData.Volume)
        {
            axis.Add(new Flexible(new Text(day.Label, TypeRole.Caption, theme.TextMuted, maxLines: 1)
            {
                Align = TextAlignment.Center,
                Mono = true,
                Tabular = true,
            }, 1));
        }

        var legend = new Row(gap: Space.S3) { Cross = CrossAlign.Center };
        legend.Add(LegendKey(theme, primary, "Card"));
        legend.Add(LegendKey(theme, success, "PIX"));

        var head = new Row(gap: Space.S3) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        var titles = new Column(gap: 0);
        titles.Add(new Text("Volume by day", TypeRole.Label, theme.TextPrimary, maxLines: 1));
        titles.Add(new Text($"{ConsoleData.LabelOf(_range)} · settled payments", TypeRole.Caption,
            theme.TextMuted, maxLines: 1));
        head.Add(new Flexible(titles, 1));
        head.Add(legend);

        var column = new Column(gap: Space.S4) { Width = SizeValue.Fill };
        column.Add(head);
        column.Add(bars);
        column.Add(axis);
        column.Add(new Divider());
        column.Add(new Text(
            "Bars are plain rounded rects — the one chart shape that stays inside the mobile engine "
            + "fence, so this card ports to Photon unchanged.",
            TypeRole.Caption, theme.TextMuted, maxLines: 3));

        return new Card(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S4),
        }, column))
        {
            Width = SizeValue.Fill,
        };
    }

    /// <summary>Flex weights are integers, so a fraction of the chart's height becomes per-mille.</summary>
    private static int Weight(float fraction) => Math.Max(1, (int)MathF.Round(fraction * 1000));

    private static VisualNode Bar(ColorToken fill, bool top) =>
        new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = fill,
            CornerRadius = top ? new CornerRadii(5, 5, 0, 0) : new CornerRadii(0, 0, 5, 5),
        });

    private static VisualNode LegendKey(IAppTheme theme, ColorToken fill, string label)
    {
        var row = new Row(gap: 5) { Cross = CrossAlign.Center };
        row.Add(new Box(new BoxStyle
        {
            Width = 9,
            Height = 9,
            Background = fill,
            CornerRadius = new CornerRadii(3),
        }));
        row.Add(new Text(label, TypeRole.Caption, theme.TextMuted, maxLines: 1));
        return row;
    }

    private VisualNode Activity(IAppTheme theme)
    {
        var head = new Row(gap: Space.S2) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        head.Add(new Flexible(new Text("Activity", TypeRole.Label, theme.TextPrimary, maxLines: 1), 1));
        head.Add(new Link("/log", new Text("View log", TypeRole.Caption, theme.LinkColor, maxLines: 1)));

        var feed = new Column(gap: Space.S3) { Width = SizeValue.Fill };
        foreach (var entry in ConsoleData.Activity) feed.Add(ActivityRow(theme, entry));

        var column = new Column(gap: Space.S3) { Width = SizeValue.Fill };
        column.Add(head);
        column.Add(new Tabs(["All", "Mine", "System"], _activityTab,
            i => SetState(() => _activityTab = i)));
        column.Add(feed);
        column.Add(new Banner(Variant.Info, "Sandbox events are excluded from this feed."));

        return new Card(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S4),
        }, column))
        {
            Width = SizeValue.Fill,
        };
    }

    private static VisualNode ActivityRow(IAppTheme theme, ActivityEntry entry)
    {
        var colors = theme.Colors(entry.Tone);

        var badge = new Box(new BoxStyle
        {
            Width = 30,
            Height = 30,
            Background = colors.Subtle,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Full)),
        }, new Icon(entry.Icon, IconSize.Sm, colors.OnSubtle).Centered());

        var text = new Column(gap: 1) { Width = SizeValue.Fill };
        text.Add(new Text($"{entry.Text} {entry.Detail}", TypeRole.Caption, theme.TextPrimary,
            maxLines: 2));
        text.Add(new Text(entry.When, TypeRole.Caption, theme.TextMuted, maxLines: 1));

        var row = new Row(gap: Space.S3) { Width = SizeValue.Fill, Cross = CrossAlign.Start };
        row.Add(badge);
        row.Add(new Flexible(text, 1));
        return row;
    }

    // ---- Table -----------------------------------------------------------------------------

    private static readonly DataColumn[] Columns =
    [
        new("Customer", GridTrack.Flex(1.6f), Sortable: true),
        new("Method", GridTrack.Flex()),
        new("Date", GridTrack.Flex(), Sortable: true),
        new("Status", GridTrack.Flex(), Sortable: true),
        new("Amount", GridTrack.Fixed(140), TextAlignment.End, Sortable: true),
    ];

    private VisualNode TableCard(IAppTheme theme)
    {
        var head = new Row(gap: Space.S2) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        head.Add(new Text("Recent payments", TypeRole.Label, theme.TextPrimary, maxLines: 1));
        head.Add(Chip(theme, "3.482", Variant.Secondary));
        head.Add(new Spacer(1));
        head.Add(new Chip("Status: Pending", ChipKind.Filter, selected: true, onRemove: () => { }));
        head.Add(new Chip("Jul 11 – 17", ChipKind.Filter));
        head.Add(new Chip("More filters", ChipKind.Filter));

        var table = new DataTable(Columns, ConsoleData.Payments.Select(RowOf).ToArray())
        {
            SortColumn = _sortColumn,
            SortDirection = _sortDirection,
            OnSort = OnSort,
            Selection = _selected,
            OnToggleRow = row => SetState(() =>
            {
                if (!_selected.Add(row.Key)) _selected.Remove(row.Key);
            }),
            OnToggleAll = () => SetState(() =>
            {
                if (_selected.Count > 0) _selected.Clear();
                else foreach (var payment in ConsoleData.Payments) _selected.Add(payment.Id);
            }),
            PendingRows = 1,
        };

        var foot = new Row(gap: Space.S3) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        foot.Add(new Flexible(new Text($"{_selected.Count} of 3.482 selected", TypeRole.Caption,
            theme.TextMuted, maxLines: 1) { Tabular = true }, 1));
        foot.Add(new Pagination(70, _page, page => SetState(() => _page = page)));

        var column = new Column(gap: 0) { Width = SizeValue.Fill };
        column.Add(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S4),
        }, head));
        column.Add(table);
        column.Add(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S4),
        }, foot));

        return new Card(column) { Width = SizeValue.Fill };
    }

    /// <summary>
    /// Pressing the sorted column flips it; pressing another takes it over, descending, because the
    /// first thing anyone wants from a new column is its largest values.
    /// </summary>
    private void OnSort(int column) => SetState(() =>
    {
        if (column == _sortColumn)
        {
            _sortDirection = _sortDirection == SortDirection.Ascending
                ? SortDirection.Descending
                : SortDirection.Ascending;
            return;
        }

        _sortColumn = column;
        _sortDirection = SortDirection.Descending;
    });

    private DataRow RowOf(Payment payment)
    {
        var theme = PhotonTheme.Instance;

        var identity = new Column(gap: 0);
        identity.Add(new Text(payment.Customer, TypeRole.Caption, theme.TextPrimary, maxLines: 1));
        identity.Add(new Text(payment.Id, TypeRole.Caption, theme.TextMuted, maxLines: 1)
        {
            Mono = true,
        });

        var customer = new Row(gap: Space.S2) { Cross = CrossAlign.Center };
        customer.Add(new Avatar(payment.Initials, SizeVariant.Small, payment.Customer));
        customer.Add(identity);

        var method = new Row(gap: 6) { Cross = CrossAlign.Center };
        method.Add(new Icon(ConsoleData.MethodIcon(payment.Method), IconSize.Sm, theme.TextMuted));
        method.Add(new Text(ConsoleData.MethodLabel(payment.Method), TypeRole.Caption,
            theme.TextSecondary, maxLines: 1));

        return new DataRow(payment.Id, [
            customer,
            method,
            new Text(payment.Date, TypeRole.Caption, theme.TextSecondary, maxLines: 1),
            StatusPill(theme, payment.Status),
            new Text(payment.Amount, TypeRole.Caption, theme.TextPrimary, maxLines: 1)
            {
                Mono = true,
                Tabular = true,
            },
        ]);
    }

    /// <summary>Status carries a DOT as well as a tint — colour alone is not a status.</summary>
    private static VisualNode StatusPill(IAppTheme theme, PaymentStatus status)
    {
        var colors = theme.Colors(ConsoleData.StatusTone(status));

        var row = new Row(gap: 5) { Cross = CrossAlign.Center };
        row.Add(new Box(new BoxStyle
        {
            Width = 6,
            Height = 6,
            Background = colors.Base,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Full)),
        }));
        row.Add(new Text(ConsoleData.StatusLabel(status), TypeRole.Caption, colors.OnSubtle,
            maxLines: 1));

        return new Box(new BoxStyle
        {
            Padding = EdgeInsets.Symmetric(Space.S2, 2),
            Background = colors.Subtle,
            CornerRadius = new CornerRadii(Radius.Sm),
        }, row);
    }

    // ---- Footer ----------------------------------------------------------------------------

    private VisualNode Footer(IAppTheme theme)
    {
        var grid = new Grid(GridTrack.Repeat(2, GridTrack.Flex()), gap: Space.S3)
        {
            Width = SizeValue.Fill,
        };
        grid.Add(PayoutSettings(theme));
        grid.Add(Explainer(theme));
        return grid;
    }

    private VisualNode PayoutSettings(IAppTheme theme)
    {
        var scheduleAndMinimum = new Row(gap: Space.S3) { Width = SizeValue.Fill };

        var schedule = new Column(gap: 5) { Width = SizeValue.Fill };
        schedule.Add(new Text("Schedule", TypeRole.Caption, theme.TextSecondary, maxLines: 1));
        schedule.Add(new SegmentedControl(["Daily", "Weekly", "Manual"], _schedule,
            i => SetState(() => _schedule = i))
        {
            Size = SizeVariant.Small,
        });

        var minimum = new Column(gap: 5) { Width = SizeValue.Fill };
        minimum.Add(new Text("Minimum", TypeRole.Caption, theme.TextSecondary, maxLines: 1));
        minimum.Add(new TextInput("R$ 250,00", null, size: SizeVariant.Medium));

        scheduleAndMinimum.Add(new Flexible(schedule, 1));
        scheduleAndMinimum.Add(new Flexible(minimum, 1));

        var receipts = new Row(gap: Space.S3) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        receipts.Add(new Switch(_receipts, () => SetState(() => _receipts = !_receipts)));
        receipts.Add(new Flexible(new Text("Email me every payout receipt", TypeRole.BodyM,
            theme.TextPrimary, maxLines: 1), 1));

        var actions = new Row(gap: Space.S2) { Width = SizeValue.Fill, Main = MainAlign.End };
        actions.Add(new Button("Cancel", Variant.Ghost, SizeVariant.Small));
        actions.Add(new Button("Save changes", Variant.Primary, SizeVariant.Small));

        var column = new Column(gap: Space.S4) { Width = SizeValue.Fill };
        column.Add(new Text("Payout settings", TypeRole.Label, theme.TextPrimary, maxLines: 1));
        column.Add(new Select(["Checking ··· 4821", "Savings ··· 1190"], _account,
            i => SetState(() => _account = i), placeholder: "Destination account"));
        column.Add(scheduleAndMinimum);
        column.Add(receipts);
        column.Add(actions);

        return new Card(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S4),
        }, column))
        {
            Width = SizeValue.Fill,
        };
    }

    /// <summary>What is web-only here, and what is emphatically not.</summary>
    private static VisualNode Explainer(IAppTheme theme)
    {
        var chips = new Row(gap: Space.S2) { Cross = CrossAlign.Center };
        chips.Add(Chip(theme, "Same tokens", Variant.Primary));
        chips.Add(Chip(theme, "Same variants", Variant.Success));
        chips.Add(Chip(theme, "Web anatomy", Variant.Warning));

        var column = new Column(gap: Space.S3) { Width = SizeValue.Fill };
        column.Add(new Text("Web-only affordances in this screen", TypeRole.Label, theme.TextPrimary,
            maxLines: 1));
        column.Add(new Text(
            "Hover on rows and controls · dense pointer-sized controls · sortable headings · "
            + "pagination · keyboard shortcuts · multi-column breakpoints.",
            TypeRole.Caption, theme.TextSecondary, maxLines: 4));
        column.Add(new Divider());
        column.Add(new Text(
            "Everything else — colour, type, spacing, radius, elevation, motion, variant and size "
            + "vocabulary — is the same Layer 1 core the macOS Studio resolves from. Nothing was "
            + "re-derived for the browser: control geometry comes from Sizing, animation from the "
            + "named Motion roles, and the realizer emits light-dark() so this page renders once "
            + "for both themes.",
            TypeRole.Caption, theme.TextSecondary, maxLines: 8));
        column.Add(chips);

        return new Card(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S4),
        }, column))
        {
            Width = SizeValue.Fill,
        };
    }
}
