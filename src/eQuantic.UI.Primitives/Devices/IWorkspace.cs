namespace eQuantic.UI.Primitives;

/// <summary>
/// Handing something to the SYSTEM to deal with — a folder to show the person, a link to open, a
/// file to hand to whatever owns that kind of file.
/// <para>
/// The counterpart of <see cref="IFileDialogs"/>, which asks a person for a path. This is what an
/// app does once it HAS one and the next step is not its own: a disk scanner that found the
/// offender and offers "Show in Finder", a settings screen linking to its own privacy page, a
/// report the app just wrote and the person wants to read in their spreadsheet.
/// </para>
/// <para>
/// Every method answers whether the system took it. False is an ordinary answer, not a failure to
/// catch: a path may have been deleted between the scan and the click, and a URL scheme may have no
/// app behind it on this machine. An app that has to catch to find out is an app that will forget
/// to, and "nothing happened" is the one outcome a person cannot diagnose.
/// </para>
/// <para>
/// The line between the two is WHO was wrong. False means the SYSTEM declined something that could
/// have been asked. An argument the system could never have been handed — a relative path, a
/// relative URL, null — is the caller's mistake and THROWS, because answering false to it would
/// send the developer looking at the operating system for a bug that is in their own call.
/// </para>
/// <para>
/// A URL is handed over by SCHEME, and only the schemes this app opens. Handing a URL to the system
/// is handing it to whatever claims its scheme: <c>https</c> reaches a browser, which is what an app
/// means by "open this link"; <c>file</c> launches what the path names; other schemes open sessions
/// or run whichever app registered them. So every realization consults one
/// <see cref="OpenUrlPolicy"/> first — <c>http</c>, <c>https</c>, every scheme the app declared it
/// answers to with <c>builder.Bundle.UrlScheme(…)</c>, and what <c>Program.cs</c> opened besides
/// (<c>builder.Workspace.OpensMail()</c>, <c>.Opens("slack")</c>). A refused URL answers false, as the
/// system does for a scheme nothing claims, and the realization logs the scheme and the one line
/// that would open it. The policy judges the scheme and nothing else: what an <c>https</c> link
/// points AT is still the app's to decide before it gets here, because a URL that arrived in
/// content — a link in a rendered document, a value a server answered with — is the app's to vet.
/// </para>
/// <para>
/// DESKTOP for now. A phone has the ideas — a share sheet, a document provider — and they are not
/// these ones, and a capability that answered "revealed it in Finder" on a phone would be lying in
/// a way the compiler cannot see.
/// </para>
/// </summary>
public interface IWorkspace
{
    /// <summary>
    /// Shows the person where a file or folder IS, selected in the system's file browser — not
    /// opened. The distinction matters: a disk tool offering "Show in Finder" for a 4 GB cache
    /// folder must not open it, and a person who asked where something lives has not asked for its
    /// contents.
    /// </summary>
    /// <returns>False when the path no longer exists, which between a scan and a click is ordinary.</returns>
    bool Reveal(string path);

    /// <summary>
    /// Hands a file to whatever the system says owns that kind of file — the app's own report in
    /// the person's spreadsheet, a log in their editor. This, and <see cref="Reveal"/>, are the doors
    /// for a file: both take a path the app holds and check that it exists, which is why a
    /// <c>file:</c> URL through <see cref="OpenUrl"/> is refused rather than opted into.
    /// </summary>
    /// <returns>False when the path is gone, or nothing on this machine claims it.</returns>
    bool OpenFile(string path);

    /// <summary>
    /// Opens a URL in whatever handles its scheme — a browser for <c>https</c>, a mail client for
    /// <c>mailto</c>, another app for its own scheme — if this app's <see cref="OpenUrlPolicy"/>
    /// hands that scheme over at all. By default it hands over <c>http</c>, <c>https</c> and the
    /// app's own declared schemes; anything else is a line in <c>Program.cs</c>.
    /// </summary>
    /// <returns>False when the URL is not one the system can route, which includes the ordinary
    /// case of a scheme no installed app claims — and false, logged, when the scheme is one this app
    /// does not open.</returns>
    /// <remarks>
    /// False is NOT silent for an unclaimed scheme. macOS answers false AND shows the person a
    /// dialog of its own — "There is no application set to open the URL…", with a button to search
    /// the App Store. That is the right thing for a link a person clicked, and the wrong thing for a
    /// check: an app that wants to know whether <c>acme://</c> has a handler must ask with
    /// <see cref="CanOpen"/>, not by trying. Measured by trying, on a desk that was not expecting
    /// the dialog. A scheme the policy refuses shows no dialog: the URL never reaches the system.
    /// </remarks>
    bool OpenUrl(Uri url);

    /// <summary>
    /// Whether something on this machine claims the URL's scheme AND this app opens it — a
    /// QUESTION, with no side effect. The way to decide whether to show an "Open in Acme" button at
    /// all, rather than showing it and letting the system explain to the person that Acme is not
    /// installed. It applies the same <see cref="OpenUrlPolicy"/> as <see cref="OpenUrl"/>, so a
    /// button gated by it disappears for exactly the links <see cref="OpenUrl"/> would refuse.
    /// </summary>
    bool CanOpen(Uri url);
}
