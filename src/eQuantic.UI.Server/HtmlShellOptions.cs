namespace eQuantic.UI.Server;

/// <summary>
/// Options for generating the HTML shell.
/// </summary>
public class HtmlShellOptions
{
    public string Title { get; set; } = "eQuantic.UI App";
    public string HtmlClass { get; set; } = "";
    /// <summary>
    /// The head elements every page carries, as DATA. They are rendered per request, so the shell
    /// stamps each script and style with that request's nonce (#1266). There is no raw-markup
    /// overload: text the shell did not build is text it cannot stamp.
    /// </summary>
    public IReadOnlyList<HtmlTag> HeadTags => _headTags;

    private readonly List<HtmlTag> _headTags = new();

    /// <summary>The app's own CSS, in the order <see cref="AddStyles"/> added it.</summary>
    public IReadOnlyList<string> Styles => _styles;

    private readonly List<string> _styles = new();

    public HtmlShellOptions SetTitle(string title)
    {
        Title = title;
        return this;
    }

    public HtmlShellOptions SetHtmlClass(string htmlClass)
    {
        HtmlClass = htmlClass;
        return this;
    }

    /// <summary>
    /// Adds a head element as DATA — <see cref="HtmlTag"/> owns the quoting, encoding and closing.
    /// This is the authoring API: app code never types markup.
    /// </summary>
    public HtmlShellOptions AddHeadTag(HtmlTag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        _headTags.Add(tag);
        return this;
    }

    /// <summary>
    /// App-wide metadata DEFAULTS — a description, a fallback share image, a Twitter card type:
    /// whatever every page should say unless it says otherwise.
    /// <para>
    /// These seed the same collection a page's <c>IHandleMetadata</c> writes into, so a page
    /// overrides them BY KEY rather than adding a second tag. That is the whole point: these used
    /// to be raw head HTML, which shares no key with anything, so an app that set a description
    /// here and a page that set its own shipped two <c>&lt;meta name="description"&gt;</c> — a
    /// crawler being told the page cannot make up its mind — and there was no way for a page to
    /// win. The only way out was to leave the global empty, which made it useless for exactly the
    /// things a global is for.
    /// </para>
    /// </summary>
    public HtmlShellOptions ConfigureMetadata(Action<Metadata.SeoBuilder> configure)
    {
        configure(new Metadata.SeoBuilder(DefaultMetadata));
        return this;
    }

    /// <summary>
    /// The metadata every page starts from; a page's own overrides it BY KEY. Public because it is
    /// what the merge reads and what a test has to be able to assert on — the seeding order is the
    /// whole contract, and a contract nothing can observe is one nothing can hold you to.
    /// </summary>
    public Metadata.MetadataCollection DefaultMetadata { get; } = new();

    /// <summary>The default <c>&lt;meta name="description"&gt;</c> — a page's own replaces it.</summary>
    public HtmlShellOptions AddDescription(string description) =>
        ConfigureMetadata(seo => seo.Description(description));

    /// <summary>A <c>&lt;link rel="preconnect"&gt;</c> to an origin the page will fetch from.
    /// <paramref name="crossOrigin"/> matters for font/CORS fetches (fonts.gstatic.com wants it).</summary>
    public HtmlShellOptions AddPreconnect(Uri origin, bool crossOrigin = false)
    {
        var tag = HtmlTag.Link("preconnect", origin.ToString());
        if (crossOrigin) tag.Attr("crossorigin");
        return AddHeadTag(tag);
    }

    /// <summary>An external stylesheet <c>&lt;link&gt;</c>.</summary>
    public HtmlShellOptions AddStylesheet(Uri href) =>
        AddHeadTag(HtmlTag.Link("stylesheet", href.ToString()));

    /// <summary>An external <c>&lt;script&gt;</c> (deferred by default — head scripts must not
    /// block parsing).</summary>
    public HtmlShellOptions AddScript(Uri src, bool defer = true) =>
        AddHeadTag(HtmlTag.Script(src.ToString(), defer));

    /// <summary>
    /// ESCAPE HATCH: CSS of the app's own, for what the theme cannot say yet. The document's surface,
    /// text, faces, selection, placeholder and scrollbars are the theme's (<c>IAppTheme</c>), written
    /// in the theme's stylesheet; this CSS comes after it in the head, so a rule here wins every tie
    /// with the SDK's. Each call appends, and nothing an app adds replaces the SDK's rules.
    /// </summary>
    public HtmlShellOptions AddStyles(string css)
    {
        ArgumentNullException.ThrowIfNull(css);
        _styles.Add(css);
        return this;
    }
}
