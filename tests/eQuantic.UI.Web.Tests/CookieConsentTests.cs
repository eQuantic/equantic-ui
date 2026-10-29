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

    /// <summary>
    /// The card is kept by position across its parent's rebuilds, so what a parent hands it later
    /// reaches the card on screen only through <c>AdoptConfig</c>. It had no override until #1142, and
    /// copy or a policy named after the first build never arrived. It keeps the fresh instance whole
    /// now, which the coverage test counts as carrying everything without reading what Build reads:
    /// this is the check that Build reads the configuration it kept.
    /// </summary>
    [Fact]
    public void AKeptCard_DrawsTheCopyAndThePolicyItsParentHandsItLater()
    {
        var kept = CookieConsent();
        kept.AdoptConfig(new eQuantic.UI.Components.CookieConsent("/privacy")
        {
            Title = "Cookies, briefly",
            Body = "Only what the site needs.",
            AcceptLabel = "Fine",
            RejectLabel = "No thanks",
            PolicyLabel = "How we use them",
        });

        var nodes = Rendered(new FakeConsent(ConsentState.Unknown), kept);
        var text = AllText(nodes);
        text.Should().Contain("Cookies, briefly").And.Contain("Only what the site needs.")
            .And.Contain("Fine").And.Contain("No thanks").And.Contain("How we use them");
        nodes.Any(n => n.Attributes.TryGetValue("href", out var href) && href == "/privacy").Should().BeTrue(
            "the policy a parent names on a later build is the one the kept card links to");
    }

    [Fact]
    public void WithoutAPolicyHref_TheLinkIsAbsent()
    {
        var nodes = Rendered(new FakeConsent(ConsentState.Unknown), CookieConsent());
        nodes.Should().NotContain(n => n.Attributes.ContainsKey("href"));
    }
}
