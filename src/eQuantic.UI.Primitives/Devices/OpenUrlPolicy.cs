namespace eQuantic.UI.Primitives;

/// <summary>
/// Which URLs <see cref="IWorkspace.OpenUrl"/> hands to the system, decided by SCHEME — and the one
/// rule every realization applies, so an app that passes a URL through the capability on a Mac is
/// protected in exactly the way it will be on a phone.
/// <para>
/// Handing a URL to the operating system is handing it to WHATEVER claims its scheme. <c>https</c>
/// reaches a browser, which is what an app means by "open this link"; <c>file</c> reaches Finder or
/// launches what the path names; <c>ssh</c> and <c>vnc</c> open sessions; a scheme another app
/// registered runs that app with the URL as its arguments. A URL that arrived in CONTENT — a link
/// in a feed, a value a server answered with — must never get to choose among those, and the place
/// to make sure of it is the one door every such URL passes through, not a filter each app
/// remembers to write in front of it.
/// </para>
/// <para>
/// So the default is the web, plus the schemes THIS app declared it answers to with
/// <c>builder.Bundle.UrlScheme(…)</c> — an app opening its own <c>acme://</c> link is ordinary, and
/// the SDK already knows the scheme, so nobody declares it twice. Everything else is opt-in,
/// stated in <c>Program.cs</c> beside every other fact about the app:
/// <c>builder.Workspace.OpensMail()</c> for <c>mailto</c>, <c>OpensPhone()</c> for <c>tel</c>,
/// <c>Opens("slack")</c> for another app's scheme.
/// </para>
/// <para>
/// A refused URL answers FALSE from <see cref="IWorkspace.OpenUrl"/> — the same answer the system
/// gives for a scheme nothing claims — and the realization logs which scheme and what to declare.
/// Not an exception, deliberately: the policy exists for URLs the app did not write, and a hostile
/// link has to be something a click handler can hand over without a try/catch around it. A
/// developer who typed <c>mailto:</c> themselves and forgot to declare it finds the sentence in the
/// log at the first click, by name.
/// </para>
/// <para>
/// <c>file</c> cannot be opted in. A file has typed doors — <see cref="IWorkspace.OpenFile"/> and
/// <see cref="IWorkspace.Reveal"/> — which check that the path exists and never launch a folder; a
/// <c>file:</c> URL through <c>OpenUrl</c> would be the same request with every check skipped.
/// </para>
/// <para>
/// Immutable: widening returns a new policy, so the one registered in the container is the one the
/// program configured, and nothing that runs later can loosen it.
/// </para>
/// </summary>
public sealed class OpenUrlPolicy
{
    private const string FileHasTypedDoors = "file: URLs do not go through OpenUrl. Hand the path to "
        + "IWorkspace.OpenFile or IWorkspace.Reveal, which check that it exists and never launch a "
        + "folder — the checks a file: URL would skip.";

    private readonly HashSet<string> _schemes;

    private OpenUrlPolicy(HashSet<string> schemes)
    {
        _schemes = schemes;
        Schemes = new System.Collections.ObjectModel.ReadOnlySet<string>(schemes);
    }

    /// <summary>The default: <c>http</c> and <c>https</c>, and nothing else.</summary>
    public static OpenUrlPolicy Web { get; } =
        new(new HashSet<string>(StringComparer.Ordinal) { Uri.UriSchemeHttp, Uri.UriSchemeHttps });

    /// <summary>The schemes this policy hands over, lower-case, in no particular order.</summary>
    public IReadOnlySet<string> Schemes { get; }

    /// <summary>
    /// Whether <paramref name="url"/> may be handed to the system. A relative URL has no scheme and
    /// is nobody's to route, so it is refused here too — a realization throws for it first, because
    /// a relative URL is the caller's mistake and not a decision of this policy's.
    /// </summary>
    public bool Allows(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        return url.IsAbsoluteUri && _schemes.Contains(url.Scheme);
    }

    /// <summary>
    /// Why <paramref name="url"/> is refused, as the sentence a realization logs — or null when it is
    /// allowed. Written once, here, so every platform's log says the same thing and names the same
    /// one-line fix.
    /// </summary>
    public string? Refusal(Uri url)
    {
        if (Allows(url)) return null;
        if (!url.IsAbsoluteUri)
            return $"\"{url.OriginalString}\" is relative: nothing can route a URL without a scheme.";
        // The one scheme whose fix is NOT a declaration. Telling the developer to declare it would
        // send them to a line that throws — the answer is the typed door, said here first.
        if (url.Scheme == Uri.UriSchemeFile)
            return $"IWorkspace.OpenUrl refused a \"{Uri.UriSchemeFile}\" URL. {FileHasTypedDoors}";
        return $"IWorkspace.OpenUrl refused a \"{url.Scheme}\" URL: this app hands only {Describe()} "
            + $"URLs to the system. If it means to open {url.Scheme}: links, declare "
            + $"builder.Workspace.Opens(\"{url.Scheme}\") in Program.cs. A URL that came from content "
            + "should stay refused.";
    }

    /// <summary>
    /// This policy, also handing over <paramref name="scheme"/>. The scheme is a NAME — <c>mailto</c>,
    /// not <c>mailto:</c> — and is compared without regard to case, as RFC 3986 says schemes are.
    /// </summary>
    /// <exception cref="ArgumentException">Not a URL scheme name, or <c>file</c>.</exception>
    public OpenUrlPolicy Allowing(string scheme)
    {
        var accepted = Accept(scheme);
        if (_schemes.Contains(accepted)) return this;
        return new(new HashSet<string>(_schemes, StringComparer.Ordinal) { accepted });
    }

    /// <summary>Several at once — the app's own declared schemes arrive this way.</summary>
    public OpenUrlPolicy Allowing(IEnumerable<string> schemes)
    {
        ArgumentNullException.ThrowIfNull(schemes);
        var policy = this;
        foreach (var scheme in schemes) policy = policy.Allowing(scheme);
        return policy;
    }

    private static string Accept(string scheme)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scheme);
        var name = scheme.Trim();
        // .NET already knows what a scheme name is (RFC 3986: a letter, then letters, digits, "+",
        // "-" or "."). A trailing colon is the commonest way to get this wrong, so it is named.
        if (!Uri.CheckSchemeName(name))
            throw new ArgumentException($"\"{scheme}\" is not a URL scheme. Give the NAME alone — "
                + "\"mailto\", not \"mailto:\" or \"mailto://\".", nameof(scheme));

        var lower = name.ToLowerInvariant();
        if (lower == Uri.UriSchemeFile) throw new ArgumentException(FileHasTypedDoors, nameof(scheme));
        return lower;
    }

    private string Describe() => string.Join(", ", _schemes.Order(StringComparer.Ordinal));
}
