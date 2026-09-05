using System.Runtime.InteropServices;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Shell.Windows;

/// <summary>
/// The Common Item Dialog — <c>IFileOpenDialog</c> and <c>IFileSaveDialog</c>, the pickers every
/// Windows app since Vista shows and the only ones that speak the modern shell (libraries, search,
/// pinned places). Through the vtables, like everything COM in this shell.
/// <para>
/// A dialog is MODAL and apartment-threaded: <c>Show</c> spins its own message loop until the
/// person answers, and one shown from a thread that never joined COM does not show. So every call
/// marshals through <see cref="IUiDispatcher"/> first — the window's thread is the one that
/// initialised COM — which is exactly the seam that exists for this.
/// </para>
/// </summary>
public sealed unsafe class WindowsFileDialogs : IFileDialogs
{
    private static readonly Guid CLSID_FileOpenDialog = new("dc1c5a9c-e88a-4dde-a5a1-60f82a20aef7");
    private static readonly Guid CLSID_FileSaveDialog = new("c0b4e2f3-ba21-4773-8dba-335ec946eb8b");
    private static readonly Guid IID_IFileOpenDialog = new("d57c7288-d4ad-4768-be02-9d969532d960");
    private static readonly Guid IID_IFileSaveDialog = new("84bccd23-5fde-4cdb-aea4-af64b83d78ab");
    private static readonly Guid IID_IShellItem = new("43826d1e-e718-42ee-bc55-a1e261c37bfe");

    private const uint FOS_OVERWRITEPROMPT = 0x2;
    private const uint FOS_NOCHANGEDIR = 0x8;
    private const uint FOS_PICKFOLDERS = 0x20;
    private const uint FOS_FORCEFILESYSTEM = 0x40;
    private const uint FOS_ALLOWMULTISELECT = 0x200;
    private const uint FOS_PATHMUSTEXIST = 0x800;
    private const uint FOS_FILEMUSTEXIST = 0x1000;
    private const uint SIGDN_FILESYSPATH = 0x80058000;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr bindContext, Guid* iid, void** item);

    /// <inheritdoc />
    public Task<string?> PickFile(string? title, IReadOnlyList<FileFilter>? filters, string? startIn) =>
        OnUiThread(() => Open(title, filters, startIn, folders: false, multiple: false) is [var one, ..] ? one : null);

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> PickFiles(string? title, IReadOnlyList<FileFilter>? filters, string? startIn) =>
        OnUiThread<IReadOnlyList<string>>(() => Open(title, filters, startIn, folders: false, multiple: true));

    /// <inheritdoc />
    public Task<string?> PickFolder(string? title, string? startIn) =>
        OnUiThread(() => Open(title, null, startIn, folders: true, multiple: false) is [var one, ..] ? one : null);

    /// <inheritdoc />
    public Task<string?> PickSavePath(string? suggestedName, string? title, IReadOnlyList<FileFilter>? filters,
        string? startIn) =>
        OnUiThread(() => Save(suggestedName, title, filters, startIn));

    private static List<string> Open(string? title, IReadOnlyList<FileFilter>? filters, string? startIn,
        bool folders, bool multiple)
    {
        Com.EnsureInitialized();
        void* dialog = null;
        void* results = null;
        try
        {
            dialog = Com.Create(CLSID_FileOpenDialog, IID_IFileOpenDialog);
            var options = FOS_FORCEFILESYSTEM | FOS_PATHMUSTEXIST | FOS_NOCHANGEDIR
                | (folders ? FOS_PICKFOLDERS : FOS_FILEMUSTEXIST)
                | (multiple ? FOS_ALLOWMULTISELECT : 0);
            Dialog.SetOptions(dialog, options);
            Configure(dialog, title, filters, startIn);

            var shown = Dialog.Show(dialog, IntPtr.Zero);
            if (shown == Com.E_CANCELLED) return [];
            Com.Check(shown, "open dialog");

            if (Dialog.GetResults(dialog, &results) < 0) return [];
            uint count;
            Com.Check(Dialog.GetCount(results, &count), "dialog results");
            var chosen = new List<string>((int)count);
            for (uint index = 0; index < count; index++)
            {
                void* item;
                if (Dialog.GetItemAt(results, index, &item) < 0) continue;
                try
                {
                    if (PathOf(item) is { Length: > 0 } path) chosen.Add(path);
                }
                finally
                {
                    Com.Release(item);
                }
            }
            return chosen;
        }
        finally
        {
            Com.Release(ref results);
            Com.Release(ref dialog);
        }
    }

    private static string? Save(string? suggestedName, string? title, IReadOnlyList<FileFilter>? filters, string? startIn)
    {
        Com.EnsureInitialized();
        void* dialog = null;
        void* item = null;
        try
        {
            dialog = Com.Create(CLSID_FileSaveDialog, IID_IFileSaveDialog);
            Dialog.SetOptions(dialog, FOS_FORCEFILESYSTEM | FOS_PATHMUSTEXIST | FOS_OVERWRITEPROMPT | FOS_NOCHANGEDIR);
            if (!string.IsNullOrWhiteSpace(suggestedName))
            {
                fixed (char* name = suggestedName) Dialog.SetFileName(dialog, name);
                // The suggested name's extension is the default one, so "report" typed over it
                // still saves as report.csv rather than a file with no type.
                var extension = Path.GetExtension(suggestedName).TrimStart('.');
                if (extension.Length > 0)
                    fixed (char* text = extension) Dialog.SetDefaultExtension(dialog, text);
            }
            Configure(dialog, title, filters, startIn);

            var shown = Dialog.Show(dialog, IntPtr.Zero);
            if (shown == Com.E_CANCELLED) return null;
            Com.Check(shown, "save dialog");
            if (Dialog.GetResult(dialog, &item) < 0) return null;
            return PathOf(item);
        }
        finally
        {
            Com.Release(ref item);
            Com.Release(ref dialog);
        }
    }

    private static void Configure(void* dialog, string? title, IReadOnlyList<FileFilter>? filters, string? startIn)
    {
        if (!string.IsNullOrWhiteSpace(title))
            fixed (char* text = title) Dialog.SetTitle(dialog, text);

        if (!string.IsNullOrWhiteSpace(startIn) && Directory.Exists(startIn))
        {
            void* folder;
            var iid = IID_IShellItem;
            if (SHCreateItemFromParsingName(startIn, IntPtr.Zero, &iid, &folder) >= 0)
            {
                Dialog.SetFolder(dialog, folder);
                Com.Release(folder);
            }
        }

        if (filters is null || filters.Count == 0) return;
        // COMDLG_FILTERSPEC: a label and a ";"-joined pattern list. The strings must outlive the
        // call only — the dialog copies them.
        var labels = new string[filters.Count];
        var patterns = new string[filters.Count];
        for (var i = 0; i < filters.Count; i++)
        {
            labels[i] = filters[i].Label;
            var extensions = filters[i].Extensions
                .Select(extension => extension.TrimStart('.').Trim())
                .Where(extension => extension.Length > 0)
                .Select(extension => "*." + extension)
                .ToArray();
            patterns[i] = extensions.Length > 0 ? string.Join(";", extensions) : "*.*";
        }
        var handles = new List<GCHandle>();
        try
        {
            var specs = stackalloc IntPtr[filters.Count * 2];
            for (var i = 0; i < filters.Count; i++)
            {
                var label = GCHandle.Alloc(labels[i] + "\0", GCHandleType.Pinned);
                var pattern = GCHandle.Alloc(patterns[i] + "\0", GCHandleType.Pinned);
                handles.Add(label);
                handles.Add(pattern);
                specs[i * 2] = label.AddrOfPinnedObject();
                specs[i * 2 + 1] = pattern.AddrOfPinnedObject();
            }
            Dialog.SetFileTypes(dialog, (uint)filters.Count, specs);
        }
        finally
        {
            foreach (var handle in handles) handle.Free();
        }
    }

    private static string? PathOf(void* item)
    {
        char* name;
        if (Dialog.GetDisplayName(item, SIGDN_FILESYSPATH, &name) < 0 || name is null) return null;
        try
        {
            return new string(name);
        }
        finally
        {
            Com.CoTaskMemFree((IntPtr)name);
        }
    }

    /// <summary>
    /// Runs the dialog where COM allows one. Already on the UI thread, it runs INLINE — a modal
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

    /// <summary>The dialog interfaces' vtables — <c>shobjidl_core.h</c> order: IModalWindow (3 Show),
    /// IFileDialog (4…26), then IFileOpenDialog's own (27 GetResults). IShellItem and
    /// IShellItemArray beside them.</summary>
    private static class Dialog
    {
        public static int Show(void* dialog, IntPtr owner) =>
            ((delegate* unmanaged<void*, IntPtr, int>)Com.Method(dialog, 3))(dialog, owner);

        public static int SetFileTypes(void* dialog, uint count, IntPtr* specs) =>
            ((delegate* unmanaged<void*, uint, IntPtr*, int>)Com.Method(dialog, 4))(dialog, count, specs);

        public static int SetOptions(void* dialog, uint options) =>
            ((delegate* unmanaged<void*, uint, int>)Com.Method(dialog, 9))(dialog, options);

        public static int SetFolder(void* dialog, void* item) =>
            ((delegate* unmanaged<void*, void*, int>)Com.Method(dialog, 12))(dialog, item);

        public static int SetFileName(void* dialog, char* name) =>
            ((delegate* unmanaged<void*, char*, int>)Com.Method(dialog, 15))(dialog, name);

        public static int SetTitle(void* dialog, char* title) =>
            ((delegate* unmanaged<void*, char*, int>)Com.Method(dialog, 17))(dialog, title);

        public static int GetResult(void* dialog, void** item) =>
            ((delegate* unmanaged<void*, void**, int>)Com.Method(dialog, 20))(dialog, item);

        public static int SetDefaultExtension(void* dialog, char* extension) =>
            ((delegate* unmanaged<void*, char*, int>)Com.Method(dialog, 22))(dialog, extension);

        public static int GetResults(void* openDialog, void** items) =>
            ((delegate* unmanaged<void*, void**, int>)Com.Method(openDialog, 27))(openDialog, items);

        // IShellItemArray: 7 GetCount, 8 GetItemAt.
        public static int GetCount(void* items, uint* count) =>
            ((delegate* unmanaged<void*, uint*, int>)Com.Method(items, 7))(items, count);

        public static int GetItemAt(void* items, uint index, void** item) =>
            ((delegate* unmanaged<void*, uint, void**, int>)Com.Method(items, 8))(items, index, item);

        // IShellItem: 5 GetDisplayName.
        public static int GetDisplayName(void* item, uint kind, char** name) =>
            ((delegate* unmanaged<void*, uint, char**, int>)Com.Method(item, 5))(item, kind, name);
    }
}
