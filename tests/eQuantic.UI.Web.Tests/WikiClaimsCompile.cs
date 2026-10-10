using eQuantic.UI.Web;
using eQuantic.UI.Primitives;
using Microsoft.Extensions.DependencyInjection;
using static eQuantic.UI.Components.UI;

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
                .FindAsync(RouteValues.Current.Param("slug"), cancellationToken);

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
            StyleOverride = brand.Type(TypeRole.Caption) with { Family = "Fira Code", Mono = true },
        };

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

    /// <summary>The theme the DesignSystem page shows, whose body is elided there as "the rest
    /// delegates to inner" — written out here so the two lines that are NOT elided compile.</summary>
    private sealed class BrandTheme(IAppTheme inner) : IAppTheme
    {
        public TypeStyle Type(TypeRole role) => inner.Type(role) with { Family = "IBM Plex Sans" };
        public string? MonoFamily => "JetBrains Mono";

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
}
