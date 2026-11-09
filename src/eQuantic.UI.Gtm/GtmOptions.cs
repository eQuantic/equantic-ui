namespace eQuantic.UI.Gtm;

/// <summary>
/// What <c>UseGtm</c> installs. The container id is the only thing an app MUST say; everything
/// else has the default the GTM snippet itself ships with.
/// </summary>
public sealed class GtmOptions
{
    /// <summary>The dataLayer global's name. Rename only when another script on the page already
    /// owns <c>dataLayer</c> — the GTM container must be configured with the same name.</summary>
    public string DataLayerName { get; set; } = "dataLayer";

    /// <summary>
    /// Push a <c>page_view</c> event on every CLIENT-SIDE navigation (default true). The container
    /// sees the initial load on its own; what it cannot see is the SPA router swapping pages
    /// without a reload — this listens to the router's <c>eq:navigate</c> and fills that gap.
    /// Turn it off when the container uses GA4's enhanced measurement history tracking instead,
    /// or the same navigation counts twice.
    /// </summary>
    public bool TrackSpaPageViews { get; set; } = true;

    /// <summary>
    /// GTM environment parameters (<c>gtm_auth</c>/<c>gtm_preview</c>), verbatim from the
    /// environment's own snippet — for apps whose container publishes through GTM environments.
    /// Null = the live container, which is almost everyone.
    /// </summary>
    public string? EnvironmentAuth { get; set; }
    public string? EnvironmentPreview { get; set; }

    /// <summary>Fluent spelling of <see cref="DataLayerName"/>.</summary>
    public GtmOptions WithDataLayerName(string name)
    {
        DataLayerName = name;
        return this;
    }

    /// <summary>Turns the SPA page-view pushes off — for containers using GA4's own history
    /// trigger, where the same navigation would count twice.</summary>
    public GtmOptions WithoutSpaPageViews()
    {
        TrackSpaPageViews = false;
        return this;
    }

    /// <summary>
    /// Load the container only AFTER the visitor consents (GDPR / LGPD). The head declares Google
    /// Consent Mode defaults of <c>denied</c>, and the container script is fetched only when the
    /// shared consent cookie says <c>granted</c> — or the moment the visitor grants it, announced by
    /// <c>IConsent</c> as the document event <c>eq:consent</c>. A visitor who declines or never answers
    /// downloads no tag manager at all, which is what "no cookie until consent" means in practice, and
    /// one who declines after granting turns every signal back to <c>denied</c> on the page (#1294).
    /// Off by default: an installer that already ran without asking keeps running; turning this on is
    /// the app's decision, paired with a consent component.
    /// </summary>
    public bool RequireConsent { get; set; }

    /// <summary>The Consent Mode signals a grant turns on (#1294). All four by default, since one
    /// question asks for them; an app whose consent question covers analytics alone says
    /// <see cref="GtmConsentSignals.AnalyticsStorage"/>.</summary>
    public GtmConsentSignals ConsentGrants { get; set; } = GtmConsentSignals.All;

    /// <summary>Gates the container on the visitor's consent (<see cref="RequireConsent"/>), a grant
    /// turning on <paramref name="grants"/>.</summary>
    public GtmOptions WithConsent(GtmConsentSignals grants = GtmConsentSignals.All)
    {
        RequireConsent = true;
        ConsentGrants = grants;
        return this;
    }

    /// <summary>Fluent spelling of the GTM environment pair.</summary>
    public GtmOptions WithEnvironment(string auth, string preview)
    {
        EnvironmentAuth = auth;
        EnvironmentPreview = preview;
        return this;
    }
}
