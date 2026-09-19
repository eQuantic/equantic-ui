namespace eQuantic.UI.Primitives;

/// <summary>
/// What an embedded document is ALLOWED to do — the vocabulary's word for the iframe sandbox,
/// composed instead of spelled. <see cref="None"/> is the fully locked-down frame; every flag
/// hands one capability back.
/// </summary>
[Flags]
public enum WebSandbox
{
    /// <summary>Maximum isolation: no scripts, no origin, no forms, no popups.</summary>
    None = 0,

    /// <summary>The document may run script.</summary>
    Scripts = 1,

    /// <summary>The document keeps the host's origin — required for it to fetch same-origin
    /// resources (module imports, styles). Trust the content before granting it.</summary>
    SameOrigin = 2,

    /// <summary>The document may submit forms.</summary>
    Forms = 4,

    /// <summary>The document may open new windows.</summary>
    Popups = 8,
}
