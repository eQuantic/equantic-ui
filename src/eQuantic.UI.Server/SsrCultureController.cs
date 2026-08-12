using System.Globalization;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Server;

/// <summary>
/// The language hand DURING SERVER RENDERING — the <see cref="SsrThemeController"/> shape, slot
/// for slot.
/// <para>
/// It REPORTS and does not decide: the request's culture was negotiated by ASP.NET's own
/// <c>RequestLocalizationMiddleware</c> (D5 — the SDK ships no negotiation, no cookie format of
/// its own), so this reads the statics that middleware set. Read per call rather than captured,
/// because a singleton that captured a culture would serve one visitor's language to the next
/// visitor's first paint.
/// </para>
/// <para>
/// <see cref="Apply"/> stays inert, for the same reason the theme's does: there is nothing to
/// re-render on the server (the response is already being written), the browser's own controller
/// takes the switch over at hydration, and writing the process statics HERE would leak one
/// request's language into every other request served by the same process.
/// </para>
/// </summary>
public sealed class SsrCultureController : ICultureController
{
    public string UICulture => CultureInfo.CurrentUICulture.Name;

    public string FormatCulture => CultureInfo.CurrentCulture.Name;

    /// <summary>Intentionally does nothing — see the type's remarks.</summary>
    public void Apply(string uiCulture, string? formatCulture = null)
    {
    }
}
