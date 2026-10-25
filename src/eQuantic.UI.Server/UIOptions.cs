using eQuantic.UI.Primitives;
using System.Reflection;
using eQuantic.UI.Server.Assets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace eQuantic.UI.Server;

/// <summary>
/// Configuration options for UI services.
/// </summary>
public class UIOptions
{
    internal List<Assembly> AssembliesToScan { get; } = new();

    /// <summary>
    /// Additional assemblies whose complex types are permitted as Server Action parameters,
    /// beyond the scanned application assemblies. Use this to opt in DTOs declared in shared
    /// libraries. Framework/system/third-party types are otherwise rejected by default.
    /// </summary>
    public HashSet<Assembly> AllowedDeserializationAssemblies { get; } = new();

    /// <summary>
    /// Lets pages served from <paramref name="origins"/> call the app's Server Actions besides its own
    /// host: a page on another domain than its actions. Each is a bare <c>scheme://host[:port]</c>, and
    /// they add to <c>EQuantic:ServerActions:AllowedOrigins</c> in configuration.
    /// </summary>
    public UIOptions AllowServerActionOrigins(params string[] origins)
    {
        ArgumentNullException.ThrowIfNull(origins);
        ServerActionOrigins.AddRange(origins);
        return this;
    }

    internal List<string> ServerActionOrigins { get; } = new();

    internal List<(Type ServiceType, Type ImplementationType)> AssetProviders { get; } = new();
    internal List<Action<IServiceCollection>> ServiceRegistrations { get; } = new();
    internal List<Action<IEndpointRouteBuilder>> EndpointConfigurations { get; } = new();

    /// <summary>
    /// Configuration for the HTML shell (index.html).
    /// </summary>
    public HtmlShellOptions HtmlShell { get; } = new();

    /// <summary>
    /// Enables Server-Side Rendering (SSR) for SEO optimization.
    /// When enabled, pages will be pre-rendered on the server and sent as HTML.
    /// Default is true.
    /// </summary>
    /// <remarks>
    /// SSR provides:
    /// - Better SEO (search engines can index the content)
    /// - Faster First Contentful Paint (FCP)
    /// - Social media preview cards (Open Graph)
    ///
    /// Individual pages can opt-out using [Page(DisableSsr = true)].
    /// </remarks>
    public bool EnableSsr { get; set; } = true;

    /// <summary>
    /// Phase 3 hot reload (SSE + eqc rebuild on save). Null = auto: on in Development, and under
    /// <c>dotnet watch</c> whatever the environment, since an app run without a launch profile is a
    /// Production one.
    /// </summary>
    public bool? HotReload { get; set; }

    /// <summary>
    /// Whether this app rebuilds its modules on a save and reloads the browsers watching it:
    /// <see cref="HotReload"/> when the app says, otherwise the Development environment or a run under
    /// <c>dotnet watch</c>, which sets <c>DOTNET_WATCH</c> to 1 on the app it runs. One decision, read
    /// by the stream that announces a rebuild, the cache of the modules a rebuild rewrites and the
    /// client that listens, which agreed before only while nothing set them apart (#627). Its first
    /// answer, the one MapUI maps the stream by at startup, is every later reader's, whatever the
    /// process's environment does after.
    /// </summary>
    internal bool HotReloads(IHostEnvironment environment) =>
        _hotReloads ??= HotReloads(environment, Environment.GetEnvironmentVariable("DOTNET_WATCH"));

    private bool? _hotReloads;

    /// <summary><see cref="HotReloads(IHostEnvironment)"/> with the variable dotnet watch sets given.</summary>
    internal bool HotReloads(IHostEnvironment environment, string? dotnetWatch) =>
        HotReload ?? (environment.IsDevelopment() || dotnetWatch == "1");

    public UIOptions WithSsr(bool enabled = true)
    {
        EnableSsr = enabled;
        return this;
    }

    /// <summary>
    /// The write-once design-system theme (an <see cref="eQuantic.UI.Primitives.IAppTheme"/>) this app
    /// renders with — <c>PhotonTheme.Instance</c>, <c>MaterialTheme.Instance</c>,
    /// <c>MaterialTheme.FromSeed(seed)</c>, or a brand theme. Null until set. When set, the SSR pipeline
    /// lowers the shared components with it, emits its token CSS, and bridges it to the client (boot
    /// calls <c>setPhotonTheme</c>) so hydration + client re-renders match the server.
    /// </summary>
    internal eQuantic.UI.Primitives.IAppTheme? Theme { get; private set; }

    /// <summary>The mode the app DECLARED, or null to follow the operating system (the default).</summary>
    internal eQuantic.UI.Primitives.ThemeMode? InitialThemeMode { get; private set; }

    /// <summary>The cookie the theme is remembered in, or null when the app has turned it off.</summary>
    internal ThemeCookieOptions? ThemeCookie { get; private set; } = new();

    /// <summary>
    /// Name and lifetime of the cookie the visitor's theme choice is remembered in.
    /// <para>
    /// It is ONE setting because both halves must agree: the browser's controller writes this
    /// cookie and the server reads it. Configure them separately and they drift, at which point the
    /// server reads a name nobody writes — persistence stops working while every part of it still
    /// looks correct.
    /// </para>
    /// <para>
    /// Worth naming when two eQuantic apps share a domain and should not inherit each other's
    /// theme, or when an existing site already has a cookie convention.
    /// </para>
    /// </summary>
    public UIOptions UseThemeCookie(string name = "eq-theme", int days = 365)
    {
        ThemeCookie = new ThemeCookieOptions { Name = name, Days = days };
        return this;
    }

    /// <summary>
    /// Do not remember the theme at all — no cookie is written, and every visit starts from
    /// <see cref="UseInitialThemeMode"/> or the operating system.
    /// <para>
    /// For a site that must not set a cookie before consent. It is a build-time switch, so an app
    /// that wants to start persisting the moment a visitor accepts a banner needs its own hand on
    /// the writing rather than this.
    /// </para>
    /// </summary>
    public UIOptions WithoutThemeCookie()
    {
        ThemeCookie = null;
        return this;
    }

    /// <summary>
    /// How this page is reached in each of the app's other languages. Set it and every rendered
    /// page carries its own <c>rel="alternate" hreflang</c> set — one link per supported culture,
    /// the page ITSELF among them, plus <c>x-default</c> — instead of each page spelling the group
    /// out by hand and drifting the day a language is added.
    /// <para>
    /// The cultures come from the app's own <c>RequestLocalization</c>, because that is where a
    /// .NET app already declares them; the SDK ships no second list to keep in step. An app that
    /// never wired localization has one language, and one language has no alternates.
    /// </para>
    /// <para>
    /// The delegate answers a URL, absolute or rooted, or NULL when the page has no version in
    /// that language — a group that promises a translation which 404s is worse than a smaller
    /// group. <see cref="AlternateUrls"/> has the two common shapes ready.
    /// </para>
    /// <para>
    /// NAME THE LANGUAGES unless the app shares its localization options through DI. The middleware
    /// overload every sample uses — <c>app.UseRequestLocalization(o => …)</c> — builds its options
    /// INLINE and registers nothing, so asking the container answers with the invariant default and
    /// the head comes out empty. An app that calls
    /// <c>services.Configure&lt;RequestLocalizationOptions&gt;(…)</c> and then
    /// <c>app.UseRequestLocalization()</c> can leave this out and keep one list.
    /// </para>
    /// <example>
    /// <code>
    /// options.UseAlternateLinks(AlternateUrls.PathPrefix(), "en", "pt-BR", "es");
    /// options.UseAlternateLinks(r => r.Culture == "pt-BR" ? $"/pt{r.Request.Path}" : r.Request.Path);
    /// </code>
    /// </example>
    /// </summary>
    public UIOptions UseAlternateLinks(Func<AlternateRequest, string?> url, params string[] cultures)
    {
        AlternateUrl = url;
        AlternateCultures = cultures.Length > 0 ? cultures : null;
        return this;
    }

    /// <summary>
    /// Serve every page under a LANGUAGE PREFIX — <c>/pt-BR/pricing</c> — with the first culture
    /// named here staying unprefixed.
    /// <para>
    /// One declaration reaches the three places that have to agree: the endpoints each page is
    /// mapped at, the client route table the runtime navigates with, and the href every in-app
    /// link carries. A prefix the app never named matches no endpoint, so it 404s rather than
    /// serving one language under another language's URL.
    /// </para>
    /// <para>
    /// Negotiation stays ASP.NET's: this configures the platform's own
    /// <c>RequestLocalizationOptions</c> from the list (default, supported cultures, and its
    /// <c>RouteDataRequestCultureProvider</c> first, reading the <c>{culture:culture}</c> segment)
    /// and teaches the route constraint map the word <c>culture</c>. The app adds
    /// <c>app.UseRequestLocalization()</c> and is done. An app that prefers the middleware's lambda
    /// overload — which builds its options inline and sees nothing from DI — passes the same list
    /// there with <see cref="CultureRouteExtensions.UseCultureRoutes(RequestLocalizationOptions, string[])"/>.
    /// </para>
    /// <para>
    /// A bare URL asked for in a prefixed language (a cookie the switcher wrote, an Accept-Language)
    /// is redirected to its prefixed address, so what a reader sees, shares and bookmarks always
    /// names its language; and the <c>hreflang</c> group is emitted from the same map unless the
    /// app declared its own policy with <see cref="UseAlternateLinks"/>.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddUI(o => o.UseCultureRoutes("en", "pt-BR", "es"));
    /// …
    /// app.UseRequestLocalization();
    /// app.MapUI();
    /// </code>
    /// </example>
    public UIOptions UseCultureRoutes(params string[] cultures)
    {
        var map = CultureRouteMap.From(cultures);
        CultureRoutes = map;
        ServiceRegistrations.Add(services => services.AddCultureRoutes(map));
        return this;
    }

    /// <summary>The language-prefix policy, or null when the app never asked for one.</summary>
    public CultureRouteMap? CultureRoutes { get; private set; }

    /// <summary>The alternate-URL policy, or null when the app never set one. See
    /// <see cref="UseAlternateLinks"/>.</summary>
    public Func<AlternateRequest, string?>? AlternateUrl { get; private set; }

    /// <summary>The languages the group advertises, when the app named them here. Null means
    /// "ask the app's RequestLocalization" — see <see cref="UseAlternateLinks"/>.</summary>
    public IReadOnlyList<string>? AlternateCultures { get; private set; }

    /// <summary>
    /// Pin the app to one light/dark mode instead of following the operating system.
    /// <para>
    /// This drives <c>color-scheme</c>, which is the only thing every token actually reads: they
    /// resolve through <c>light-dark()</c>. Declaring a mode that did NOT reach
    /// <c>color-scheme</c> left two answers to one question — the page painted whatever the OS
    /// wanted while a toggle reported what the app had asked for, so a dark desktop got a dark
    /// page offering to "switch to dark".
    /// </para>
    /// <para>
    /// Leave it unset to follow the OS, which is what most apps want. Then the first paint is the
    /// OS's choice and a toggle's label is only settled once the browser's own controller resolves
    /// it at hydration — a server cannot know a preference it was never sent.
    /// </para>
    /// </summary>
    public UIOptions UseInitialThemeMode(eQuantic.UI.Primitives.ThemeMode mode)
    {
        InitialThemeMode = mode;
        return this;
    }

    /// <summary>Select the write-once theme for the whole app (SSR + client). See <see cref="Theme"/>.</summary>
    public UIOptions UseTheme(eQuantic.UI.Primitives.IAppTheme theme)
    {
        Theme = theme ?? throw new ArgumentNullException(nameof(theme));
        return this;
    }

    /// <summary>
    /// Registers an asset provider for a specific component type.
    /// The provider will supply additional or overriding assets for the component.
    /// </summary>
    public UIOptions WithAssetProvider<TProvider>() where TProvider : class
    {
        var providerType = typeof(TProvider);
        foreach (var iface in providerType.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IComponentAssetProvider<>))
            {
                AssetProviders.Add((iface, providerType));
                return this;
            }
        }

        throw new ArgumentException(
            $"{providerType.Name} does not implement IComponentAssetProvider<T>.",
            nameof(TProvider));
    }

    /// <summary>
    /// Registers a custom service registration callback.
    /// Used by packages to add services to the DI container during AddUI().
    /// </summary>
    public UIOptions RegisterServices(Action<IServiceCollection> registration)
    {
        ServiceRegistrations.Add(registration);
        return this;
    }

    /// <summary>
    /// Registers a custom endpoint configuration callback.
    /// Used by packages to map routes during MapUI().
    /// </summary>
    public UIOptions RegisterEndpoints(Action<IEndpointRouteBuilder> configuration)
    {
        EndpointConfigurations.Add(configuration);
        return this;
    }

    /// <summary>
    /// Lets the server publish to topics the app's components subscribe to (<c>IServerEvents</c> in a
    /// component, <see cref="IServerEventPublisher"/> on the server), over one stream per page.
    /// <paramref name="configure"/> says who may subscribe to which topics (a topic no template
    /// matches is refused) and fills the seams an app may need: its own backplane, lifecycle
    /// handlers, the limits read from <c>EQuantic:ServerEvents</c>.
    /// </summary>
    public UIOptions UseServerEvents(Action<ServerEventsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        // One builder however many times this is called: a library and the app may each declare
        // their topics. A builder per call mapped the endpoints twice, which made every request to
        // them ambiguous, and the last call's templates replaced the others'.
        if (ServerEvents is null)
        {
            ServerEvents = new ServerEventsBuilder();
            RegisterServices(ServerEvents.Register);
            RegisterEndpoints(endpoints => endpoints.MapServerEvents());
        }
        configure(ServerEvents);
        return this;
    }

    /// <summary>The server events the app configured, or null when it called no
    /// <see cref="UseServerEvents"/>: the page is told, so a subscription is refused at once.</summary>
    internal ServerEventsBuilder? ServerEvents { get; private set; }

    /// <summary>
    /// Routes the app declared in <c>Program.cs</c> with <c>MapPage&lt;T&gt;</c>, rather than on the
    /// page with <c>[Page]</c>.
    /// <para>
    /// Both are read everywhere a route is read — the endpoint table, the client's route table for
    /// SPA navigation, and the SSR page index — because a route that only half-registers is worse
    /// than one that does not: the page serves, and then the first client-side link to it reloads
    /// the whole document for no visible reason.
    /// </para>
    /// </summary>
    public IReadOnlyList<(string Pattern, Type Page, string? Title)> DeclaredRoutes => _declaredRoutes;

    private readonly List<(string Pattern, Type Page, string? Title)> _declaredRoutes = new();

    /// <summary>Records a route declared in Program.cs. Called by <c>MapPage&lt;T&gt;</c>.</summary>
    internal void DeclareRoute(string pattern, Type page, string? title)
    {
        _declaredRoutes.Add((pattern, page, title));
    }

    public UIOptions ConfigureHtmlShell(Action<HtmlShellOptions> configure)
    {
        configure(HtmlShell);
        return this;
    }

    /// <summary>
    /// Turns off the <c>x-powered-by: eQuantic.UI</c> response header. It is ON by default —
    /// provenance, name only, no version — and this exists for the app whose hardening checklist
    /// flags any x-powered-by at all.
    /// </summary>
    public bool DisablePoweredByHeader { get; set; }

    /// <summary>Fluent spelling of <see cref="DisablePoweredByHeader"/>.</summary>
    public UIOptions WithoutPoweredByHeader()
    {
        DisablePoweredByHeader = true;
        return this;
    }

    /// <summary>
    /// Scan an assembly for components with [Page] and [ServerAction] attributes.
    /// </summary>
    public UIOptions ScanAssembly(Assembly assembly)
    {
        AssembliesToScan.Add(assembly);

        // Scan for Error Pages
        var pageTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttributes<PageAttribute>().Any() && TwinName.OwnersCross(t));

        foreach (var type in pageTypes)
        {
            var attr = type.GetCustomAttributes<PageAttribute>().First();
            RegisterErrorPage(type, attr.Route);
        }

        return this;
    }

    internal Type? NotFoundPageType { get; private set; }
    internal Type? ErrorPageType { get; private set; }

    internal void RegisterErrorPage(Type type, string route)
    {
        if (route == "/404") NotFoundPageType = type;
        if (route == "/500") ErrorPageType = type;
    }
}
