using System.Globalization;
using eQuantic.UI.Web;
using eQuantic.UI.Primitives;
using FluentAssertions;
using static eQuantic.UI.Components.UI;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The consent card asks exactly while the answer is unknown, and offers the two answers plus the
/// policy — rendered through the real WebRealizer with a fake IConsent in the capability scope, the
/// same door the server (SsrConsent) and the browser (WebConsent) hand a real one through.
/// </summary>
public class CookieConsentTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private sealed class FakeConsent(ConsentState state) : IConsent
    {
        public ConsentState State { get; private set; } = state;
        public void Grant() => State = ConsentState.Granted;
        public void Deny() => State = ConsentState.Denied;
    }

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    // Culture-EXPLICIT: the SDK ships pt-BR and es resources too, and these assertions read the
    // English copy, so the render is pinned to `en` rather than to whatever culture the machine
    // or a neighbouring test left behind.
    private static List<HtmlNode> Rendered(IConsent? consent, VisualNode node)
    {
        var outer = CapabilityScope.Current;
        var culture = CultureInfo.CurrentUICulture;
        CapabilityScope.Current = type => type == typeof(IConsent) ? consent : null;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        try
        {
            return Walk(WebRealizer.Lower(node, Theme).Render()).ToList();
        }
        finally
        {
            CapabilityScope.Current = outer;
            CultureInfo.CurrentUICulture = culture;
        }
    }

    private static string AllText(IEnumerable<HtmlNode> nodes) =>
        string.Join(" ", nodes.Select(n => n.TextContent).Where(t => !string.IsNullOrEmpty(t)));

    [Fact]
    public void WhileUnanswered_ItAsks_WithBothAnswersAndThePolicy()
    {
        var nodes = Rendered(new FakeConsent(ConsentState.Unknown), CookieConsent("/privacy"));
        var text = AllText(nodes);
        text.Should().Contain("We use cookies");
        text.Should().Contain("Accept");
        text.Should().Contain("Decline");
        nodes.Any(n => n.Attributes.TryGetValue("href", out var href) && href == "/privacy").Should().BeTrue(
            "a consent card without its policy behind it is a promise the site cannot keep");
    }

    [Theory]
    [InlineData(ConsentState.Granted)]
    [InlineData(ConsentState.Denied)]
    public void OnceAnswered_ItDrawsNothing(ConsentState answered)
    {
        var nodes = Rendered(new FakeConsent(answered), CookieConsent("/privacy"));
        AllText(nodes).Should().BeEmpty("an answered question is not asked again — on this visit or the next");
    }

    [Fact]
    public void WhereNoConsentCapabilityExists_ItDrawsNothing()
    {
        AllText(Rendered(null, CookieConsent("/privacy"))).Should().BeEmpty(
            "a host with no tag manager to gate has nothing to ask consent for");
    }

    [Fact]
    public void WithoutAPolicyHref_TheLinkIsAbsent()
    {
        var nodes = Rendered(new FakeConsent(ConsentState.Unknown), CookieConsent());
        nodes.Should().NotContain(n => n.Attributes.ContainsKey("href"));
    }
}
