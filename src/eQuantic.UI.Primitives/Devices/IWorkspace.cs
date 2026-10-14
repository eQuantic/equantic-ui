namespace eQuantic.UI.Primitives;

/// <summary>
/// Handing a FILE to the system to deal with: a folder to show the person, a file to hand to
/// whatever owns that kind of file.
/// <para>
/// The counterpart of <see cref="IFileDialogs"/>, which asks a person for a path. This is what an
/// app does once it HAS one and the next step is not its own: a disk scanner that found the
/// offender and offers "Show in Finder", a report the app just wrote and the person wants to read
/// in their spreadsheet. A LINK is <see cref="IUrlLauncher"/>'s, which every target with somewhere
/// to hand one realizes.
/// </para>
/// <para>
/// Every method answers whether the system took it. False is an ordinary answer, not a failure to
/// catch: a path may have been deleted between the scan and the click. An app that has to catch to
/// find out is an app that will forget to, and "nothing happened" is the one outcome a person
/// cannot diagnose.
/// </para>
/// <para>
/// The line between the two is WHO was wrong. False means the SYSTEM declined something that could
/// have been asked. An argument the system could never have been handed (a relative path, null) is
/// the caller's mistake and THROWS, because answering false to it would send the developer looking
/// at the operating system for a bug that is in their own call.
/// </para>
/// <para>
/// DESKTOP. A phone has the ideas (a share sheet, a document provider) and they are not these
/// ones, and a capability that answered "revealed it in Finder" on a phone would be lying in a way
/// the compiler cannot see.
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
    /// <c>file:</c> URL through <see cref="IUrlLauncher.OpenUrlAsync"/> is refused rather than
    /// opted into.
    /// </summary>
    /// <returns>False when the path is gone, or nothing on this machine claims it.</returns>
    bool OpenFile(string path);
}
