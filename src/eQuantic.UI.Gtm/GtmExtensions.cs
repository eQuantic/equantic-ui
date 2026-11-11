using System.Text.Json;
using System.Text.RegularExpressions;
using eQuantic.UI.Server;

namespace eQuantic.UI.Gtm;

/// <summary>
/// Google Tag Manager for eQuantic.UI, installed with one call:
/// <code>
/// builder.Services.AddUI(options => options
///     .ScanAssembly(typeof(Program).Assembly)
///     .UseGtm("GTM-XXXXXXX"));
/// </code>
/// <para>
/// ONE container per app is the design, and it is GTM's too: the container loads into the
/// DOCUMENT and never unloads, so in a SPA a "per-page container" cannot exist — and per-page
/// VARIATION is what the container's own triggers are for (`page_view` here carries `page_path`
/// precisely so tags can fire per route without the app changing). What does exist is the
/// agency-plus-client case: call <c>UseGtm</c> once per container — both ride the SAME dataLayer,
/// which is also GTM's own multi-container rule.
/// </para>
/// <para>
/// The package is SERVER-ONLY on purpose. It puts three things into the HTML shell — the official
/// container snippet, the <c>__EQ_ANALYTICS__</c> declaration that arms the runtime's
/// <c>IAnalytics</c> realization, and (by default) a listener that turns the client router's
/// <c>eq:navigate</c> into <c>page_view</c> pushes, the navigations no tag manager can see on its
/// own. A page never references this package: it asks for <c>IAnalytics</c> from Primitives and
/// never learns who is listening.
/// </para>
/// <para>
/// Deliberately ABSENT: the <c>&lt;noscript&gt;</c> iframe from Google's install instructions. It
/// exists to measure users whose browsers run no JavaScript — and for THIS framework such a user
/// gets no app at all: there is no interaction to measure and no consent dialog to show them.
/// Carrying the iframe would be cargo cult; leaving it out is stated here so nobody re-adds it as
/// a "fix".
/// </para>
/// </summary>
public static class GtmExtensions
{
    /// <summary>GTM-XXXXXXX — GTM's own container-id shape. Validated at startup because a typo'd
    /// id installs a container that silently collects nothing, discovered weeks later.</summary>
    private static readonly Regex ContainerId = new("^GTM-[A-Z0-9]{4,10}$", RegexOptions.Compiled);

    /// <summary>The dataLayer each shell already declared — how a second UseGtm (the
    /// agency-plus-client case) knows to add only its container, on the SAME dataLayer.</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<UIOptions, string>
        Installed = new();

    /// <summary>Installs a Google Tag Manager container into every page this app serves. Called
    /// twice (two containers), the second call adds only its container snippet — the installer
    /// declaration and the page-view listener exist once, and both containers must ride the same
    /// dataLayer, which is GTM's own multi-container rule.</summary>
    public static UIOptions UseGtm(this UIOptions options, string containerId,
        Action<GtmOptions>? configure = null)
    {
        if (!ContainerId.IsMatch(containerId))
            throw new ArgumentException(
                $"'{containerId}' is not a GTM container id (GTM-XXXXXXX). A wrong id installs a "
                + "container that silently collects nothing — refusing now beats discovering that "
                + "in next month's report.", nameof(containerId));

        var gtm = new GtmOptions();
        configure?.Invoke(gtm);
        if (string.IsNullOrWhiteSpace(gtm.DataLayerName))
            throw new ArgumentException("The dataLayer name cannot be empty.", nameof(configure));

        var first = !Installed.TryGetValue(options, out var declaredLayer);
        if (!first && declaredLayer != gtm.DataLayerName)
            throw new ArgumentException(
                $"A container is already installed on dataLayer '{declaredLayer}'; a second one "
                + $"cannot ride '{gtm.DataLayerName}' — IAnalytics pushes into ONE layer, and GTM's "
                + "own multi-container guidance is the same. Give both containers the same name.",
                nameof(configure));
        if (first) Installed.Add(options, gtm.DataLayerName);

        // What the container connects to, for the Content-Security-Policy an app may turn on (#1267):
        // the hosts Google's own guidance for GTM and GA4 names. Declared whether or not the policy is
        // on, so turning it on later needs nothing more from the app. The container's script and the
        // tags it loads are admitted by the nonce and 'strict-dynamic', with no host for them.
        options.ContentSecurity.AllowConnectionsTo(
            "https://*.googletagmanager.com", "https://*.google-analytics.com", "https://*.analytics.google.com");

        options.ConfigureHtmlShell(shell =>
        {
            // The runtime's IAnalytics realization stays a no-op until an installer declares
            // itself — this line is that declaration, and it must run BEFORE the app bundle.
            if (first)
                shell.AddHeadTag(HtmlTag.InlineScript(
                    $"window.__EQ_ANALYTICS__={{dataLayer:{Js(gtm.DataLayerName)}}};", defer: false));

            // The official container snippet, verbatim but for the parameters GTM itself
            // templates: the dataLayer name, the container id, and the optional environment pair.
            var environment = gtm.EnvironmentAuth is { Length: > 0 } auth
                && gtm.EnvironmentPreview is { Length: > 0 } preview
                ? $"+{Js($"&gtm_auth={Uri.EscapeDataString(auth)}&gtm_preview={Uri.EscapeDataString(preview)}&gtm_cookies_win=x")}"
                : "";
            var snippet =
                "(function(w,d,s,l,i){w[l]=w[l]||[];w[l].push({'gtm.start':new Date().getTime(),"
                + "event:'gtm.js'});var f=d.getElementsByTagName(s)[0],j=d.createElement(s),"
                + "dl=l!='dataLayer'?'&l='+l:'';j.async=true;j.src='https://www.googletagmanager.com/gtm.js?id='+i+dl"
                + environment
                // The nonce-aware form of Google's snippet: the container script carries the page's
                // nonce, which GTM hands the custom HTML tags it writes, so a strict policy admits
                // them (#1267).
                + ";var n=d.querySelector('[nonce]');n&&j.setAttribute('nonce',n.nonce||n.getAttribute('nonce'))"
                + ";f.parentNode.insertBefore(j,f);})"
                + $"(window,document,'script',{Js(gtm.DataLayerName)},{Js(containerId)});";

            if (!gtm.RequireConsent)
            {
                shell.AddHeadTag(HtmlTag.InlineScript(snippet, defer: false));
            }
            else
            {
                // Consent-gated (GDPR / LGPD). Google Consent Mode defaults to DENIED through the
                // gtag stub, which pushes into the same dataLayer the container will read when it
                // arrives. The container itself is fetched only on a granted answer — read from
                // the consent cookie the IConsent realizations share, or delivered live by the
                // eq:consent event the browser dispatches when the visitor answers. Denied or
                // unanswered visitors never download the tag manager; the `loaded` flag keeps a
                // re-grant from injecting it twice. Every answer updates the signals, so a denial
                // after a grant turns them back to denied on the page (#1294), and a grant turns on
                // only the signals the app named. Over HTTPS the answer is read from the cookie only
                // the page's own host can write (ConsentCookie.SecureName).
                shell.AddHeadTag(HtmlTag.InlineScript(
                    "(function(){"
                    + $"var l={Js(gtm.DataLayerName)};window[l]=window[l]||[];"
                    + "function gtag(){window[l].push(arguments);}"
                    + $"gtag('consent','default',{Signals(GtmConsentSignals.None)});"
                    + "var loaded=false;"
                    + "function answer(state){"
                    + $"if(state==='granted'){{gtag('consent','update',{Signals(gtm.ConsentGrants, update: true)});"
                    + "if(!loaded){loaded=true;" + snippet + "}}"
                    + $"else if(state==='denied')gtag('consent','update',{Signals(GtmConsentSignals.None, update: true)});}}"
                    + $"var name=location.protocol==='https:'?{Js(ConsentCookie.SecureName)}:{Js(ConsentCookie.Name)};"
                    + "var m=document.cookie.split('; ').filter(function(c){return c.indexOf(name+'=')===0;})[0];"
                    + "if(m&&m.slice(name.length+1)==='granted')answer('granted');"
                    + "document.addEventListener('eq:consent',function(e){if(e.detail)answer(e.detail.state);});"
                    + "})();", defer: false));
            }

            // SPA page views: the router announces every committed navigation on the document
            // (eq:navigate), and the container cannot see those on its own. page_path/page_title
            // are the names GA4's own history events use, so existing tags pick them up unchanged.
            if (first && gtm.TrackSpaPageViews)
            {
                shell.AddHeadTag(HtmlTag.InlineScript(
                    "document.addEventListener('eq:navigate',function(e){"
                    + $"window[{Js(gtm.DataLayerName)}].push({{event:'page_view',"
                    + "page_path:e.detail.path+e.detail.search,page_title:e.detail.title});});", defer: false));
            }
        });

        return options;
    }

    /// <summary>A value as a JavaScript string literal, written by the JSON writer: a quote, a line
    /// break, U+2028 or an end tag in a configured name stays inside the string (#1294).</summary>
    private static string Js(string value) => $"\"{JsonEncodedText.Encode(value)}\"";

    /// <summary>The Consent Mode object: each signal <c>granted</c> when <paramref name="granted"/> holds
    /// it and <c>denied</c> otherwise, with the default's wait for an update when it is the default.</summary>
    private static string Signals(GtmConsentSignals granted, bool update = false)
    {
        string Of(GtmConsentSignals signal) => granted.HasFlag(signal) ? "'granted'" : "'denied'";
        return $"{{ad_storage:{Of(GtmConsentSignals.AdStorage)},ad_user_data:{Of(GtmConsentSignals.AdUserData)},"
            + $"ad_personalization:{Of(GtmConsentSignals.AdPersonalization)},analytics_storage:{Of(GtmConsentSignals.AnalyticsStorage)}"
            + (update ? "}" : ",wait_for_update:500}");
    }
}
