using eQuantic.UI.Primitives;

namespace HydrationSite.Pages;

/// <summary>
/// A link that carries an order as text, escaped in the component, as a real site's WhatsApp link was (#1805):
/// the browser escaped it with <c>encodeURIComponent</c>, which keeps <c>! ' ( ) *</c>, so the link the server
/// drew changed once the page hydrated.
/// </summary>
[Page("/share")]
public sealed class SharePage : StatelessComponent
{
    public override VisualNode Build(ComponentContext context) =>
        Link("https://wa.me/1?text=" + Uri.EscapeDataString("Hello! I'd like a cake (14 cm) * 2"), Text("Order"));
}
