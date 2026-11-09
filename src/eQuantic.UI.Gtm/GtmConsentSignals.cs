namespace eQuantic.UI.Gtm;

/// <summary>
/// The Google Consent Mode signals a visitor's grant turns on, when the container waits for consent
/// (<see cref="GtmOptions.WithConsentGrants"/>, #1294). The ones a grant leaves out stay <c>denied</c>, and a denial turns every
/// one of them back to <c>denied</c>. Google's own names, because this package transcribes Google Tag
/// Manager.
/// </summary>
[Flags]
public enum GtmConsentSignals
{
    /// <summary>None: a grant loads the container with every signal still denied.</summary>
    None = 0,

    /// <summary><c>analytics_storage</c>: cookies and storage for analytics.</summary>
    AnalyticsStorage = 1,

    /// <summary><c>ad_storage</c>: cookies and storage for advertising.</summary>
    AdStorage = 2,

    /// <summary><c>ad_user_data</c>: sending the visitor's data to Google for advertising.</summary>
    AdUserData = 4,

    /// <summary><c>ad_personalization</c>: personalized advertising.</summary>
    AdPersonalization = 8,

    /// <summary>All four: analytics and advertising.</summary>
    All = AnalyticsStorage | AdStorage | AdUserData | AdPersonalization,
}
