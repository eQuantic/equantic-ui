namespace eQuantic.UI.Primitives;

/// <summary>
/// Offering a person a file the page MADE, for them to keep: recovery codes shown once, a report
/// exported from a table, a drawing. The save half of <see cref="IFilePicker"/>, and the same page
/// wherever it runs.
/// <para>
/// The page gives a name and the content, and each target does what saving means there: the browser
/// downloads it, a desktop asks where with its save panel and writes it there. The media type is
/// derived from the name when the page states none (<c>codes.txt</c> is <c>text/plain</c>), so a
/// name is all a page has to know.
/// </para>
/// <para>
/// Offer from a person's own gesture, a press handler. A browser downloads a file in answer to one,
/// and a server render has no person to offer it to: there the saver says so, by throwing with the
/// reason, rather than doing nothing.
/// </para>
/// <para>
/// <see cref="IFileDialogs"/> is not this. It answers with a path a desktop app then writes itself,
/// which a browser never hands a page. This is the portable half: a file the page holds, handed over.
/// </para>
/// </summary>
public interface IFileSaver
{
    /// <summary>Whether this target can offer a person a file at all.</summary>
    bool IsAvailable { get; }

    /// <summary>Offers <paramref name="text"/> as a file, written as UTF-8.</summary>
    /// <param name="name">The file's name, without a folder: <c>recovery-codes.txt</c>.</param>
    /// <param name="text">What the file holds.</param>
    /// <param name="mimeType">What the content is. Null derives it from the name.</param>
    /// <param name="cancellationToken">Gives up before the file is offered, throwing
    /// <see cref="OperationCanceledException"/> as .NET does. Once the person has chosen a place,
    /// the file is written whole.</param>
    ValueTask<FileSaveOutcome> SaveTextAsync(string name, string text, string? mimeType = null,
        CancellationToken cancellationToken = default);

    /// <summary>Offers <paramref name="content"/> as a file.</summary>
    /// <param name="name">The file's name, without a folder: <c>report.pdf</c>.</param>
    /// <param name="content">What the file holds.</param>
    /// <param name="mimeType">What the content is. Null derives it from the name.</param>
    /// <param name="cancellationToken">Gives up before the file is offered, throwing
    /// <see cref="OperationCanceledException"/> as .NET does. Once the person has chosen a place,
    /// the file is written whole.</param>
    ValueTask<FileSaveOutcome> SaveBytesAsync(string name, byte[] content, string? mimeType = null,
        CancellationToken cancellationToken = default);
}
