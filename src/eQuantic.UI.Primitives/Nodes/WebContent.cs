namespace eQuantic.UI.Primitives;

/// <summary>
/// WHAT an embedded document is — an address to load, or a document handed over whole. Exactly
/// one of the two, which is the entire reason this type exists.
///
/// <para>
/// <see cref="WebFrame"/> USED TO CARRY BOTH as nullable strings, and which one won was a rule
/// written down in the web realizer's doc — "inline <c>Document</c> wins over <c>Source</c>" —
/// implemented as an <c>if/else if</c> and pinned by a test that set both on purpose. Nothing
/// stopped a caller filling both; nothing told them which they would get; and the moment that
/// frame gained a factory the precedence would have become two optional parameters in the SDK's
/// own public surface, where the reader has neither the realizer's doc nor its test.
/// </para>
///
/// <para>
/// So the exclusion is a TYPE rather than a convention. <see cref="Url"/> and
/// <see cref="Document"/> are the only ways to make one, both take their string, and neither can
/// produce a value that is both — so there is no precedence left to state, to document, or to get
/// wrong. The realizer reads <see cref="IsInline"/> and writes the one attribute it names, in a
/// single total line instead of a two-armed guess.
/// </para>
/// </summary>
public readonly record struct WebContent
{
    private readonly string? _value;

    private WebContent(string value, bool isInline)
    {
        _value = value;
        IsInline = isInline;
    }

    /// <summary>
    /// The address, or the markup — <see cref="IsInline"/> says which.
    /// <para>
    /// Read through a field so that <c>default(WebContent)</c> — the one value a private
    /// constructor cannot stop anybody making — answers the empty string rather than a null
    /// behind a non-nullable <c>string</c>. A frame built on it draws neither attribute, which is
    /// exactly what the two nullable strings did when a caller set neither.
    /// </para>
    /// </summary>
    public string Value => _value ?? "";

    /// <summary>True when <see cref="Value"/> is the document itself rather than where to find
    /// it. A realizer that embeds by reference and one that embeds by value need exactly this
    /// one bit; nothing else about the two forms differs.</summary>
    public bool IsInline { get; }

    /// <summary>Embed by ADDRESS: someone else's page, a map, a video.</summary>
    public static WebContent Url(string address) => new(address, isInline: false);

    /// <summary>Embed by VALUE: markup that exists only in memory and never at an address — the
    /// playground form, where the document is what the page just built.</summary>
    public static WebContent Document(string markup) => new(markup, isInline: true);
}
