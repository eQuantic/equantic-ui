namespace eQuantic.UI.Primitives;

/// <summary>
/// A file a person chose: from a picker (<see cref="IFilePicker"/>) or by dropping it onto a
/// <see cref="DropTarget"/>. It says what every target can honestly give, a name, what the content is
/// and how big it is, and a way to read it.
/// <para>
/// Never a PATH. A browser hands a page the content and never where it lives, and a phone's document
/// picker answers with a scoped address that is not a path either, so a path would be a promise only
/// the desktop could keep. <see cref="IFileDialogs"/> stays the desktop's door for when the path IS the
/// point: opening a document to edit it in place.
/// </para>
/// <para>
/// The content stays where the target holds it until someone asks for it. Choosing a 250 MB model
/// reads nothing, and sending it reads it a part at a time. <see cref="ReadAllBytesAsync"/> is for a
/// file small enough to hold whole: a spreadsheet to import, a picture to preview.
/// </para>
/// <para>
/// Each target realizes it over what its picker answered: the browser over the file it was handed, a
/// native shell over its own. The realization constructs it, and a page never does.
/// </para>
/// </summary>
public abstract class PickedFile
{
    /// <summary>For a target's realization: what its picker reported about the file.</summary>
    /// <param name="name">The file's name, without a folder.</param>
    /// <param name="mimeType">What the content is, or empty when the target did not say.</param>
    /// <param name="size">How many bytes it holds.</param>
    protected PickedFile(string name, string mimeType, long size)
    {
        Name = name;
        MimeType = string.IsNullOrEmpty(mimeType) ? UnknownMimeType : mimeType;
        Size = size;
    }

    /// <summary>What a file whose content the target could not name is: bytes, nothing more.</summary>
    public const string UnknownMimeType = "application/octet-stream";

    /// <summary>The file's name as the person saw it, without a folder: <c>bracket.stl</c>.</summary>
    public string Name { get; }

    /// <summary>
    /// What the content is, <c>image/png</c> or <c>application/pdf</c>, as the target reported it, and
    /// <see cref="UnknownMimeType"/> when it reported nothing. A browser derives it from the name, so
    /// it says what the file claims to be, never what its bytes are.
    /// </summary>
    public string MimeType { get; }

    /// <summary>How many bytes the file holds. Known before any of them is read.</summary>
    public long Size { get; }

    /// <summary>The whole content, read now.</summary>
    public abstract ValueTask<byte[]> ReadAllBytesAsync(CancellationToken cancellationToken = default);

    /// <summary>The whole content as text, decoded as UTF-8.</summary>
    public abstract ValueTask<string> ReadAllTextAsync(CancellationToken cancellationToken = default);
}
