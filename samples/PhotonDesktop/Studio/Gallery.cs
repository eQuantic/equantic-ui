using eQuantic.UI.Components;
using eQuantic.UI.Primitives;

namespace eQuantic.Studio;

/// <summary>The gallery's table of contents — one entry per family of controls.</summary>
public enum GallerySection
{
    Buttons = 0,
    Inputs = 1,
    Selection = 2,
    DataDisplay = 3,
    Containers = 4,
    Progress = 5,
    Navigation = 6,
    Feedback = 7,
    Overlays = 8,
    Device = 9,
}

/// <summary>
/// The gallery's CONTENT: which sections exist, what each is for, and the tree each one renders.
/// Kept apart from the shell so the shell is only chrome and the sections are only components —
/// adding a control to the library means adding it here and nowhere else.
/// </summary>
public static class Gallery
{
    public static readonly GallerySection[] Sections = Enum.GetValues<GallerySection>();

    /// <summary>The variants worth flipping between on one specimen (Link is an inline-text form).</summary>
    public static readonly Variant[] InspectableVariants =
    [
        Variant.Primary, Variant.Secondary, Variant.Destructive,
        Variant.Outline, Variant.Ghost, Variant.Success,
        Variant.Warning, Variant.Info, Variant.Tertiary,
    ];

    public static string NameOf(GallerySection section) => section switch
    {
        GallerySection.Buttons => "Buttons",
        GallerySection.Inputs => "Inputs",
        GallerySection.Selection => "Selection",
        GallerySection.DataDisplay => "Identity & status",
        GallerySection.Containers => "Cards & lists",
        GallerySection.Progress => "Progress",
        GallerySection.Navigation => "Tabs & bars",
        GallerySection.Feedback => "Banners & toasts",
        GallerySection.Overlays => "Overlays",
        _ => "Device",
    };

    /// <summary>The sidebar's group headings, exactly as the handoff draws them — plus DEVICE,
    /// which the 1.0 handoff predates (Track D landed after it was drawn).</summary>
    public static string GroupOf(GallerySection section) => section switch
    {
        GallerySection.Buttons or GallerySection.Inputs or GallerySection.Selection => "Primitives",
        GallerySection.DataDisplay or GallerySection.Containers or GallerySection.Progress
            => "Data display",
        GallerySection.Navigation => "Navigation",
        GallerySection.Feedback or GallerySection.Overlays => "Feedback",
        _ => "Device",
    };

    /// <summary>The footer's headline number: every distinct component this gallery puts live.</summary>
    public static int TotalCoverage()
    {
        var total = 0;
        foreach (var section in Sections) total += CoverageOf(section);
        return total;
    }

    public static string BlurbOf(GallerySection section) => section switch
    {
        GallerySection.Buttons =>
            "Nine variants across four sizes, all measured by the same control ladder.",
        GallerySection.Inputs =>
            "Text, choice and range entry — every field states its label, helper and error.",
        GallerySection.Selection =>
            "Binary and exclusive choice, each announcing its state rather than only painting it.",
        GallerySection.DataDisplay =>
            "Who someone is and how something stands: avatars, badges, chips.",
        GallerySection.Containers =>
            "Surfaces that hold other things — a card you read at a glance, a list you scan.",
        GallerySection.Progress =>
            "Work in flight — determinate, indeterminate, and the shape of what is coming.",
        GallerySection.Navigation =>
            "Moving between places, and knowing which place you are in.",
        GallerySection.Feedback =>
            "Messages that arrive: banners you keep, toasts you dismiss, states with nothing in them.",
        GallerySection.Overlays =>
            "Surfaces over the page — and how each one maps to a desktop window.",
        _ => "What the DEVICE can do, taken through a constructor like any other dependency.",
    };

    public static Icons IconOf(GallerySection section) => section switch
    {
        GallerySection.Buttons => Icons.Check,
        GallerySection.Inputs => Icons.Mail,
        GallerySection.Selection => Icons.CheckCircle,
        GallerySection.DataDisplay => Icons.Person,
        GallerySection.Containers => Icons.CheckCircle,
        GallerySection.Progress => Icons.Refresh,
        GallerySection.Navigation => Icons.Search,
        GallerySection.Feedback => Icons.Notifications,
        GallerySection.Overlays => Icons.Info,
        _ => Icons.Person,
    };

    /// <summary>How many distinct components the section puts on screen — the status bar's count.</summary>
    public static int CoverageOf(GallerySection section) => section switch
    {
        GallerySection.Buttons => 3,
        GallerySection.Inputs => 6,
        GallerySection.Selection => 5,
        GallerySection.DataDisplay => 3,
        GallerySection.Containers => 3,
        GallerySection.Progress => 4,
        GallerySection.Navigation => 4,
        GallerySection.Feedback => 4,
        GallerySection.Overlays => 4,
        _ => 6,
    };

    public static VisualNode Render(GallerySection section, IAppTheme theme, SectionState state,
        Action<Action> mutate, Func<IAppTheme, VisualNode> specimen, string specimenCode = "",
        Density density = Density.Comfortable) => section switch
    {
        GallerySection.Buttons => Buttons(theme, specimen, specimenCode, density),
        GallerySection.Inputs => Inputs(theme, state, mutate),
        GallerySection.Selection => Selection(theme, state, mutate),
        GallerySection.DataDisplay => DataDisplay(theme, state, mutate),
        GallerySection.Containers => Containers(theme),
        GallerySection.Progress => Progress(theme, state),
        GallerySection.Navigation => Navigation(theme, state, mutate),
        GallerySection.Feedback => Feedback(theme, state, mutate),
        GallerySection.Overlays => Overlays(theme, state, mutate),
        _ => Device(theme, state, mutate),
    };

    // ---- Sections --------------------------------------------------------------------------

    /// <summary>The code card's fixed colours — the handoff paints the snippet on the same dark
    /// slab in BOTH modes, so these are tokens with one answer.</summary>
    private static readonly ColorToken CodeSlab = new(new Color(0x10, 0x14, 0x18, 0xFF));
    private static readonly ColorToken CodeInk = new(new Color(0xC9, 0xD4, 0xDE, 0xFF));

    private static VisualNode Buttons(IAppTheme theme, Func<IAppTheme, VisualNode> specimen,
        string specimenCode, Density density)
    {
        // Live preview, per the handoff: title row, the STAGE (a SurfaceSubtle slab the specimen
        // centres on), and the C# that builds it on the code slab.
        var head = new Row(gap: Space.S2) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        head.Add(new Text("Live preview", TypeRole.TitleSmall, theme.TextPrimary, maxLines: 1));
        head.Add(new Box(new BoxStyle
        {
            Background = theme.SurfaceSubtle,
            CornerRadius = new CornerRadii(Radius.Xs),
            Padding = EdgeInsets.Symmetric(Space.S2, 2),
        }, new Text("Button", TypeRole.Overline, theme.TextSecondary, maxLines: 1) { Mono = true }));
        head.Add(new Spacer(1));
        head.Add(new Text("driven by the inspector \u2192", TypeRole.LabelSmall, theme.TextMuted,
            maxLines: 1));

        var staged = new Row(gap: 0)
        {
            Width = SizeValue.Fill,
            Main = MainAlign.Center,
            Cross = CrossAlign.Center,
        };
        staged.Add(specimen(theme));
        var stage = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            MinHeight = 120,
            Background = theme.SurfaceSubtle,
            CornerRadius = new CornerRadii(Radius.Md),
            Padding = EdgeInsets.All(Space.S5),
        }, staged);

        // The SDK's own code component, coloured by the shared C# tokenizer — the gallery shows
        // the thing itself rather than a hand-drawn slab that looks like it. EDITABLE, because a
        // gallery that only shows code you cannot put a caret in proves half the component.
        VisualNode code = new CodeEditor(specimenCode, "csharp")
        {
            Inverse = true,
            ShowLineNumbers = true,
            Caption = "C#",
        };

        var preview = Column(Space.S4);
        preview.Add(head);
        preview.Add(stage);
        preview.Add(code);
        var previewCard = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Background = theme.Surface,
            BorderWidth = 1,
            BorderColor = theme.Border,
            CornerRadius = new CornerRadii(Radius.Lg),
            Padding = EdgeInsets.All(Space.S5),
            Elevation = 1,
        }, preview);

        // The matrix card: every variant at Medium, the size ladder, and the IconButton kinds —
        // three rows separated by dividers, exactly the handoff's one card.
        var variants = new Row(gap: Space.S3) { Width = SizeValue.Fill, Wrap = true };
        foreach (var variant in Enum.GetValues<Variant>())
            variants.Add(new Button(variant.ToString(), variant));

        var ladder = new Row(gap: Space.S3) { Width = SizeValue.Fill, Wrap = true };
        foreach (var size in Enum.GetValues<SizeVariant>())
            ladder.Add(new Button($"{size} {Sizing.Height(size, density):0}", Variant.Primary, size));

        var kinds = new Row(gap: Space.S3) { Width = SizeValue.Fill, Wrap = true, Cross = CrossAlign.Center };
        foreach (var kind in Enum.GetValues<IconButtonKind>())
            kinds.Add(new IconButton(Icons.Heart, $"{kind} icon button", kind));
        kinds.Add(new Text("IconButton \u00b7 Standard \u00b7 Tonal \u00b7 Filled \u00b7 Outline",
            TypeRole.Caption, theme.TextMuted, maxLines: 1));

        var matrix = Column(Space.S4);
        matrix.Add(variants);
        matrix.Add(new Divider());
        matrix.Add(ladder);
        matrix.Add(new Divider());
        matrix.Add(kinds);
        var matrixCard = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Background = theme.Surface,
            BorderWidth = 1,
            BorderColor = theme.Border,
            CornerRadius = new CornerRadii(Radius.Lg),
            Padding = EdgeInsets.All(Space.S4),
        }, matrix);

        var column = Column(Space.S3);
        column.Add(previewCard);
        column.Add(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = new EdgeInsets(0, Space.S4, 0, 0),
        }, new Text("VARIANT MATRIX \u00b7 MEDIUM", TypeRole.Caption, theme.TextMuted, maxLines: 1)));
        column.Add(matrixCard);
        return column;
    }

    private static VisualNode Inputs(IAppTheme theme, SectionState state, Action<Action> mutate)
    {
        // The handoff's two-column grid: every field states what STATE it is demonstrating on the
        // caption line under it, because a gallery that only shows the resting state documents a
        // third of the component.
        var grid = new Grid([GridTrack.Flex(), GridTrack.Flex()], gap: Space.S4)
        {
            Width = SizeValue.Fill,
        };

        grid.Add(Labelled(theme,
            new TextInput(state.Name, v => mutate(() => state.Name = v), "Full name",
                placeholder: "As it appears on the document"),
            "Default · Radius.Md · 1dp BorderStrong"));

        grid.Add(Labelled(theme,
            new TextInput(state.Email, v => mutate(() => state.Email = v), "Email",
                placeholder: "name@company.com",
                error: state.Email.Length > 0 && !state.Email.Contains('@')
                    ? "That address is missing an @"
                    : null),
            "Click it — the focus ring is 2dp, and the caret is real"));

        grid.Add(Labelled(theme,
            new TextInput(state.TaxId, v => mutate(() => state.TaxId = v), "Tax ID",
                placeholder: "000.000.000-00",
                error: state.TaxId.Length is > 0 and < 14 ? "Enter all 11 digits" : null),
            "Error · 2dp Destructive border, message under the field"));

        grid.Add(Labelled(theme,
            new Select(["Checking ··· 4821", "Savings ··· 1190", "Card ··· 7702"], state.Account,
                i => mutate(() => state.Account = i), placeholder: "Choose an account"),
            "Select · opens the picker, matched to the field's width"));

        grid.Add(Labelled(theme,
            new SearchField(state.Query, v => mutate(() => state.Query = v),
                placeholder: "Search transactions"),
            "SearchField · Radius.Full, clears itself"));

        // Stepper and Slider share the last cell, as they do in the handoff.
        var bounded = new Row(gap: Space.S4) { Width = SizeValue.Fill, Cross = CrossAlign.End };
        bounded.Add(Labelled(theme,
            new Stepper(state.Quantity, v => mutate(() => state.Quantity = v))
            {
                Min = 1,
                Max = 9,
                Label = "Quantity",
            },
            "Stepper"));
        bounded.Add(new Flexible(Labelled(theme,
            new Slider(state.Budget, v => mutate(() => state.Budget = v))
            {
                Step = 0.05f,
                Label = "Monthly budget",
            },
            $"Slider · {state.Budget * 100:0}%"), 1));
        grid.Add(bounded);

        return Section(theme, "Inputs", grid);
    }

    private static VisualNode Selection(IAppTheme theme, SectionState state, Action<Action> mutate)
    {
        // Three groups side by side, hairlines between — the handoff's one card.
        var boxes = new Column(gap: Space.S3) { Cross = CrossAlign.Start };
        boxes.Add(new Checkbox(state.Agreed, () => mutate(() => state.Agreed = !state.Agreed),
            "Checked"));
        boxes.Add(new Checkbox(false, () => mutate(() => { }), "Unchecked"));
        boxes.Add(new Checkbox(false, null, "Indeterminate") { Indeterminate = true });
        boxes.Add(new Checkbox(false, null, "Disabled") { Disabled = true });

        var exclusive = new Column(gap: Space.S3) { Cross = CrossAlign.Start };
        exclusive.Add(new RadioGroup(["Selected", "Option B", "Option C"], state.Choice,
            i => mutate(() => state.Choice = i)));
        exclusive.Add(Caption(theme, "RadioGroup · roving focus"));

        var toggles = new Column(gap: Space.S3) { Cross = CrossAlign.Start };
        var faceId = new Row(gap: Space.S2) { Cross = CrossAlign.Center };
        faceId.Add(new Switch(state.Notifications,
            () => mutate(() => state.Notifications = !state.Notifications)));
        faceId.Add(new Text($"Face ID · {(state.Notifications ? "on" : "off")}", TypeRole.BodyM,
            theme.TextPrimary, maxLines: 1));
        toggles.Add(faceId);

        var off = new Row(gap: Space.S2) { Cross = CrossAlign.Center };
        off.Add(new Switch(false, null) { Disabled = true });
        off.Add(new Text("Disabled", TypeRole.BodyM, theme.TextMuted, maxLines: 1));
        toggles.Add(off);

        toggles.Add(new SegmentedControl(["1M", "6M", "1Y"], state.Filter,
            i => mutate(() => state.Filter = i))
        {
            Size = SizeVariant.Small,
            Stretch = false,
        });

        var row = new Row(gap: 0) { Width = SizeValue.Fill, Cross = CrossAlign.Start };
        row.Add(boxes);
        row.Add(GroupDivider());
        row.Add(exclusive);
        row.Add(GroupDivider());
        row.Add(toggles);

        return Section(theme, "Selection controls", row);
    }

    /// <summary>Identity &amp; status: who someone is, and how a thing stands.</summary>
    private static VisualNode DataDisplay(IAppTheme theme, SectionState state, Action<Action> mutate)
    {
        var people = new Row(gap: Space.S2) { Cross = CrossAlign.Center };
        people.Add(new Avatar("AB", SizeVariant.Small, "Ana Beatriz"));
        people.Add(new Avatar("AB", SizeVariant.Medium, "Ana Beatriz"));
        people.Add(new Avatar("AB", SizeVariant.Large, "Ana Beatriz")
        {
            Status = PresenceStatus.Online,
        });
        people.Add(new Avatar("AB", SizeVariant.XLarge, "Ana Beatriz"));
        var identity = Labelled(theme, people,
            $"Avatar {Sizing.Avatar(SizeVariant.Small):0}/{Sizing.Avatar(SizeVariant.Medium):0}"
            + $"/{Sizing.Avatar(SizeVariant.Large):0}/{Sizing.Avatar(SizeVariant.XLarge):0} · with presence");

        var badges = new Row(gap: Space.S2) { Cross = CrossAlign.Center, Wrap = true };
        badges.Add(new Chip("Beta", ChipKind.Tag) { Variant = Variant.Primary });
        badges.Add(new Chip("Paid", ChipKind.Tag) { Variant = Variant.Success });
        badges.Add(new Chip("Pending", ChipKind.Tag) { Variant = Variant.Warning });
        badges.Add(new Chip("Failed", ChipKind.Tag) { Variant = Variant.Destructive });
        badges.Add(new Badge(12));
        badges.Add(new Badge { Dot = true });
        var status = Labelled(theme, badges, "Tag · tonal status · Badge count · Badge dot");

        var chips = new Row(gap: Space.S2) { Cross = CrossAlign.Center, Wrap = true };
        chips.Add(new Chip("All", ChipKind.Filter, state.Filter == 0,
            () => mutate(() => state.Filter = 0)));
        chips.Add(new Chip("Income", ChipKind.Input, true, onRemove: () => mutate(() => { })));
        chips.Add(new Chip("April", ChipKind.Filter, state.Filter == 2,
            () => mutate(() => state.Filter = 2)));
        var filters = Labelled(theme, chips, "Chip · filter, removable");

        var row = new Row(gap: Space.S3)
        {
            Width = SizeValue.Fill,
            Cross = CrossAlign.Start,
            Wrap = true,
            RunGap = Space.S4,
        };
        row.Add(identity);
        row.Add(status);
        row.Add(filters);

        return Section(theme, "Identity & status", row);
    }

    /// <summary>Cards &amp; lists: the two containers the handoff puts side by side.</summary>
    private static VisualNode Containers(IAppTheme theme)
    {
        // The stat card: heading row, the number that matters, the delta, and one action in a
        // footer the divider separates — a Card is a SURFACE, so its anatomy is composed here.
        var heading = new Row(gap: Space.S2) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        var titles = new Column(gap: 0);
        titles.Add(new Text("Monthly spend", TypeRole.TitleSmall, theme.TextPrimary, maxLines: 1));
        titles.Add(new Text("April · 14 categories", TypeRole.LabelSmall, theme.TextMuted, maxLines: 1));
        heading.Add(new Flexible(titles, 1));
        heading.Add(new IconButton(Icons.ChevronDown, "More", IconButtonKind.Standard)
        {
            Size = SizeVariant.Small,
            OnPressed = () => { },
        });

        var delta = new Row(gap: Space.S2) { Cross = CrossAlign.Center };
        delta.Add(new Chip("+12%", ChipKind.Tag) { Variant = Variant.Success });
        delta.Add(new Text("vs March", TypeRole.Caption, theme.TextMuted, maxLines: 1));

        var figures = new Column(gap: Space.S1) { Width = SizeValue.Fill };
        figures.Add(new Text("R$ 3.842", TypeRole.Display, theme.TextPrimary, maxLines: 1)
        {
            Tabular = true,
        });
        figures.Add(delta);

        var footer = new Row(gap: 0) { Width = SizeValue.Fill, Main = MainAlign.End };
        footer.Add(new Button("See report", Variant.Link, SizeVariant.Small) { OnPressed = () => { } });

        var cardBody = new Column(gap: Space.S4) { Width = SizeValue.Fill };
        cardBody.Add(heading);
        cardBody.Add(figures);
        cardBody.Add(new Divider());
        cardBody.Add(footer);
        var stat = new eQuantic.UI.Components.Card(cardBody) { Width = SizeValue.Fill };

        var rows = new List([
            new ListItem("Wi-Fi", onPressed: () => { })
            {
                Leading = new Icon(Icons.Info, IconSize.Md, theme.TextSecondary),
                Trailing = new Icon(Icons.ChevronRight, IconSize.Sm, theme.TextMuted),
            },
            new ListItem("Marcos Ribeiro", "Invoice #0311 — paid", onPressed: () => { })
            {
                Leading = new Avatar("MR", SizeVariant.Medium, "Marcos Ribeiro"),
                Trailing = new Text("09:12", TypeRole.Caption, theme.TextMuted, maxLines: 1),
            },
            new ListItem("Notifications", "Push, email and in-app alerts")
            {
                Leading = new Icon(Icons.Notifications, IconSize.Md, theme.TextSecondary),
                Trailing = new Switch(true, () => { }),
            },
        ]);
        var listCard = new eQuantic.UI.Components.Card(rows, CardKind.Outlined)
        {
            Width = SizeValue.Fill,
            Padding = default,
        };

        var grid = new Grid([GridTrack.Flex(), GridTrack.Flex(1.15f)], gap: Space.S3)
        {
            Width = SizeValue.Fill,
        };
        grid.Add(stat);
        grid.Add(listCard);

        return Section(theme, "Cards & lists", grid, Space.S3);
    }

    private static VisualNode Progress(IAppTheme theme, SectionState state)
    {
        var determinate = Labelled(theme,
            new Box(new BoxStyle { Width = SizeValue.Fill }, new ProgressBar(state.Budget)),
            $"Determinate · {state.Budget * 100:0}%", 200);

        var indeterminate = Labelled(theme,
            new Box(new BoxStyle { Width = SizeValue.Fill }, new ProgressBar()),
            "Indeterminate · length honestly unknown", 200);

        var spinner = Labelled(theme,
            new Spinner(IconSize.Md, theme.Colors(Variant.Primary).Base),
            "Spinner · shows after 400ms", 150);

        // Skeleton: the SHAPE of the row that is coming — a circle where the avatar goes and two
        // lines where the text goes, which is the whole argument against a spinner here.
        var lines = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        lines.Add(new Skeleton(SkeletonShape.Line, 150));
        lines.Add(new Skeleton(SkeletonShape.Line, 96));
        var placeholder = new Row(gap: Space.S3) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        placeholder.Add(new Skeleton(SkeletonShape.Circle, 36, 36));
        placeholder.Add(new Flexible(lines, 1));
        var skeleton = Labelled(theme, placeholder, "Skeleton · the SHAPE of what is coming", 240);

        var row = new Row(gap: Space.S5)
        {
            Width = SizeValue.Fill,
            Cross = CrossAlign.Start,
            Wrap = true,
            RunGap = Space.S4,
        };
        row.Add(determinate);
        row.Add(indeterminate);
        row.Add(spinner);
        row.Add(skeleton);

        return Section(theme, "Progress & loading", row);
    }

    private static VisualNode Navigation(IAppTheme theme, SectionState state, Action<Action> mutate)
    {
        var tabs = Labelled(theme,
            new Box(new BoxStyle { Width = SizeValue.Fill },
                new Tabs(["Overview", "Activity", "Reports"], state.Tab,
                    i => mutate(() => state.Tab = i))),
            "Tabs · underline indicator, one Tab stop, arrows move", 260);

        var pager = Labelled(theme,
            new PageIndicator(5, state.Page, i => mutate(() => state.Page = i)),
            "PageIndicator · position, not navigation", 200);

        var actions = new Row(gap: Space.S3) { Cross = CrossAlign.Center };
        actions.Add(new Tooltip(new IconButton(Icons.Search, "Search", IconButtonKind.Tonal)
        {
            OnPressed = () => { },
        }, "Search ⌘K"));
        actions.Add(new Tooltip(new IconButton(Icons.Mail, "Messages") { OnPressed = () => { } },
            "Messages"));
        actions.Add(new Tooltip(new IconButton(Icons.Notifications, "Alerts") { OnPressed = () => { } },
            "Alerts"));
        var tooltips = Labelled(theme, actions, "Tooltip · hover any of them", 180);

        var row = new Row(gap: Space.S4)
        {
            Width = SizeValue.Fill,
            Cross = CrossAlign.Start,
            Wrap = true,
            RunGap = Space.S4,
        };
        row.Add(tabs);
        row.Add(pager);
        row.Add(tooltips);

        return Section(theme, "Navigation", row);
    }

    private static VisualNode Feedback(IAppTheme theme, SectionState state, Action<Action> mutate)
    {
        // Four banners, one per status — the handoff stacks them so the tonal pairs can be
        // compared at a glance (each is background + on-background from the SAME variant).
        var banners = Column(Space.S3);
        banners.Add(new Banner(Variant.Info, "Statement ready",
            "Your April statement is available for download.")
        {
            OnDismiss = () => { },
        });
        banners.Add(new Banner(Variant.Success, "Transfer completed",
            "R$ 1.200,00 sent to Marcos Ribeiro."));
        banners.Add(new Banner(Variant.Warning, "Limit almost reached",
            "You have used 92% of this month's transfer limit.")
        {
            PrimaryAction = new Button("Raise limit", Variant.Link, SizeVariant.Small)
            {
                OnPressed = () => { },
            },
        });
        banners.Add(new Banner(Variant.Destructive, "Payment failed",
            "The card issuer declined this charge. Try another card."));

        // A Toast is an OVERLAY: it docks itself bottom-trailing on this target and cannot sit
        // inside a card. So the card carries the TRIGGER, and pressing it shows the real thing
        // where the real thing belongs — which documents the behaviour the handoff only drew.
        var toast = Labelled(theme,
            new Button("Show toast", Variant.Secondary)
            {
                OnPressed = () => mutate(() => state.Toast = "Card removed"),
            },
            "Toast · inverse surface, docks bottom-trailing, leaves on its own", 240);

        var empty = new EmptyState(Icons.Search, "No transactions yet",
            "They will appear here once you transfer.")
        {
            Action = new Button("New transfer", Variant.Primary, SizeVariant.Small)
            {
                OnPressed = () => { },
            },
        };

        var bottom = new Row(gap: Space.S5)
        {
            Width = SizeValue.Fill,
            Cross = CrossAlign.Start,
            Wrap = true,
            RunGap = Space.S4,
        };
        bottom.Add(toast);
        bottom.Add(new Box(new BoxStyle { Width = 300 }, empty));

        var column = Column(Space.S4);
        column.Add(banners);
        column.Add(bottom);

        var section = Section(theme, "Feedback", column);
        if (state.Toast is null) return section;

        // The overlay rides ALONGSIDE the section: an Overlay node is zero in the page flow, so
        // the layer lays out against the viewport and this column is unaffected.
        var layers = new Stack { Width = SizeValue.Fill };
        layers.Add(section);
        layers.Add(new Toast(state.Toast)
        {
            ActionLabel = "Undo",
            OnAction = () => mutate(() => state.Toast = null),
        });
        return layers;
    }

    private static VisualNode Overlays(IAppTheme theme, SectionState state, Action<Action> mutate)
    {
        // The handoff draws the three overlays side by side with the desktop mapping beside them.
        // Here they are LIVE: the triggers open the real thing, over this same window.
        var dialog = Labelled(theme,
            new Button("Delete card…", Variant.Destructive)
            {
                OnPressed = () => mutate(() => state.DialogOpen = true),
            },
            "Dialog · centred, E5; Escape and the scrim both mean no", 230);

        var sheet = Labelled(theme,
            new Button("Transfer options", Variant.Outline)
            {
                OnPressed = () => mutate(() => state.SheetOpen = true),
            },
            "BottomSheet · a popover here, a sheet on a phone", 230);

        var menu = Labelled(theme,
            new Menu(new Button("Row actions", Variant.Secondary),
            [
                new MenuItem("Schedule") { Icon = Icons.Refresh },
                new MenuItem("Repeat monthly") { Icon = Icons.Check },
                new MenuItem("Remove recipient") { Icon = Icons.Close, Destructive = true },
            ]),
            "Menu · arrows walk it, Enter takes it, Escape closes", 230);

        var mapping = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        mapping.Add(new Text("Desktop mapping", TypeRole.TitleSmall, theme.TextPrimary, maxLines: 1));
        mapping.Add(new Text(
            "On macOS the mobile BottomSheet becomes a popover anchored to its trigger; "
            + "ActionSheet becomes a context menu; Toast docks bottom-trailing. The Dialog keeps "
            + "the same anatomy, centred on the window with E5.",
            TypeRole.Caption, theme.TextSecondary, maxLines: 4));
        mapping.Add(new Text(
            "Hover states exist here (the desktop adapter): every pressable surface takes a "
            + "SurfaceSubtle hover fill 100ms before its pressed state.",
            TypeRole.Caption, theme.TextSecondary, maxLines: 3));

        var specimens = new Row(gap: Space.S4) { Width = SizeValue.Fill, Cross = CrossAlign.Start, Wrap = true };
        specimens.Add(dialog);
        specimens.Add(sheet);
        specimens.Add(menu);

        var content = Column(Space.S4);
        content.Add(specimens);
        content.Add(new Divider());
        content.Add(mapping);
        var section = Section(theme, "Overlays", content);

        if (!state.DialogOpen && !state.SheetOpen) return section;

        var stack = new Stack { Width = SizeValue.Fill };
        stack.Add(section);
        if (state.DialogOpen)
        {
            stack.Add(new Dialog("Delete card?",
                "\"Work Visa ··· 4821\" will be removed and its automatic payments will stop.",
                [
                    new DialogAction("Keep card", () => mutate(() => state.DialogOpen = false)),
                    new DialogAction("Delete", () => mutate(() => state.DialogOpen = false),
                        Variant.Destructive),
                ],
                // Destructive, and still dismissible: Escape and a tap outside both mean "not
                // that", which is the answer someone who opened this by accident is looking for.
                // Only the Delete button deletes.
                dismissible: true,
                onDismiss: () => mutate(() => state.DialogOpen = false)));
        }
        if (state.SheetOpen)
        {
            var options = new List([
                new ListItem("Instant (PIX)", onPressed: () => mutate(() => state.SheetOpen = false))
                {
                    Leading = new Icon(Icons.Play, IconSize.Md, theme.TextSecondary),
                },
                new ListItem("Schedule", onPressed: () => mutate(() => state.SheetOpen = false))
                {
                    Leading = new Icon(Icons.Refresh, IconSize.Md, theme.TextSecondary),
                },
                new ListItem("Remove recipient", onPressed: () => mutate(() => state.SheetOpen = false))
                {
                    Leading = new Icon(Icons.Close, IconSize.Md,
                        theme.Colors(Variant.Destructive).Base),
                },
            ]);
            var body = Column(Space.S2);
            body.Add(new Text("Transfer options", TypeRole.Label, theme.TextPrimary, maxLines: 1));
            body.Add(options);
            stack.Add(new BottomSheet(body, () => mutate(() => state.SheetOpen = false)));
        }
        return stack;
    }

    // ---- Shared pieces ---------------------------------------------------------------------

    /// <summary>The handoff's section shape: an UPPERCASE eyebrow, then one bordered surface. The
    /// eyebrow sits outside the card, which is why it is not the Card component's own header.</summary>
    private static VisualNode Section(IAppTheme theme, string eyebrow, VisualNode content,
        float padding = Space.S4)
    {
        var column = Column(Space.S2);
        column.Add(new Text(eyebrow.ToUpperInvariant(), TypeRole.Overline, theme.TextMuted,
            maxLines: 1));
        column.Add(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Background = theme.Surface,
            BorderWidth = 1,
            BorderColor = theme.Border,
            CornerRadius = new CornerRadii(Radius.Lg),
            Padding = EdgeInsets.All(padding),
        }, content));
        return column;
    }

    /// <summary>The small grey line under a specimen that NAMES it — the handoff labels every
    /// specimen this way, and it is what turns a gallery into documentation.</summary>
    private static VisualNode Caption(IAppTheme theme, string text) =>
        new Text(text, TypeRole.LabelSmall, theme.TextMuted, maxLines: 2);

    /// <summary>
    /// A specimen with its caption underneath, as one cell. WIDTH matters: a caption with no
    /// bound measures at everything available and pushes its neighbours onto the next line, so a
    /// row of specimens gives each one a column to live in.
    /// </summary>
    private static VisualNode Labelled(IAppTheme theme, VisualNode specimen, string caption,
        float width = 0)
    {
        var column = new Column(gap: Space.S2)
        {
            Cross = CrossAlign.Start,
            Width = width > 0 ? SizeValue.Fixed(width) : SizeValue.Hug,
        };
        column.Add(specimen);
        column.Add(Caption(theme, caption));
        return column;
    }

    /// <summary>The hairline between the handoff's specimen groups.</summary>
    private static VisualNode GroupDivider() =>
        new Box(new BoxStyle { Height = SizeValue.Fill, Padding = EdgeInsets.Symmetric(Space.S3, 0) },
            new Divider(axis: DividerAxis.Vertical));

    private static Row Row(float gap) => new(gap) { Cross = CrossAlign.Center };

    private static Column Column(float gap) => new(gap) { Width = SizeValue.Fill };

    private static VisualNode Label(IAppTheme theme, string text) =>
        new Text(text, TypeRole.BodyM, theme.TextSecondary, maxLines: 1);

    private static VisualNode SwitchRow(IAppTheme theme, string label, bool on, Action onChanged)
    {
        var row = new Row(gap: Space.S3) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        row.Add(new Flexible(Label(theme, label), 1));
        row.Add(new Switch(on, onChanged));
        return row;
    }

    /// <summary>One demo cell: what the component is, one sentence on WHEN to reach for it, and it.</summary>
    /// <summary>
    /// What the DEVICE can do. The shell takes an IPhotoLibrary through its constructor and passes
    /// what it got here — which is the whole point of the design: this section has no idea whether
    /// a Mac's open panel, an iPhone's picker or a browser's file input answered, and it does not
    /// change by one line when the answer comes from somewhere else.
    /// </summary>
    private static VisualNode Device(IAppTheme theme, SectionState state, Action<Action> mutate)
    {
        var pick = Column(Space.S3);
        pick.Add(new Button("Choose a picture…", Variant.Primary)
        {
            Leading = CuratedIcons.Resolve(Icons.Plus),
            OnPressed = state.OnPickImage,
            Disabled = state.OnPickImage is null,
        });

        if (state.PickedImage is { } picked)
        {
            // The bytes as a data URI: no temporary file, no path to clean up, and the same node
            // shows it on every target.
            pick.Add(new Image(picked.ToDataUri(), 240, 160, ImageFit.Contain, picked.Name ?? "picked")
            {
                CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Medium)),
            });
            pick.Add(Label(theme, $"{picked.Name} · {picked.Width}×{picked.Height} · "
                + $"{picked.ByteCount / 1024} KB · {picked.MimeType}"));
        }
        else
        {
            pick.Add(Label(theme, state.OnPickImage is null
                ? "No photo library on this device."
                : "Nothing chosen yet."));
        }

        var identity = Column(Space.S3);
        identity.Add(new Button("Confirm it's you", Variant.Secondary)
        {
            OnPressed = state.OnAuthenticate,
            Disabled = state.OnAuthenticate is null,
        });
        identity.Add(Label(theme, state.Authentication
            ?? (state.OnAuthenticate is null
                ? "No reader on this device — the section says so instead of offering a dead button."
                : "Not asked yet.")));

        var column = Column(Space.S5);
        column.Add(Card(theme, "Photo library",
            "A capability is a SERVICE: an interface taken through the constructor, so this page is "
            + "testable with a fake and identical on all four targets.", pick));
        column.Add(Card(theme, "Biometrics",
            "Four ways not to succeed, and they are not the same — a wrong finger, nothing enrolled, "
            + "someone who backed out, a device with no reader at all.", identity));

        var connectivity = Column(Space.S3);
        if (state.Network is { } network)
        {
            var (dot, label) = network switch
            {
                { Online: true, Kind: NetworkKind.Wifi } => (theme.Colors(Variant.Success).Base, "Online — wifi"),
                { Online: true, Kind: NetworkKind.Wired } => (theme.Colors(Variant.Success).Base, "Online — wired"),
                { Online: true, Kind: NetworkKind.Cellular } => (theme.Colors(Variant.Warning).Base, "Online — cellular (metered)"),
                { Online: true } => (theme.Colors(Variant.Success).Base, "Online"),
                _ => (theme.Colors(Variant.Destructive).Base, "Offline"),
            };
            var readout = Row(Space.S2);
            readout.Add(new Box(new BoxStyle
            {
                Width = 10, Height = 10,
                Background = dot,
                CornerRadius = new CornerRadii(5),
            }));
            readout.Add(new Text(label, TypeRole.BodyM, theme.TextPrimary, maxLines: 1));
            connectivity.Add(readout);
            connectivity.Add(Label(theme, "Turn wifi off and watch it flip — no refresh anywhere."));
        }
        else
        {
            connectivity.Add(Label(theme, "No network monitor on this head."));
        }
        column.Add(Card(theme, "Network",
            "The first capability whose answer does not hold still: the state seeds the page and "
            + "every change streams into SetState.", connectivity));

        var movement = Column(Space.S3);
        if (state.MotionAvailable && state.Motion is { } sample)
        {
            movement.Add(Label(theme,
                $"gravity ({sample.Gravity.X:0.00}, {sample.Gravity.Y:0.00}, {sample.Gravity.Z:0.00}) g"));
            movement.Add(Label(theme,
                $"rotation ({sample.RotationRate.X:0.0}, {sample.RotationRate.Y:0.0}, {sample.RotationRate.Z:0.0}) rad/s"));
        }
        else if (state.MotionAvailable)
        {
            movement.Add(Label(theme, "Listening — move the device."));
        }
        else
        {
            movement.Add(Label(theme,
                "No motion sensors on this desk — and that is the design working, not a gap: "
                + "IsAvailable is part of the contract, and the tilt UI belongs behind it."));
        }
        column.Add(Card(theme, "Motion",
            "A high-rate stream, and the first capability most devices honestly lack.", movement));

        var whereabouts = Column(Space.S3);
        whereabouts.Add(new Button("Where am I?", Variant.Secondary)
        {
            OnPressed = state.OnLocate,
            Disabled = state.OnLocate is null,
        });
        whereabouts.Add(Label(theme, state.Position
            ?? (state.OnLocate is null
                ? "Location services are off on this device."
                : "Not asked yet — the system prompt appears on the first press.")));
        column.Add(Card(theme, "Location",
            "Where PermissionState earns its three values: asking prompts, Denied explains the way "
            + "to Settings, and asking again is never the answer.", whereabouts));

        var filming = Column(Space.S3);
        var controls = Row(Space.S3);
        controls.Add(new Button(state.CameraSession is null ? "Start camera" : "Stop", Variant.Secondary)
        {
            OnPressed = state.OnToggleCamera,
            Disabled = state.OnToggleCamera is null,
        });
        controls.Add(new Button("Capture", Variant.Primary)
        {
            OnPressed = state.OnCapture,
            Disabled = state.CameraSession is null,
        });
        filming.Add(controls);
        // The node itself: null session = the SurfaceSubtle placeholder, no branching here.
        filming.Add(new CameraPreview(state.CameraSession, 320, 240)
        {
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Medium)),
            Alt = "Live camera preview",
        });
        if (state.Captured is { } still)
        {
            filming.Add(new Image(still.ToDataUri(), 160, 120, ImageFit.Cover, "captured still")
            {
                CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Small)),
            });
            filming.Add(Label(theme, $"{still.Width}×{still.Height} · {still.ByteCount / 1024} KB · {still.MimeType}"));
        }
        column.Add(Card(theme, "Camera",
            "The one capability whose answer is a picture that MOVES — a new node in the "
            + "vocabulary, composited by the engine like any other texture.", filming));
        return column;
    }

    private static VisualNode Card(IAppTheme theme, string title, string rule, VisualNode demo)
    {
        var head = Column(2);
        head.Add(new Text(title, TypeRole.Label, theme.TextPrimary, maxLines: 1));
        head.Add(new Text(rule, TypeRole.Caption, theme.TextMuted, maxLines: 2));

        var body = Column(Space.S4);
        body.Add(head);
        body.Add(new Divider());
        body.Add(demo);

        return new eQuantic.UI.Components.Card(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S4),
        }, body))
        {
            Width = SizeValue.Fill,
        };
    }
}
