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

        // WriteOnceComponents, "A weight of zero takes no share" (preview.61, #680): a picture that
        // starts at 540 and never grows, beside text that takes the rest.
        var story = new Row(gap: Space.S6) { Wrap = true, Width = SizeValue.Fill };
        story.Add(Flexible(Text("picture", TypeRole.BodyM), flex: 0, basis: 540));
        story.Add(Flexible(Text("story", TypeRole.BodyM), flex: 1, basis: 380));

        // EmailRealizer, the welcome email, whole (#684, #694).
        _ = new WelcomeEmail("Edgar");
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
