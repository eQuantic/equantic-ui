using System.Text;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Hosting;

/// <summary>
/// <see cref="IFileSaver"/> on a desktop: the save panel the shell's <see cref="IFileDialogs"/> already
/// opens, on the file's name and filtered to its extension, then the bytes written where the person
/// chose. One realization for every shell that has the panel, registered by
/// <see cref="PhotonApplication"/> wherever a shell offers <see cref="IFileDialogs"/>, so a shell never
/// writes its own.
/// </summary>
internal sealed class DialogFileSaver(IFileDialogs dialogs) : IFileSaver
{
    public bool IsAvailable => true;

    public ValueTask<FileSaveOutcome> SaveTextAsync(string name, string text, string? mimeType = null,
        CancellationToken cancellationToken = default) =>
        SaveBytesAsync(name, Encoding.UTF8.GetBytes(text), mimeType, cancellationToken);

    public async ValueTask<FileSaveOutcome> SaveBytesAsync(string name, byte[] content,
        string? mimeType = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = await dialogs.PickSavePath(suggestedName: name, filters: FiltersFor(name));
        if (path is null) return FileSaveOutcome.Cancelled;
        await File.WriteAllBytesAsync(path, content, cancellationToken);
        return FileSaveOutcome.Saved;
    }

    /// <summary>The panel offers the kind the name says, labelled as a person reads an extension
    /// (<c>TXT</c>); a name with no extension offers anything.</summary>
    internal static IReadOnlyList<FileFilter>? FiltersFor(string name)
    {
        var extension = Path.GetExtension(name).TrimStart('.');
        return extension.Length == 0 ? null : [new FileFilter(extension.ToUpperInvariant(), extension)];
    }
}
