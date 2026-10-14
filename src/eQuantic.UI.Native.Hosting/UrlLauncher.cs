using eQuantic.UI.Primitives;
using Microsoft.Extensions.Logging;

namespace eQuantic.UI.Native.Hosting;

/// <summary>
/// The rule every shell's <see cref="IUrlLauncher"/> applies, written once, so no two shells can
/// disagree about one URL: who was wrong, what the app opens, and only then the platform.
/// <para>
/// A shell says the two things only it knows: how its system is handed a URL
/// (<see cref="Hand"/>) and how it is asked whether something claims a scheme
/// (<see cref="Claimed"/>). Everything before that is here. It was written twice while two desktops
/// answered, and the second copy had already drifted from the first once: a check that answered
/// false to a relative URL where the contract says to throw.
/// </para>
/// </summary>
public abstract class UrlLauncher(OpenUrlPolicy policy, ILogger logger) : IUrlLauncher
{
    /// <inheritdoc />
    public ValueTask<bool> OpenUrlAsync(Uri url)
    {
        Absolute(url, "open");
        // The POLICY, before the platform: a refused URL never reaches the system. Logged, because
        // false alone is the one answer nobody can diagnose, and a developer who typed mailto:
        // themselves should find the fix at the first click.
        if (policy.Refusal(url) is { } refusal)
        {
            logger.LogWarning("{Refusal}", refusal);
            return new(false);
        }
        return Hand(url);
    }

    /// <inheritdoc />
    public bool CanOpen(Uri url)
    {
        Absolute(url, "check");
        // The same policy OpenUrlAsync applies, or the two would disagree about the same URL: a
        // button gated by CanOpen has to disappear for exactly the links OpenUrlAsync would refuse.
        // Not logged: a QUESTION has no side effect, and a screen may ask it on every build.
        return policy.Allows(url) && Claimed(url);
    }

    /// <summary>
    /// Hands the system a URL this app opens, and answers whether the system took it. The URL is
    /// absolute and its scheme passed the policy. Pass <see cref="Uri.AbsoluteUri"/> on, never
    /// <see cref="Uri.ToString"/>: the second gives back what the app typed, and a space or an
    /// accent that was never percent-encoded is a URL no system parses.
    /// </summary>
    protected abstract ValueTask<bool> Hand(Uri url);

    /// <summary>
    /// Whether something on this machine claims the URL's scheme, asked in the system's QUERY form:
    /// no window, no dialog, nothing a person sees. The URL is absolute and its scheme passed the
    /// policy.
    /// </summary>
    protected abstract bool Claimed(Uri url);

    /// <summary>
    /// A relative URL is not something the system declined: no system could ever have been asked.
    /// Answering false would send the developer looking at the operating system for a mistake that
    /// is in their own call.
    /// </summary>
    private static void Absolute(Uri url, string verb)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (!url.IsAbsoluteUri)
            throw new ArgumentException($"A URL to {verb} must be absolute: nothing can route "
                + $"\"{url.OriginalString}\" without a scheme.", nameof(url));
    }
}
