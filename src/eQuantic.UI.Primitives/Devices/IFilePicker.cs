namespace eQuantic.UI.Primitives;

/// <summary>
/// Asking a PERSON for files to hand to the app: a document to attach, a spreadsheet to import, a model
/// to print. The answer is a <see cref="PickedFile"/> on every target, so a page that asks is the same
/// page wherever it runs.
/// <para>
/// Cancelling is an ordinary answer: null from <see cref="PickFileAsync"/> and an empty list from
/// <see cref="PickFilesAsync"/>. A person who closed the picker has not failed at anything.
/// </para>
/// <para>
/// Ask from a person's own gesture, a press handler. A browser opens its picker only in answer to one,
/// so a page that asks from a timer or after awaiting something else is refused by the browser, and
/// the answer is then the same as a cancel: nothing was chosen.
/// </para>
/// <para>
/// <see cref="IFileDialogs"/> is not this. It answers with paths and folders, which only a desktop can
/// give, and it is how a desktop app opens a document to edit it in place. This is the portable half:
/// choosing files to read or send.
/// </para>
/// </summary>
public interface IFilePicker
{
    /// <summary>Whether this target can ask a person for a file at all.</summary>
    bool IsAvailable { get; }

    /// <summary>One file, or null when the person chose nothing.</summary>
    /// <param name="filters">What may be chosen. Null or empty means anything.</param>
    /// <param name="cancellationToken">Gives up waiting for the person.</param>
    ValueTask<PickedFile?> PickFileAsync(IReadOnlyList<FileFilter>? filters = null,
        CancellationToken cancellationToken = default);

    /// <summary>Several files. EMPTY when the person chose nothing, never null.</summary>
    /// <param name="filters">What may be chosen. Null or empty means anything.</param>
    /// <param name="cancellationToken">Gives up waiting for the person.</param>
    ValueTask<IReadOnlyList<PickedFile>> PickFilesAsync(IReadOnlyList<FileFilter>? filters = null,
        CancellationToken cancellationToken = default);
}
