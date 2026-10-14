namespace eQuantic.UI.Primitives;

/// <summary>
/// Handing a URL to the SYSTEM to open: a browser for <c>https</c>, the mail app for
/// <c>mailto</c>, the dialer for <c>tel</c>, a map, another app for its own scheme.
/// <para>
/// One contract for every Photon shell, a desktop and a phone alike, because "open this link" is
/// the same sentence on all of them: a settings screen linking to its own privacy page, a "Contact
/// support" button, a shop's address opened in the person's map. It was two methods of
/// <see cref="IWorkspace"/> while only a desktop answered, and a contract that also reveals a file
/// in a file browser is one a phone could never register. The browser registers none: a link there
/// is an anchor, which the browser already routes.
/// </para>
/// <para>
/// A URL is handed over by SCHEME, and only the schemes this app opens. Handing a URL to the system
/// is handing it to whatever claims its scheme: <c>https</c> reaches a browser, which is what an app
/// means by "open this link"; other schemes open sessions or run whichever app registered them. So
/// every realization consults one <see cref="OpenUrlPolicy"/> first: <c>http</c>, <c>https</c>,
/// every scheme the app declared it answers to with <c>builder.Bundle.UrlScheme(…)</c>, and what
/// <c>Program.cs</c> opened besides (<c>builder.Workspace.OpensMail()</c>, <c>.Opens("slack")</c>).
/// The policy judges the scheme and nothing else: what an <c>https</c> link points AT is still the
/// app's to decide before it gets here, because a URL that arrived in content (a link in a rendered
/// document, a value a server answered with) is the app's to vet.
/// </para>
/// <para>
/// The same lines are what a phone's build writes for the system. A phone answers
/// <see cref="CanOpen"/> only for schemes the app's manifest says it may ask about, so the SDK
/// writes them there from <c>builder.Workspace.Opens…</c>, and nobody opens an <c>Info.plist</c> or
/// a manifest.
/// </para>
/// <para>
/// The line between false and an exception is WHO was wrong. False means the SYSTEM declined
/// something that could have been asked, or this app does not open that scheme. An argument the
/// system could never have been handed (a relative URL, null) is the caller's mistake and THROWS,
/// because answering false to it would send the developer looking at the operating system for a
/// bug that is in their own call.
/// </para>
/// </summary>
public interface IUrlLauncher
{
    /// <summary>
    /// Opens a URL in whatever handles its scheme, if this app's <see cref="OpenUrlPolicy"/> hands
    /// that scheme over at all. By default it hands over <c>http</c>, <c>https</c> and the app's own
    /// declared schemes; anything else is a line in <c>Program.cs</c>.
    /// </summary>
    /// <returns>Whether the system took it. False when nothing on this machine claims the scheme,
    /// which is ordinary, and false, logged, when the scheme is one this app does not open.</returns>
    /// <remarks>
    /// <para>
    /// Asynchronous because one system answers later: an iPhone is asked on its main thread and
    /// says whether it opened the URL in a callback, so an answer given at once could only be a
    /// guess. A desktop and Android answer before the call returns, and the task is already done.
    /// </para>
    /// <para>
    /// False is NOT silent for an unclaimed scheme on a Mac. macOS answers false AND shows the
    /// person a dialog of its own, "There is no application set to open the URL…". That is the
    /// right thing for a link a person clicked and the wrong thing for a check: an app that wants
    /// to know whether <c>acme://</c> has a handler asks with <see cref="CanOpen"/>, not by trying.
    /// A scheme the policy refuses shows no dialog: the URL never reaches the system.
    /// </para>
    /// </remarks>
    ValueTask<bool> OpenUrlAsync(Uri url);

    /// <summary>
    /// Whether something on this machine claims the URL's scheme AND this app opens it: a QUESTION,
    /// with no side effect. The way to decide whether to show an "Open in Acme" button at all,
    /// rather than showing it and letting the system explain to the person that Acme is not
    /// installed. It applies the same <see cref="OpenUrlPolicy"/> as <see cref="OpenUrlAsync"/>, so
    /// a button gated by it disappears for exactly the links <see cref="OpenUrlAsync"/> would refuse.
    /// </summary>
    bool CanOpen(Uri url);
}
