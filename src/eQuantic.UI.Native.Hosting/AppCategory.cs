namespace eQuantic.UI.Native.Hosting;

/// <summary>
/// The Finder and App Store category an app declares, as C# rather than as
/// <c>public.app-category.utilities</c> — one more platform string that is a typo away from being
/// silently ignored, and that nobody should have to look up.
/// <para>
/// Apple's list is longer than this (it includes every game genre); these are the ones a Photon
/// desktop app reaches for. Anything else is one line away:
/// <c>builder.Bundle.Key("LSApplicationCategoryType", "public.app-category.puzzle-games")</c>.
/// </para>
/// </summary>
public enum AppCategory
{
    /// <summary>Not declared.</summary>
    None,

    /// <summary>Utilities — the default home of a tool that does one job well.</summary>
    Utilities,

    /// <summary>Developer tools.</summary>
    DeveloperTools,

    /// <summary>Productivity.</summary>
    Productivity,

    /// <summary>Business.</summary>
    Business,

    /// <summary>Finance.</summary>
    Finance,

    /// <summary>Graphics and design.</summary>
    GraphicsDesign,

    /// <summary>Photography.</summary>
    Photography,

    /// <summary>Music.</summary>
    Music,

    /// <summary>Video.</summary>
    Video,

    /// <summary>Education.</summary>
    Education,

    /// <summary>Social networking.</summary>
    SocialNetworking,

    /// <summary>News.</summary>
    News,

    /// <summary>Reference.</summary>
    Reference,

    /// <summary>Healthcare and fitness.</summary>
    HealthcareFitness,

    /// <summary>Games.</summary>
    Games,
}
