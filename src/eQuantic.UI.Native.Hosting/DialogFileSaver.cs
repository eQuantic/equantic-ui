using System.Text;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Hosting;

/// <summary>
/// <see cref="IFileSaver"/> on a desktop: the save panel the shell's <see cref="IFileDialogs"/> already
/// opens, on the file's name and filtered to its extension, then the bytes written where the person
/// chose. One realization for every shell that has the panel: each desktop shell registers it over its
/// own <see cref="IFileDialogs"/> (#1022), and a phone, which has no such panel, saves through its
/// export picker instead (#782).
/// </summary>
public sealed class DialogFileSaver(IFileDialogs dialogs) : IFileSaver
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
        // Not cancellable from here on: the person chose a place, and a write stopped halfway would
        // leave a truncated file there under the name they gave it.
        await File.WriteAllBytesAsync(path, content, CancellationToken.None);
        return FileSaveOutcome.Saved;
    }

    /// <summary>The panel offers the kind the name says, labelled as a person reads an extension
    /// (<c>TXT</c>); a name with no extension offers anything.</summary>
    private static IReadOnlyList<FileFilter>? FiltersFor(string name)
    {
        var extension = Path.GetExtension(name).TrimStart('.');
        return extension.Length == 0 ? null : [new FileFilter(extension.ToUpperInvariant(), extension)];
    }
}
