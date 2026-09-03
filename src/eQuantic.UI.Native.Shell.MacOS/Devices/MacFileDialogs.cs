using eQuantic.UI.Primitives;
using static eQuantic.UI.Native.Shell.Apple.ObjC;

namespace eQuantic.UI.Native.Shell.MacOS;

/// <summary>
/// NSOpenPanel and NSSavePanel, which are the Mac's answer to every one of these questions and the
/// only way a sandboxed app is handed something it was not given.
/// <para>
/// A panel is MODAL and main-thread-only: <c>runModal</c> spins its own run loop until the person
/// answers, and one opened from a worker thread does not open at all — it simply never appears,
/// with no error anywhere. So every call marshals through <see cref="IUiDispatcher"/> first, which
/// is exactly the seam that exists for this.
/// </para>
/// </summary>
public sealed class MacFileDialogs : IFileDialogs
{
    private const long ModalResponseOk = 1;

    /// <inheritdoc />
    public Task<string?> PickFile(string? title, IReadOnlyList<FileFilter>? filters, string? startIn) =>
        OnUiThread(() => Open(title, filters, startIn, folders: false, multiple: false) is [var one, ..] ? one : null);

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> PickFiles(string? title, IReadOnlyList<FileFilter>? filters,
        string? startIn) =>
        OnUiThread<IReadOnlyList<string>>(() => Open(title, filters, startIn, folders: false, multiple: true));

    /// <inheritdoc />
    public Task<string?> PickFolder(string? title, string? startIn) =>
        OnUiThread(() => Open(title, null, startIn, folders: true, multiple: false) is [var one, ..] ? one : null);

    /// <inheritdoc />
    public Task<string?> PickSavePath(string? suggestedName, string? title,
        IReadOnlyList<FileFilter>? filters, string? startIn) =>
        OnUiThread(() => Save(suggestedName, title, filters, startIn));

    private static List<string> Open(string? title, IReadOnlyList<FileFilter>? filters, string? startIn,
        bool folders, bool multiple)
    {
        var panel = Send(objc_getClass("NSOpenPanel"), Sel("openPanel"));
        if (panel == IntPtr.Zero) return [];

        SendVoid(panel, Sel("setCanChooseFiles:"), !folders);
        SendVoid(panel, Sel("setCanChooseDirectories:"), folders);
        SendVoid(panel, Sel("setAllowsMultipleSelection:"), multiple);
        // A folder picker must NOT resolve an alias to its target: "choose the folder" means the
        // one that was clicked.
        SendVoid(panel, Sel("setResolvesAliases:"), !folders);
        Configure(panel, title, filters, startIn);

        if (SendLong(panel, Sel("runModal")) != ModalResponseOk) return [];

        var urls = Send(panel, Sel("URLs"));
        var count = (long)SendULong(urls, Sel("count"));
        var chosen = new List<string>((int)count);
        for (long index = 0; index < count; index++)
        {
            if (FromNSString(Send(Send(urls, Sel("objectAtIndex:"), index), Sel("path"))) is { Length: > 0 } path)
                chosen.Add(path);
        }

        return chosen;
    }

    private static string? Save(string? suggestedName, string? title, IReadOnlyList<FileFilter>? filters,
        string? startIn)
    {
        var panel = Send(objc_getClass("NSSavePanel"), Sel("savePanel"));
        if (panel == IntPtr.Zero) return null;

        if (!string.IsNullOrWhiteSpace(suggestedName))
            SendVoid(panel, Sel("setNameFieldStringValue:"), NSString(suggestedName));
        Configure(panel, title, filters, startIn);

        return SendLong(panel, Sel("runModal")) != ModalResponseOk
            ? null
            : FromNSString(Send(Send(panel, Sel("URL")), Sel("path")));
    }

    private static void Configure(IntPtr panel, string? title, IReadOnlyList<FileFilter>? filters,
        string? startIn)
    {
        // `message` and not `title`: a Mac panel's title bar is the app's, and the sentence a
        // person actually reads is the message above the file list. Setting `title` instead puts
        // the text where nobody looks.
        if (!string.IsNullOrWhiteSpace(title)) SendVoid(panel, Sel("setMessage:"), NSString(title));

        if (!string.IsNullOrWhiteSpace(startIn) && Directory.Exists(startIn))
        {
            var url = Send(objc_getClass("NSURL"), Sel("fileURLWithPath:"), NSString(startIn));
            if (url != IntPtr.Zero) SendVoid(panel, Sel("setDirectoryURL:"), url);
        }

        if (ContentTypes(filters) is { } types) SendVoid(panel, Sel("setAllowedContentTypes:"), types);
    }

    /// <summary>
    /// The extensions as UTTypes, or null when there is nothing to restrict. UTType and not the old
    /// <c>allowedFileTypes</c>: that one has been deprecated since macOS 12 and takes the same
    /// strings, so the only thing the deprecated call would buy is a warning later.
    /// <para>
    /// An extension the system does not know produces a NULL type, and a null inside the array is a
    /// crash rather than a lenient dialog — so unknown ones are dropped, and a filter list that
    /// drops to nothing restricts nothing rather than restricting everything away.
    /// </para>
    /// </summary>
    private static IntPtr? ContentTypes(IReadOnlyList<FileFilter>? filters)
    {
        if (filters is null || filters.Count == 0) return null;

        var utType = objc_getClass("UTType");
        if (utType == IntPtr.Zero) return null;

        var types = Send(objc_getClass("NSMutableArray"), Sel("array"));
        foreach (var extension in filters
                     .SelectMany(filter => filter.Extensions)
                     .Select(extension => extension.TrimStart('.').Trim())
                     .Where(extension => extension.Length > 0)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var type = Send(utType, Sel("typeWithFilenameExtension:"), NSString(extension));
            if (type != IntPtr.Zero) SendVoid(types, Sel("addObject:"), type);
        }

        return (long)SendULong(types, Sel("count")) > 0 ? types : null;
    }

    /// <summary>
    /// Runs the panel where AppKit allows one. Already on the UI thread, it runs INLINE — a modal
    /// posted to the next frame would deadlock a caller that awaits it from inside an event handler,
    /// because the frame that would run it is the one waiting.
    /// </summary>
    private static Task<T> OnUiThread<T>(Func<T> work)
    {
        if (UiDispatcher.Current is not { IsOnUiThread: false } dispatcher)
            return Task.FromResult(work());

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.Post(() =>
        {
            try { completion.SetResult(work()); }
            catch (Exception error) { completion.SetException(error); }
        });
        return completion.Task;
    }
}
