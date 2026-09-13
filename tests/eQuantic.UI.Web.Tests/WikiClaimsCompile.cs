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
