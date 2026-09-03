namespace eQuantic.UI.Primitives;

/// <summary>
/// One entry in a picker's format list — the label a person reads and the extensions it stands for.
/// <para>
/// Extensions WITHOUT the dot ("png", not ".png"): the platforms disagree about the dot and every
/// one of them is happy to accept a filter that matches nothing, so the framework normalizes rather
/// than letting a leading dot silently empty a dialog.
/// </para>
/// </summary>
/// <param name="Label">What the person reads: "Images", "Spreadsheets".</param>
/// <param name="Extensions">The extensions it covers.</param>
public sealed record FileFilter(string Label, params string[] Extensions);

/// <summary>
/// Asking a PERSON for a file, a set of files, a folder, or a place to save — the one way a
/// sandboxed app is allowed to touch anything it was not given, and the reason
/// <c>PhotonEntitlements.UserSelectedFiles</c> exists.
/// <para>
/// Every method answers with a PATH, and null when the person cancelled. Cancelling is an ordinary
/// answer rather than an exception: a person closing a dialog has not failed at anything, and an
/// app that has to catch to find out is an app that will forget to.
/// </para>
/// <para>
/// The dialogs are MODAL, which is the platform's decision and not this contract's — a Mac panel
/// runs its own loop until it is answered. The task therefore completes when the person does. Call
/// from anywhere: the realization marshals to the UI thread itself, because a panel opened from a
/// worker thread on macOS does not open at all.
/// </para>
/// <para>
/// A path is good for THIS RUN. On a sandboxed app the permission a person granted by choosing a
/// file does not survive a relaunch — that needs a security-scoped bookmark, which is a different
/// currency and is not offered here yet. An app that wants to reopen last time's file must ask
/// again, and one that stores the path and reads it on next launch will be refused with no
/// explanation. Said here because the failure appears only after shipping, only when sandboxed.
/// </para>
/// <para>
/// DESKTOP for now, deliberately. A phone's document picker returns a security-scoped URL rather
/// than a path, and a capability that answered "a path" on one platform and "a thing that looks
/// like a path and is not" on another would be worse than one that says where it applies.
/// </para>
/// </summary>
public interface IFileDialogs
{
    /// <summary>One file, or null if the person cancelled.</summary>
    /// <param name="title">The sentence at the top of the panel — say what the file is FOR.</param>
    /// <param name="filters">What may be chosen. Empty means anything.</param>
    /// <param name="startIn">The folder to open at. Ignored when it does not exist.</param>
    Task<string?> PickFile(string? title = null, IReadOnlyList<FileFilter>? filters = null,
        string? startIn = null);

    /// <summary>Several files. EMPTY when the person cancelled — never null, because a caller that
    /// is about to loop should not have to ask twice.</summary>
    Task<IReadOnlyList<string>> PickFiles(string? title = null,
        IReadOnlyList<FileFilter>? filters = null, string? startIn = null);

    /// <summary>One folder, or null if the person cancelled.</summary>
    Task<string?> PickFolder(string? title = null, string? startIn = null);

    /// <summary>
    /// Where to save, or null if the person cancelled. The path may name a file that does not exist
    /// yet, which is the point; the platform has already asked about overwriting by the time this
    /// answers.
    /// </summary>
    Task<string?> PickSavePath(string? suggestedName = null, string? title = null,
        IReadOnlyList<FileFilter>? filters = null, string? startIn = null);
}
