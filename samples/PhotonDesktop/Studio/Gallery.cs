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
    Progress = 4,
    Navigation = 5,
    Feedback = 6,
    Overlays = 7,
    Device = 8,
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
        GallerySection.DataDisplay => "Data display",
        GallerySection.Progress => "Progress",
        GallerySection.Navigation => "Navigation",
        GallerySection.Feedback => "Feedback",
        GallerySection.Overlays => "Overlays",
        _ => "Device",
    };

    public static string BlurbOf(GallerySection section) => section switch
    {
        GallerySection.Buttons =>
            "Nine variants across four sizes, all measured by the same control ladder.",
        GallerySection.Inputs =>
            "Text, choice and range entry — every field states its label, helper and error.",
        GallerySection.Selection =>
            "Binary and exclusive choice, each announcing its state rather than only painting it.",
        GallerySection.DataDisplay =>
            "Reading surfaces: identity, counts, rows and tables.",
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
        GallerySection.Progress => Icons.ChevronRight,
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
        GallerySection.DataDisplay => 6,
        GallerySection.Progress => 3,
        GallerySection.Navigation => 4,
        GallerySection.Feedback => 4,
        GallerySection.Overlays => 4,
        _ => 3,
    };

    public static VisualNode Render(GallerySection section, IAppTheme theme, SectionState state,
        Action<Action> mutate, Func<IAppTheme, VisualNode> specimen) => section switch
    {
        GallerySection.Buttons => Buttons(theme, specimen),
        GallerySection.Inputs => Inputs(theme, state, mutate),
        GallerySection.Selection => Selection(theme, state, mutate),
        GallerySection.DataDisplay => DataDisplay(theme),
        GallerySection.Progress => Progress(theme, state),
        GallerySection.Navigation => Navigation(theme, state, mutate),
        GallerySection.Feedback => Feedback(theme, state),
        GallerySection.Overlays => Overlays(theme, state, mutate),
        _ => Device(theme, state, mutate),
    };

    // ---- Sections --------------------------------------------------------------------------

    private static VisualNode Buttons(IAppTheme theme, Func<IAppTheme, VisualNode> specimen)
    {
        var live = Row(Space.S3);
        live.Add(specimen(theme));

        var matrix = new Grid([GridTrack.Flex(), GridTrack.Flex(), GridTrack.Flex()], gap: Space.S3) { Width = SizeValue.Fill };
        foreach (var variant in InspectableVariants)
            matrix.Add(new Button(variant.ToString(), variant));

        var ladder = Row(Space.S3);
        foreach (var size in Enum.GetValues<SizeVariant>())
            ladder.Add(new Button($"{size} {Sizing.Height(size):0}", Variant.Secondary, size));

        var column = Column(Space.S5);
        column.Add(Card(theme, "Live preview", "Driven by the inspector on the right.", live));
        column.Add(Card(theme, "Variant matrix", "Medium size, so only the colour role differs.", matrix));
        column.Add(Card(theme, "Size ladder",
            "Each label states the height the ladder resolved — the numbers are read, not typed.", ladder));
        return column;
    }

    private static VisualNode Inputs(IAppTheme theme, SectionState state, Action<Action> mutate)
    {
        var fields = Column(Space.S4);
        fields.Add(new TextInput(state.Name, v => mutate(() => state.Name = v), "Full name",
            placeholder: "As it appears on the document"));
        fields.Add(new TextInput(state.Email, v => mutate(() => state.Email = v), "Email",
            placeholder: "name@company.com",
            helper: "We only use it for the receipt.",
            error: state.Email.Length > 0 && !state.Email.Contains('@')
                ? "That address is missing an @"
                : null));
        fields.Add(new Select(["Checking ··· 4821", "Savings ··· 1190", "Card ··· 7702"], state.Account,
            i => mutate(() => state.Account = i), placeholder: "Choose an account"));
        fields.Add(new SearchField(state.Query, v => mutate(() => state.Query = v),
            placeholder: "Search transactions"));

        var ranges = Column(Space.S4);
        // A Flexible needs slack to take, so the row that holds one has to fill.
        var quantity = new Row(gap: Space.S3) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        quantity.Add(new Flexible(Label(theme, "Quantity"), 1));
        quantity.Add(new Stepper(state.Quantity, v => mutate(() => state.Quantity = v))
        {
            Min = 1,
            Max = 9,
            Label = "Quantity",
        });
        ranges.Add(quantity);
        ranges.Add(Label(theme, $"Monthly budget · {state.Budget * 100:0}%"));
        ranges.Add(new Slider(state.Budget, v => mutate(() => state.Budget = v))
        {
            Step = 0.05f,
            Label = "Monthly budget",
        });

        var column = Column(Space.S5);
        column.Add(Card(theme, "Text and choice",
            "A field carries its own label, helper and error — the caller never lays them out.", fields));
        column.Add(Card(theme, "Bounded values",
            "Stepper for a number you will read; Slider for a proportion you will feel.", ranges));
        return column;
    }

    private static VisualNode Selection(IAppTheme theme, SectionState state, Action<Action> mutate)
    {
        var boxes = Column(Space.S3);
        boxes.Add(new Checkbox(state.Agreed, () => mutate(() => state.Agreed = !state.Agreed),
            "I have read the terms"));
        boxes.Add(new Checkbox(true, null, "Checked, not editable") { Disabled = true });

        var toggles = Column(Space.S3);
        toggles.Add(SwitchRow(theme, "Notifications", state.Notifications,
            () => mutate(() => state.Notifications = !state.Notifications)));
        toggles.Add(SwitchRow(theme, "Wi-Fi only", state.Wifi,
            () => mutate(() => state.Wifi = !state.Wifi)));

        var exclusive = Column(Space.S4);
        exclusive.Add(new RadioGroup(["Standard delivery", "Express", "Pick-up point"], state.Choice,
            i => mutate(() => state.Choice = i)));
        exclusive.Add(new SegmentedControl(["All", "Income", "Expenses"], state.Filter,
            i => mutate(() => state.Filter = i)));

        var column = Column(Space.S5);
        column.Add(Card(theme, "Checkbox", "Independent yes/no, each one its own decision.", boxes));
        column.Add(Card(theme, "Switch",
            "Takes effect immediately — a Switch never waits for a Save button.", toggles));
        column.Add(Card(theme, "One of a set",
            "RadioGroup when the options need explaining; SegmentedControl when they fit in a word.",
            exclusive));
        return column;
    }

    private static VisualNode DataDisplay(IAppTheme theme)
    {
        var people = Row(Space.S3);
        foreach (var size in Enum.GetValues<SizeVariant>())
            people.Add(new Avatar("AB", size, "Ana Beatriz"));

        var badges = Row(Space.S3);
        badges.Add(new Badge(3));
        badges.Add(new Badge(128, variant: Variant.Warning));
        badges.Add(new Chip("Beta", ChipKind.Tag));
        badges.Add(new Chip("Paid", ChipKind.Tag));

        var rows = new List([
            new ListItem("Marcos Ribeiro", "Transfer · today 14:02"),
            new ListItem("Photon-5G", "Connected · 5 GHz"),
            new ListItem("Notifications", "All apps"),
        ]);

        var table = new Table(
            ["Month", "Categories", "Spend"],
            [["April", "14", "R$ 4.280"], ["March", "12", "R$ 3.910"], ["February", "13", "R$ 4.006"]]);

        var column = Column(Space.S5);
        column.Add(Card(theme, "Identity", "Avatars ride the same four-step ladder as everything else.",
            people));
        column.Add(Card(theme, "Counts and labels", "Badge for a number, Chip for a word.", badges));
        column.Add(Card(theme, "Rows", "A list item states its title and its subtitle, never a layout.", rows));
        column.Add(Card(theme, "Table", "Columns that stay aligned as the figures change.", table));
        return column;
    }

    private static VisualNode Progress(IAppTheme theme, SectionState state)
    {
        var bars = Column(Space.S4);
        bars.Add(Label(theme, $"Determinate · {state.Budget * 100:0}%"));
        bars.Add(new ProgressBar(state.Budget));
        bars.Add(Label(theme, "Indeterminate · work of unknown length"));
        bars.Add(new ProgressBar());

        var spinners = Row(Space.S4);
        spinners.Add(new Spinner(IconSize.Sm));
        spinners.Add(new Spinner(IconSize.Dense));
        spinners.Add(new Spinner(IconSize.Md, theme.Colors(Variant.Primary).Base));

        var skeleton = Column(Space.S2);
        skeleton.Add(new Skeleton(SkeletonShape.Line, 220));
        skeleton.Add(new Skeleton(SkeletonShape.Line, 160));
        skeleton.Add(new Skeleton(SkeletonShape.Block, 260, 72));

        var column = Column(Space.S5);
        column.Add(Card(theme, "ProgressBar",
            "Determinate when the end is known; indeterminate when it is honestly not.", bars));
        column.Add(Card(theme, "Spinner",
            "Appears only after 400ms, so a fast round-trip never flashes.", spinners));
        column.Add(Card(theme, "Skeleton",
            "The SHAPE of what is loading — never a spinner where a layout is already known.", skeleton));
        return column;
    }

    private static VisualNode Navigation(IAppTheme theme, SectionState state, Action<Action> mutate)
    {
        var tabs = new Tabs(["Overview", "Activity", "Reports"], state.Tab,
            i => mutate(() => state.Tab = i));

        var pager = Column(Space.S3);
        pager.Add(new PageIndicator(5, state.Page, i => mutate(() => state.Page = i)));
        pager.Add(new PageIndicator(12, 3));

        var actions = Row(Space.S3);
        actions.Add(new Tooltip(new IconButton(Icons.Search, "Search", IconButtonKind.Filled), "Search ⌘K"));
        actions.Add(new Tooltip(new IconButton(Icons.Mail, "Messages"), "Messages"));
        actions.Add(new Tooltip(new IconButton(Icons.Notifications, "Alerts"), "Alerts"));

        var column = Column(Space.S5);
        column.Add(Card(theme, "Tabs", "Peers on one screen, with the indicator under the current one.", tabs));
        column.Add(Card(theme, "PageIndicator",
            "Position, not navigation — past eight pages it becomes a readout on its own.", pager));
        column.Add(Card(theme, "Icon actions",
            "An icon-only control always carries a name, and a tooltip repeats it for sighted users.",
            actions));
        return column;
    }

    private static VisualNode Feedback(IAppTheme theme, SectionState state)
    {
        var banners = Column(Space.S3);
        banners.Add(new Banner(Variant.Success, "Transfer completed", "R$ 1.240 to Marcos Ribeiro."));
        banners.Add(new Banner(Variant.Warning, "Limit almost reached", "You have used 92% of April."));
        banners.Add(new Banner(Variant.Destructive, "Payment failed", "The card issuer declined it."));

        var toast = new Toast(state.Toast ?? "Card removed", Variant.Info) { ActionLabel = "Undo" };

        var empty = new EmptyState(Icons.Search, "No transactions yet",
            "When money moves, it shows up here.");

        var column = Column(Space.S5);
        column.Add(Card(theme, "Banner", "Stays until the situation changes — it is part of the page.",
            banners));
        column.Add(Card(theme, "Toast",
            "Leaves on its own, so it never carries anything the user must read.", toast));
        column.Add(Card(theme, "EmptyState",
            "Says what will appear here and how to make it happen — never just \"no data\".", empty));
        return column;
    }

    private static VisualNode Overlays(IAppTheme theme, SectionState state, Action<Action> mutate)
    {
        var dialogTrigger = new Button("Remove card", Variant.Destructive)
        {
            OnPressed = () => mutate(() => state.DialogOpen = true),
        };

        var menu = new Menu(
            new Button("Transfer options", Variant.Outline),
            [new MenuItem("Schedule"), new MenuItem("Repeat monthly"), new MenuItem("Remove recipient")]);

        var mapping = new Text(
            "On macOS a mobile BottomSheet becomes a window-anchored popover — same tree, "
            + "same component, resolved by the target rather than by the caller.",
            TypeRole.BodyM, theme.TextSecondary, maxLines: 3);

        var column = Column(Space.S5);
        column.Add(Card(theme, "Dialog", "Interrupts, so it must be worth interrupting for.", dialogTrigger));
        column.Add(Card(theme, "Menu", "A short list of actions on the thing you just pressed.", menu));
        column.Add(Card(theme, "Desktop mapping", "One tree, two shapes.", mapping));

        if (!state.DialogOpen) return column;

        var stack = new Stack();
        stack.Add(column);
        stack.Add(new Dialog("Remove this card?",
            "Scheduled payments on it will fail until you add another.",
            [
                new DialogAction("Keep card", () => mutate(() => state.DialogOpen = false)),
                new DialogAction("Remove", () => mutate(() => state.DialogOpen = false),
                    Variant.Destructive),
            ],
            // Destructive, and still dismissible: Escape and a tap outside both mean "not that",
            // which is the answer someone who opened this by accident is looking for. Only the
            // Remove button removes.
            dismissible: true,
            onDismiss: () => mutate(() => state.DialogOpen = false)));
        return stack;
    }

    // ---- Shared pieces ---------------------------------------------------------------------

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
