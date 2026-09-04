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
/// It opens what the APP names, never what a document does. Everything here reaches outside the
/// app's own window, so a URL that arrived in content — a link in a rendered document, a value that
/// came back from a server — is the app's to decide about before it gets here. The capability does
/// not judge, which is exactly why the judging has to happen at the call site.
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
    /// the person's spreadsheet, a log in their editor.
    /// </summary>
    /// <returns>False when the path is gone, or nothing on this machine claims it.</returns>
    bool OpenFile(string path);

    /// <summary>
    /// Opens a URL in whatever handles its scheme: a browser for <c>https</c>, a mail client for
    /// <c>mailto</c>, another app for its own scheme.
    /// </summary>
    /// <returns>False when the URL is not one the system can route, which includes the ordinary
    /// case of a scheme no installed app claims.</returns>
    bool OpenUrl(Uri url);
}
