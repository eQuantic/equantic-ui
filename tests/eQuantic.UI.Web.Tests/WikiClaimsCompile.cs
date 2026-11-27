using eQuantic.UI.Components;
using eQuantic.UI.Web;
using eQuantic.UI.Primitives;
using Microsoft.Extensions.DependencyInjection;
using static eQuantic.UI.Components.UI;
using static eQuantic.UI.Spreadsheets.SpreadsheetsUI;

namespace eQuantic.UI.Web.Tests;

/// <summary>Every API the 0.2.0-preview.13 wiki pages claim, compiled. Nothing else compiles the
/// wiki, so this is what keeps it from rotting in silence.</summary>
public class WikiClaimsCompile
{
    private interface IDocs { Task<string?> FindAsync(string? slug, CancellationToken ct); }

    private sealed class DocPage : Primitives.StatelessComponent, IServerPrefetch
    {
        private string? _doc;

        [ServerOnly]
        public async Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
            => _doc = await services.GetRequiredService<IDocs>()
                .FindAsync(services.GetRequiredService<RouteValues>().Param("slug"), cancellationToken);

        public override VisualNode Build(ComponentContext context)
        {
            _ = context.Route.Param("slug");
            _ = context.Route.Query("page");
            return Text(_doc ?? "Not found", TypeRole.Heading);
        }
    }

    private sealed class LiveRates(INetworkStatus status) : Primitives.StatefulComponent
    {
        private IDisposable? _subscription;
        private NetworkState _network;

        protected override void OnMount() =>
            _subscription = status.Subscribe(state => SetState(() => _network = state));

        protected override void OnUnmount() => _subscription?.Dispose();

        public override VisualNode Build(ComponentContext context) =>
            Text(_network.Online ? "online" : "offline", TypeRole.BodyM);
    }

    // ServerEvents.md (#291): a component hearing a typed topic, as the page writes it.
    private sealed record RoomMessage(string Author, string Text, long Sequence);

    private static class Topics
    {
        public static ServerTopic<RoomMessage> Room(string id) => new($"room:{id}");
    }

    private sealed class RoomScreen(IServerEvents? events) : Primitives.StatefulComponent
    {
        private readonly List<RoomMessage> _messages = [];
        private IDisposable? _room;
        private IDisposable? _connection;
        private ServerConnectionState _state;
        private bool _refused;

        protected override void OnMount()
        {
            _room = events?.Subscribe(Topics.Room("lobby"),
                message => SetState(() => _messages.Add(message)),
                refusal => SetState(() => _refused = true));
            _connection = events?.OnConnectionChanged(connection => SetState(() => _state = connection.State));
        }

        protected override void OnUnmount()
        {
            _room?.Dispose();
            _connection?.Dispose();
        }

        public override VisualNode Build(ComponentContext context) => _refused
            ? Text("This room is not open to you.", TypeRole.BodyM)
            : Column(gap: Space.S2, children: [
                Text(_state == ServerConnectionState.Connected ? "Live" : "Catching up", TypeRole.Caption),
                .. _messages.Select(message => Text(message.Text, TypeRole.BodyM)),
            ]);
    }

    private sealed class ProfilePage(IPhotoLibrary photos) : Primitives.StatefulComponent
    {
        public override VisualNode Build(ComponentContext context) =>
            photos.IsAvailable
                ? Button(label: "Choose a picture", onPressed: () => { })
                : Text("No library here", TypeRole.BodyM);
    }

    /// <summary>DesignSystem (0.2.0-preview.61): a size that follows the window.</summary>
    [Fact]
    public void AFluidSize_Compiles()
    {
        var display = TypeStyle.OfSize(40, FontWeight.Bold).WithFluidSize(34, 4.2f, 54);
        _ = Text("Escolha o seu distrito.", styleOverride: display, headingLevel: 1);
    }

    /// <summary>Icons and Components (0.2.0-preview.61): a drawing at its column's width, a
    /// child placed by fraction and shifted by its own size, and an auto-filling grid.</summary>
    [Fact]
    public void TheLayoutThatFollowsItsBox_Compiles()
    {
        var mark = new VectorDrawing(0, 0, 3, 1, []);
        var tooltip = Text("Lisboa", TypeRole.Label);

        _ = new Drawing(mark, SizeValue.Fill, label: "eQuantic");
        _ = Positioned(tooltip, top: -16, startFraction: 0.5f, topFraction: 0.25f, shiftX: -0.5f, shiftY: -1f);
        _ = Grid([GridTrack.AutoFill(210)], gap: 10, width: SizeValue.Fill, children: [tooltip]);
    }

    /// <summary>Components, "An embedded page: WebFrame" (#1228): a payment provider's checkout,
    /// handed the payment so that it offers the device's wallet.</summary>
    [Fact]
    public void TheEmbeddedCheckout_Compiles()
    {
        _ = WebFrame(WebContent.Url("https://checkout.example.com/pay"), "Payment",
            sandbox: WebSandbox.Scripts | WebSandbox.SameOrigin | WebSandbox.Forms | WebSandbox.Popups
                | WebSandbox.Payment);
    }

    /// <summary>TableView (#982): Excel's sheet behind a pinned header row and column, a mark, and a
    /// request that reveals the last row.</summary>
    [Fact]
    public void TheTableView_Compiles()
    {
        var theme = PhotonTheme.Instance;
        var version = 1;

        _ = TableView(
            rows: new eQuantic.UI.Components.UniformTableSpans(1_048_576 + 1, 28),
            columns: new eQuantic.UI.Components.SparseTableSpans(16_384 + 1, 96, new Dictionary<int, double> { [0] = 48, [3] = 180 }),
            cellBuilder: (row, column) => Text(row == 0 ? $"{column}" : $"{row}:{column}", TypeRole.Caption),
            pinnedRows: 1,
            pinnedColumns: 1,
            width: SizeValue.Fill,
            height: SizeValue.Fill,
            marks: [TableViewCell(row: 4, column: 2, Box(new BoxStyle { BorderWidth = 2, BorderColor = theme.FocusRing }),
                mark: true, rows: 3, columns: 2)],
            request: new TableScrollRequest(version) { Reveal = new TableRange(1_048_575, 0) },
            label: "Sheet");
    }

    /// <summary>TableView read as a grid (#1081): its headers, the selection and the current cell.</summary>
    [Fact]
    public void TheTableViewAsAGrid_Compiles()
    {
        var rows = new eQuantic.UI.Components.UniformTableSpans(1_048_576 + 1, 28);
        var columns = new eQuantic.UI.Components.UniformTableSpans(16_384 + 1, 96);
        VisualNode? Cell(int row, int column) => Text($"{row}:{column}", TypeRole.Caption);

        _ = TableView(rows, columns, Cell,
            pinnedRows: 1,
            pinnedColumns: 1,
            label: "Ledger",
            role: TableRole.Grid,
            cellRole: (row, column) => row == 0 ? TableCellRole.ColumnHeader
                : column == 0 ? TableCellRole.RowHeader
                : TableCellRole.Cell,
            selected: (row, column) => column == 2 && row >= 2 && row <= 4,
            current: new TableRange(3, 2));
    }

    /// <summary>Spreadsheet (#995): a sheet the viewport builds lazily on both axes, and the theme's
    /// sheet slot naming two of its colours.</summary>
    [Fact]
    public void TheSpreadsheet_Compiles()
    {
        var ledger = new eQuantic.UI.Sheet.SheetController(rows: 100_000, cols: 100);
        ledger.Document.SetCell(new eQuantic.UI.Sheet.CellRef(0, 0), "Ledger");

        _ = Spreadsheet(ledger, height: SizeValue.Fill);
        _ = Ledger(PhotonTheme.Instance);

        // In an app's theme: `public SheetTheme? Sheet => Ledger(this);`
        static SheetTheme Ledger(IAppTheme theme) => new()
        {
            Gridline = theme.BorderStrong,
            RangeFill = theme.FocusRing.WithOpacity(0.12f),
        };
    }

    [Fact]
    public void TheDeclarativeSnippets_Compile()
    {
        var theme = PhotonTheme.Instance;

        _ = Row(gap: Space.S3, main: MainAlign.SpaceBetween, cross: CrossAlign.Start,
            children: [Text("a", TypeRole.BodyM)]);
        _ = Column(gap: Space.S2, wrap: true, runGap: Space.S4, children: [Text("b", TypeRole.BodyM)]);
        _ = Text("42", TypeRole.Display, align: TextAlignment.Center, tabular: true);
        _ = Column(Space.S3, children: [Text("c", TypeRole.BodyM)]);

        _ = new BoxStyle
        {
            BorderWidth = 1,
            BorderColor = theme.Border,
            BorderSides = BorderSides.Top,
        };
        _ = new BoxStyle
        {
            BorderWidth = 3,
            BorderColor = theme.Colors(Variant.Primary).Base,
            BorderSides = BorderSides.Start,
        };

        // The claims added for preview.15–18.
        _ = new TextRun("Build()", Mono: true) { StyleOverride = TypeStyle.OfSize(13.5f, FontWeight.Regular) };
        _ = Simulated(SimulatedState.Hovered | SimulatedState.Pressed, Text("x", TypeRole.BodyM));
        _ = InFlow(Text("x", TypeRole.BodyM));
        _ = InView(Text("Heading", TypeRole.Heading), visible => { _ = visible; });
        _ = new InView(Text("x", TypeRole.BodyM), _ => { }) { Threshold = 0.9f };
        var corner = new Stack();
        corner.Add(new Positioned(Text("x", TypeRole.BodyM), top: 0, end: 0));

        _ = new DocPage();
        _ = new LiveRates(null!);
        _ = new ProfilePage(null!);

        // DesignSystem, "Naming a typeface" (preview.51). Both halves, because the page shows both:
        // a theme naming its faces, and a node naming one for itself.
        IAppTheme brand = new BrandTheme(PhotonTheme.Instance);
        _ = brand.MonoFamily;
        _ = brand.Type(TypeRole.Heading).Family;
        _ = new Text("git log", TypeRole.Caption) { Mono = true };
        _ = new Text("git log", TypeRole.Caption)
        {
            StyleOverride = brand.Type(TypeRole.Caption) with { Family = new("Fira Code"), Mono = true },
        };

        // DesignSystem, "Shipping a typeface" (preview.66): a theme naming a family its catalog ships,
        // and a library declaring the files of its own.
        _ = new ShippedTheme(PhotonTheme.Instance).TextFamily;
        _ = Roboto.Files;

        // Styling, "A state changes any member of the style" (preview.60): the card that lifts and
        // deepens its glow on hover, the hover that keeps a resting rotation, and the one that
        // undoes it.
        var glow = theme.Colors(Variant.Primary).Base;
        _ = Box(new BoxStyle
        {
            Background = theme.Surface,
            Elevation = 1,
            Shadows = [new ShadowSpec(12, 30, -12, glow)],
            Transition = new TransitionSpec(StyleChannels.Transform | StyleChannels.Shadow),
            Hover = new StyleDiff
            {
                Transform = Transform2D.Translate(0, -2),
                Elevation = 3,
                Shadows = [new ShadowSpec(16, 36, -12, glow)],
            },
        }, Text("content", TypeRole.BodyM));
        _ = new StyleDiff { Transform = Transform2D.Rotate(2).WithTranslate(0, -2) };
        _ = new StyleDiff { Transform = Transform2D.Scale(1) };

        // Styling, "A control's press and focus" (preview.61): a control that presses in and takes a
        // focus border from the keyboard.
        Action save = () => { };
        _ = Pressable(Box(new BoxStyle
        {
            Background = theme.Surface,
            Elevation = 2,
            Transition = new TransitionSpec(StyleChannels.Transform, Motion.BaseMs),
            Pressed = new StyleDiff { Transform = Transform2D.Scale(0.985f) },
            Focus = new StyleDiff { BorderColor = theme.FocusRing, BorderWidth = 2 },
        }, Text("label", TypeRole.BodyM)), onPressed: save);

        // Styling, "A header the content scrolls under" (preview.61): a floating header that frosts,
        // raises and draws its bottom hairline once the page scrolls under it.
        var header = Text("header", TypeRole.BodyM);
        _ = new Pinned(header)
        {
            Float = true,
            ScrolledStyle = new StyleDiff
            {
                Background = theme.Surface,
                Elevation = 2,
                BorderWidth = 1,
                BorderColor = theme.Border,
                BackdropBlur = 24,
            },
            Transition = new TransitionSpec(StyleChannels.Colors | StyleChannels.Shadow),
        };
        _ = Primitives.Pinned.ScrolledThreshold;
        _ = Primitives.Pinned.ScrolledBase;

        // Styling, "What is inside a header that scrolls" (preview.70): a large badge that gives way to
        // a small mark with the name once the page scrolls, both inside the one link.
        var badge = Text("F", TypeRole.Display);
        var markAndName = Text("Francine", TypeRole.Label);
        var glide = new TransitionSpec(StyleChannels.Opacity | StyleChannels.Transform, Motion.BaseMs);
        _ = Pinned(Link("/", Stack(Alignment.CenterStart, children: [
            Box(new BoxStyle
            {
                Transition = glide,
                Scrolled = new StyleDiff { Opacity = 0, Transform = Transform2D.Scale(0.6f) },
            }, badge),
            Box(new BoxStyle
            {
                Opacity = 0,
                Transition = glide,
                Scrolled = new StyleDiff { Opacity = 1 },
            }, markAndName),
        ]), label: "Home"));

        // WriteOnceComponents, "A weight of zero takes no share" (preview.61, #680): a picture that
        // starts at 540 and never grows, beside text that takes the rest.
        var story = new Row(gap: Space.S6) { Wrap = true, Width = SizeValue.Fill };
        story.Add(Flexible(Text("picture", TypeRole.BodyM), flex: 0, basis: 540));
        story.Add(Flexible(Text("story", TypeRole.BodyM), flex: 1, basis: 380));

        // EmailRealizer, the welcome email, whole (#684, #694).
        _ = new WelcomeEmail("Edgar");
    }

    /// <summary>Security, "A page that requires authorization" (preview.61): a page that only the
    /// backoffice may open, which says so itself.</summary>
    [Page("/backoffice/queue")]
    [Primitives.Authorize(Policy = "Backoffice")]
    private sealed class VerificationQueuePage : Primitives.StatefulComponent, IServerPrefetch
    {
        public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken) => Task.CompletedTask;

        public override VisualNode Build(ComponentContext context) => Text("queue", TypeRole.BodyM);
    }

    /// <summary>Components, "A subtree is heard as one, or not at all" (preview.69, #1567): a switch heard as
    /// its row, and the initials an avatar draws, which a reader passes over.</summary>
    private sealed class SubtreeReadingPage : Primitives.StatefulComponent
    {
        private bool _on;

        private void Toggle() => SetState(() => _on = !_on);

        public override VisualNode Build(ComponentContext context) => Column(children:
        [
            MergeSemantics(Row(children: [Text("Wi-Fi"), Switch(_on, Toggle)])),
            Row(children: [ExcludeSemantics(Avatar("JD")), Text("Jane Doe")]),
        ]);
    }

    /// <summary>Components, "ContextTarget and ContextMenu: a menu asked for anywhere" (preview.69, #1531): a
    /// file's card with its menu, and the blocks it is made of for a panel of another look.</summary>
    private sealed class FileMenuPage : Primitives.StatefulComponent
    {
        private sealed record File(string Name);

        private readonly File file = new("README.md");
        private Point? _at;
        private bool _open;

        private static VisualNode FileCard(File file) => Text(file.Name, TypeRole.BodyM);

        private static void Run(File file, int index) { }

        public override VisualNode Build(ComponentContext context) =>
            ContextMenu(
                FileCard(file),
                [new MenuItem("Rename"), new MenuItem("Delete") { Destructive = true }],
                index => Run(file, index));

        /// <summary>The blocks under it: a target under an anchored panel opened at the request's point.</summary>
        public VisualNode OwnPanel(VisualNode panel) =>
            new Anchored(ContextTarget(FileCard(file), request => SetState(() => (_at, _open) = (request.At, true))), panel)
            {
                At = _at,
                Open = _open,
                OnDismiss = () => SetState(() => _open = false),
            };
    }

    /// <summary>Components, "Sortable: lists a pointer puts in order" (preview.69, #1535): a board's column.</summary>
    private sealed class BoardColumnPage : Primitives.StatefulComponent
    {
        private readonly List<(string Id, string Title)> _cards = [("c1", "Draw the tree")];
        private SortDrop? _landing;

        private void Move(string item, string to, int index) { }

        public override VisualNode Build(ComponentContext context) =>
            Sortable(
                Column(gap: Space.S2, children: [.. _cards.Select(card => SortableItem(Text(card.Title, TypeRole.BodyM), card.Id))]),
                id: "todo",
                onDropped: drop => SetState(() => Move(drop.Item, drop.To, drop.Index)),
                group: "board",
                onHovered: drop => SetState(() => _landing = drop),
                accepts: drop => !drop.Item.StartsWith("locked", StringComparison.Ordinal));
    }

    /// <summary>Components, "Sortable", the words are the list's (#1564): a column that says the keyboard's steps
    /// in the app's own words.</summary>
    private sealed class OwnWordsColumnPage : Primitives.StatefulComponent
    {
        private readonly List<(string Id, string Title)> _cards = [("c1", "Draw the tree")];

        private void Move(string item, string to, int index) { }

        public override VisualNode Build(ComponentContext context) =>
            Sortable(
                Column(gap: Space.S2, children: [.. _cards.Select(card => SortableItem(Text(card.Title, TypeRole.BodyM), card.Id))]),
                id: "todo",
                onDropped: drop => SetState(() => Move(drop.Item, drop.To, drop.Index)),
                label: "To do",
                announcements: new SortAnnouncements(
                    Instructions: "Space picks the card up, the arrows move it, Space drops it.",
                    PickedUp: "{0} lifted from {3}.",
                    Moved: "{0}: slot {1} of {2}.",
                    MovedInto: "{0} on top of {1}.",
                    Refused: "{0} cannot go there.",
                    Dropped: "{0} placed in {3}, slot {1}.",
                    DroppedInto: "{0} placed on {1}.",
                    PutBack: "{0} is back where it was."));
    }

    /// <summary>Components, "DataTable: columns a person orders and sizes" (#1538): a table whose page keeps the
    /// order a person leaves its columns in and the widths they give them.</summary>
    private sealed class OrderedColumnsPage : Primitives.StatefulComponent
    {
        private readonly DataColumn[] _columns = [new("Name", GridTrack.Flex()), new("Size", GridTrack.Fixed(120))];
        private IReadOnlyList<int>? _order;

        public override VisualNode Build(ComponentContext context) =>
            new DataTable(_columns, [])
            {
                ColumnOrder = _order,
                OnColumnsReordered = order => SetState(() => _order = order),
                OnColumnResized = (column, width) => SetState(() =>
                    _columns[column] = _columns[column] with { Track = GridTrack.Fixed(width) }),
            };
    }

    /// <summary>Components, "Sortable", the look is the theme's (#1602): a theme's look of a carried card, and a
    /// column's own laid over it.</summary>
    private sealed class StyledColumnPage : Primitives.StatefulComponent
    {
        private readonly List<(string Id, string Title)> _cards = [("c1", "Draw the tree")];

        private void Move(string item, string to, int index) { }

        internal static eQuantic.UI.Server.UIOptions Configure(eQuantic.UI.Server.UIOptions ui) =>
            ui.UseTheme(PhotonTheme.Instance.With(sortable: new SortableTheme { CopyElevation = 5, MaxLean = 6 }));

        public override VisualNode Build(ComponentContext context) =>
            Sortable(
                Column(gap: Space.S2, children: [.. _cards.Select(card => SortableItem(Text(card.Title, TypeRole.BodyM), card.Id))]),
                id: "done",
                onDropped: drop => SetState(() => Move(drop.Item, drop.To, drop.Index)),
                style: new SortableTheme { LiftedOpacity = 0.25f, MaxLean = 0 });
    }

    /// <summary>Components, "Sortable", depths (#1594): an outline whose rows have depths, and the depth a drop lands
    /// at.</summary>
    private sealed class OutlinePage : Primitives.StatefulComponent
    {
        private sealed record OutlineRow(string Id, string Title, int Depth);

        private readonly List<OutlineRow> rows = [new("intro", "Introduction", 0), new("why", "Why", 1)];

        private static VisualNode Line(OutlineRow row) => Text(row.Title, TypeRole.BodyM);

        private void Move(string item, int index, int? depth) { }

        public override VisualNode Build(ComponentContext context) =>
            Sortable(
                Column(children: [.. rows.Select(row => SortableItem(Line(row), row.Id, depth: row.Depth))]),
                id: "outline",
                onDropped: drop => SetState(() => Move(drop.Item, drop.Index, drop.Depth)),
                nests: true,
                indent: 24);
    }

    /// <summary>Components, "TreeView: a tree the keyboard walks" (preview.69, #1530): a repository's
    /// files, the top folder from the server and every folder below it loaded as it opens.</summary>
    private sealed class FilesPage : Primitives.StatefulComponent
    {
        private sealed record Entry(string Path, string Name, bool IsDirectory);

        private readonly List<Entry> Top = [];
        private string? _open;

        private Task<List<Entry>> ListFolder(string path) => Task.FromResult(new List<Entry>());

        private TreeNode Node(Entry entry) => new(entry.Path, entry.Name)
        {
            Icon = entry.IsDirectory ? Icons.Menu : Icons.Copy,
            IsLeaf = !entry.IsDirectory,
        };

        public override VisualNode Build(ComponentContext context) =>
            TreeView(
                roots: Top.Select(entry => new TreeNode(entry.Path, entry.Name)
                {
                    Icon = entry.IsDirectory ? Icons.Menu : Icons.Copy,
                    IsLeaf = !entry.IsDirectory,
                }).ToList(),
                selectedKey: _open,
                onPressed: node => SetState(() => _open = node.Key),
                loadChildren: async folder => (await ListFolder(folder.Key)).Select(Node).ToList(),
                label: "Repository",
                height: SizeValue.Fill);

        /// <summary>The vocabulary under it, for a composite of an app's own.</summary>
        public static VisualNode OwnComposite() =>
            new Navigable(_ => { }, [
                new Pressable(Text("src", TypeRole.BodyM), () => { })
                {
                    Role = PressableRole.TreeItem,
                    Expanded = false,
                    TreePlace = new TreePlace(Level: 1, PositionInSet: 1, SizeOfSet: 1),
                },
            ])
            {
                Role = NavigableRole.Tree,
                OnTextSearch = _ => { },
            };
    }

    /// <summary>
    /// The EmailRealizer page's welcome email, as the page writes it, with an address standing in
    /// for its generated <c>Assets.Logo</c>. Its logo gave a width and no height (#684), and its
    /// button was a <c>Button</c> with an <c>href</c> the factory does not have, which an email
    /// refuses anyway because a Button is a Pressable (#694): the bulletproof button is a Link
    /// around a painted Box.
    /// </summary>
    private sealed class WelcomeEmail(string name) : Primitives.StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) =>
            Column(gap: Space.S4, children: [
                Image("https://cdn.example.com/logo.png", width: 132, height: 26),
                Text($"Welcome, {name}", TypeRole.Heading),
                Link("https://example.com/confirm", Box(new BoxStyle
                {
                    Background = context.Theme.Colors(Variant.Primary).Base,
                    Padding = EdgeInsets.Symmetric(Space.S4, Space.S2),
                }, Text("Confirm your address", TypeRole.Label, context.Theme.Colors(Variant.Primary).OnBase))),
            ]);
    }

    /// <summary>The theme the DesignSystem page shows, whose body is elided there as "the rest
    /// delegates to inner" — written out here so the lines that are NOT elided compile.</summary>
    private sealed class BrandTheme(IAppTheme inner) : IAppTheme
    {
        public FontFamily? TextFamily => new("IBM Plex Sans");
        public FontFamily? MonoFamily => new("JetBrains Mono");
        public TypeStyle Type(TypeRole role) => role is TypeRole.Display or TypeRole.Heading
            ? inner.Type(role) with { Family = new("Newsreader") }
            : inner.Type(role);

        public ColorToken Background => inner.Background;
        public ColorToken Surface => inner.Surface;
        public ColorToken SurfaceSubtle => inner.SurfaceSubtle;
        public ColorToken SurfaceHighlight => inner.SurfaceHighlight;
        public ColorToken Border => inner.Border;
        public ColorToken BorderStrong => inner.BorderStrong;
        public ColorToken TextPrimary => inner.TextPrimary;
        public ColorToken TextSecondary => inner.TextSecondary;
        public ColorToken TextMuted => inner.TextMuted;
        public ColorToken TextInverse => inner.TextInverse;
        public ColorToken FocusRing => inner.FocusRing;
        public ColorToken LinkColor => inner.LinkColor;
        public ColorToken Scrim => inner.Scrim;
        public float DisabledOpacity => inner.DisabledOpacity;
        public VariantColors Colors(Variant variant) => inner.Colors(variant);
        public ShadowSpec Elevation(int level) => inner.Elevation(level);
        public float Shape(ShapeScale scale) => inner.Shape(scale);
    }

    /// <summary>What the SDK writes for an app with the files of one family under its Assets folder,
    /// which the "Shipping a typeface" theme names.</summary>
    private static class Fonts
    {
        public static FontFamily AtkinsonHyperlegibleNext { get; } = new("Atkinson Hyperlegible Next",
            new FontFile("Acme.App", "eQuantic.UI.Fonts/AtkinsonHyperlegibleNext-Variable.ttf", 200, 800));
    }

    /// <summary>The "Shipping a typeface" theme, its elided body written out.</summary>
    private sealed class ShippedTheme(IAppTheme inner) : IAppTheme
    {
        public FontFamily? TextFamily => Fonts.AtkinsonHyperlegibleNext;

        public ColorToken Background => inner.Background;
        public ColorToken Surface => inner.Surface;
        public ColorToken SurfaceSubtle => inner.SurfaceSubtle;
        public ColorToken SurfaceHighlight => inner.SurfaceHighlight;
        public ColorToken Border => inner.Border;
        public ColorToken BorderStrong => inner.BorderStrong;
        public ColorToken TextPrimary => inner.TextPrimary;
        public ColorToken TextSecondary => inner.TextSecondary;
        public ColorToken TextMuted => inner.TextMuted;
        public ColorToken TextInverse => inner.TextInverse;
        public ColorToken FocusRing => inner.FocusRing;
        public ColorToken LinkColor => inner.LinkColor;
        public ColorToken Scrim => inner.Scrim;
        public float DisabledOpacity => inner.DisabledOpacity;
        public VariantColors Colors(Variant variant) => inner.Colors(variant);
        public TypeStyle Type(TypeRole role) => inner.Type(role);
        public ShadowSpec Elevation(int level) => inner.Elevation(level);
        public float Shape(ShapeScale scale) => inner.Shape(scale);
    }

    /// <summary>The library's family the "Shipping a typeface" page declares, as it is written there.</summary>
    public static FontFamily Roboto { get; } = new("Roboto",
        new FontFile("Acme.Theme", "Fonts/Roboto-Variable.ttf", 100, 900),
        new FontFile("Acme.Theme", "Fonts/Roboto-Italic-Variable.ttf", 100, 900, Italic: true));

    /// <summary>The relying party the Capabilities page's passkey sign-in calls: a Web Authentication
    /// library writes the options and verifies the answer.</summary>
    private static class RelyingParty
    {
        public static Task<string> RequestOptionsAsync() => Task.FromResult("{}");

        public static Task<bool> VerifyAsync(string credential) => Task.FromResult(credential.Length > 0);
    }

    /// <summary>Capabilities, "Passkeys" (preview.65): a sign-in that asks for the options when the
    /// screen opens and starts the ceremony first thing in the press, as the page writes it.</summary>
    [Page("/sign-in")]
    private sealed class SignInPage(IPasskeys? passkeys = null) : Primitives.StatefulComponent
    {
        private string? _options;
        private string _said = "";

        // Asked for when the screen opens, so the press can start the ceremony at once.
        protected override void OnMount() => _ = Prepare();

        [ServerAction]
        public Task<string> BeginSignIn() => RelyingParty.RequestOptionsAsync();   // your Web Authentication library

        [ServerAction]
        public Task<bool> FinishSignIn(string credential) => RelyingParty.VerifyAsync(credential);

        private async Task Prepare()
        {
            if (passkeys is not { IsAvailable: true }) return;
            var options = await BeginSignIn();
            SetState(() => _options = options);
        }

        private async Task SignIn()
        {
            if (passkeys is null || _options is not { } options) return;
            var result = await passkeys.AuthenticateAsync(options);   // the first thing the press does
            var signedIn = result.Outcome == PasskeyOutcome.Succeeded && await FinishSignIn(result.Credential!);
            var next = await BeginSignIn();
            SetState(() =>
            {
                _said = signedIn ? "Welcome back." : "Not signed in.";
                _options = next;
            });
        }

        public override VisualNode Build(ComponentContext context) =>
            Column(gap: Space.S3, children: [
                Button("Sign in with a passkey", onPressed: () => _ = SignIn()),
                Text(_said, TypeRole.BodyM),
            ]);
    }

    // Capabilities.md, "Choosing files" (#1213): a picker beside a drop area, both answering PickedFiles.
    private sealed class ImportCard(IFilePicker? picker) : Primitives.StatefulComponent
    {
        private static readonly FileFilter[] Sheets = [new FileFilter("Spreadsheets", "csv")];
        private bool _over;
        private string _status = "Nothing imported yet";

        public override VisualNode Build(ComponentContext context) =>
            DropTarget(
                Column(gap: Space.S3, children: [
                    Text(_over ? "Release to import" : _status, TypeRole.BodyM),
                    Button("Choose a file", onPressed: Choose),
                ]),
                onDropped: files => _ = Import(files[0]),
                onDragOverChanged: over => SetState(() => _over = over));

        private async void Choose()
        {
            if (picker is not null && await picker.PickFileAsync(Sheets) is { } file)
                await Import(file);
        }

        private async Task Import(PickedFile file)
        {
            var text = await file.ReadAllTextAsync();
            SetState(() => _status = $"{file.Name}: {text.Split('\n').Length} rows");
        }
    }

    // Capabilities.md, "Saving a file the page made" (#1022): codes made in the page, offered as a file.
    private sealed class RecoveryCodes(IFileSaver? saver, ITextClipboard? clipboard) : Primitives.StatefulComponent
    {
        private const string Codes = "7KQ2-M9XD\nP4TN-8WCE\nH6JB-2RZF\n";
        private string _said = "";

        public override VisualNode Build(ComponentContext context) =>
            Column(gap: Space.S3, children: [
                Text(Codes, TypeRole.BodyL),
                Row(gap: Space.S3, children: [
                    Button("Download .txt", onPressed: () => _ = Download()),
                    Button("Copy all", Variant.Outline, onPressed: () => clipboard?.Write(Codes)),
                ]),
                Text(_said, TypeRole.BodyM),
            ]);

        private async Task Download()
        {
            if (saver is null) return;
            var outcome = await saver.SaveTextAsync("recovery-codes.txt", Codes);
            SetState(() => _said = outcome == FileSaveOutcome.Cancelled ? "Nothing was saved." : "Saved.");
        }
    }

    // Styling.md, "A colour the text inside inherits" (#614): a label and its arrow brighten together.
    private static Pressable ReadTheDocs(IAppTheme theme, Action open) =>
        Pressable(Box(new BoxStyle
        {
            Background = theme.Surface,
            Foreground = theme.TextSecondary,
            Hover = new StyleDiff { Foreground = theme.TextPrimary },
        }, Row(gap: Space.S2, children: [
            Text("Read the docs", TypeRole.Label),
            Icon(Icons.ChevronRight, 16),
        ])), onPressed: open);

    // Uploads.md (#1215): the policy the page and the server both read, a page that takes files with
    // FileUpload and receives them in its Server Action, and a component drawn over IUploads.
    private static class Uploads
    {
        public static readonly UploadPolicy Models = new("models",
            filters: [new FileFilter("3D models", "stl", "obj", "3mf")],
            maxSize: 250 * 1024 * 1024);
    }

    [Page("/models")]
    private sealed class ModelsScreen : Primitives.StatefulComponent
    {
        private string _saved = "";

        [ServerAction]
        public async Task<string> SaveModel(UploadedFile model)
        {
            await using var content = await model.OpenRead();
            // … store it where the app keeps its data …
            await model.Delete();
            return model.Name;
        }

        public override VisualNode Build(ComponentContext context) =>
            Column(gap: Space.S4, children: [
                FileUpload(Uploads.Models, onCompleted: model => _ = Save(model)),
                Text(_saved, TypeRole.BodyM),
            ]);

        private async Task Save(UploadedFile model)
        {
            var name = await SaveModel(model);
            SetState(() => _saved = $"Saved {name}");
        }
    }

    private sealed class ModelButton(IFilePicker? picker, IUploads? uploads) : Primitives.StatefulComponent
    {
        private Upload? _upload;
        private IDisposable? _changes;

        protected override void OnUnmount() => _changes?.Dispose();

        public override VisualNode Build(ComponentContext context) => _upload switch
        {
            null => Button("Send a model", onPressed: () => _ = Choose()),
            { State: UploadState.Failed } failed => Text(failed.Error ?? "", TypeRole.BodyM),
            { State: UploadState.Completed } => Text("Sent", TypeRole.BodyM),
            var sending => ProgressBar((float)sending.Sent / sending.Size, label: "Sending the model"),
        };

        private async Task Choose()
        {
            if (picker is null || uploads is null) return;
            if (await picker.PickFileAsync(Uploads.Models.Filters) is not { } file) return;
            var upload = uploads.Start(file, Uploads.Models);
            _changes?.Dispose();
            _changes = upload.Subscribe(_ => SetState(() => { }));
            SetState(() => _upload = upload);
        }
    }

    /// <summary>ServerIntegration's UseLayout: a layout takes the page as its one constructor
    /// parameter, keeps its own state, and reads the location the page is shown at.</summary>
    private sealed class ConsoleLayout(VisualNode body) : Primitives.StatefulComponent
    {
        private bool _navOpen;

        public override VisualNode Build(ComponentContext context) =>
            Column(children: [
                Button(_navOpen ? "Close menu" : "Menu", Variant.Secondary,
                    onPressed: () => SetState(() => _navOpen = !_navOpen)),
                Text($"You are at {Navigator.Of(context).Location}", TypeRole.Label),
                body,
            ]);
    }

    /// <summary>The declaration ServerIntegration shows, which refuses a layout that cannot take the page.</summary>
    private static void DeclaresTheLayout(eQuantic.UI.Server.UIOptions options) => options.UseLayout<ConsoleLayout>();

    /// <summary>Localization's right-to-left section (#1568, #1569): a subtree that reads the other way, a
    /// glyph that turns with the direction, and a glyph of the app's own that says it points somewhere.</summary>
    private sealed class BackLink(string snippet) : Primitives.StatelessComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            var back = Icons.ChevronLeft;
            var own = new IconGlyph("back", "M15 6l-6 6 6 6", IconGlyphStyle.Stroke, MatchTextDirection: true);
            return Column(children: [
                Icon(back, 16),
                Icon(own, 16),
                Directionality(TextDirection.LeftToRight, new CodeBlock(snippet, "csharp")),
            ]);
        }
    }
}
