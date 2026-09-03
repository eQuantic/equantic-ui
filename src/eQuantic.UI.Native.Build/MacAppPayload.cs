namespace eQuantic.UI.Build;

/// <summary>
/// What goes inside <c>Contents/MacOS</c>, decided from a directory of build or publish output.
/// <para>
/// This used to be a glob in the SDK's targets, and a glob is where the rules that matter here
/// cannot be tested: each of them was learned from a bundle that failed to sign or an app that
/// failed to launch, and none of them is guessable from the file name alone.
/// </para>
/// <para>
/// The reason the rules are strict rather than generous: <c>codesign</c> refuses to sign a bundle
/// containing a file it cannot sign, and it refuses the WHOLE bundle, not the file. So one stray
/// <c>.pdb</c>, or one static archive under a runtime pack for a platform this app will never run
/// on, leaves the app unsigned — and an unsigned app is quietly refused by the very capabilities
/// that check who is asking (LocalAuthentication simply never shows its sheet, and says nothing).
/// </para>
/// </summary>
public static class MacAppPayload
{
    /// <summary>
    /// The files to place under <c>Contents/MacOS</c>, as paths relative to <paramref name="payloadDir"/>.
    /// <para>
    /// <paramref name="excluded"/> is for what PACKAGING itself writes into the payload directory
    /// — a file or a whole directory. Two of them, both found the same way, by packaging twice:
    /// <list type="bullet">
    /// <item>the publish directory lives under the build output, so a build-output bundle that does
    /// not exclude it ships a second, entire copy of the app inside itself (86 MB of it, measured
    /// on this repo's own desktop sample);</item>
    /// <item>the disk image lands beside the app in the publish directory, so the NEXT publish
    /// copies it into <c>Contents/MacOS</c> — and codesign then refuses the whole bundle, because a
    /// .dmg is a subcomponent it cannot sign.</item>
    /// </list>
    /// The caller passes the paths because MSBuild knows them. Excluding by NAME instead would drop
    /// an app's own data folder that happened to be called "publish", or a disk image it ships on
    /// purpose.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> Select(string payloadDir, params string[] excluded)
    {
        var root = Path.GetFullPath(payloadDir);
        // A file is excluded by being ITSELF; a directory by being a prefix of what is under it.
        // Appending a separator to every entry (which is right for a directory) silently stopped
        // matching the disk image, whose path names one file.
        var skip = excluded
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .ToArray();

        var selected = new List<string>();
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var full = Path.GetFullPath(file);
            if (skip.Any(path => full == path
                || full.StartsWith(EnsureTrailingSeparator(path), StringComparison.Ordinal))) continue;

            var relative = Path.GetRelativePath(root, full);
            if (Includes(relative)) selected.Add(relative);
        }

        // Sorted so a bundle is the same bundle whatever order the file system enumerated in —
        // the copy is idempotent either way, but a stable list is one a test can pin.
        selected.Sort(StringComparer.Ordinal);
        return selected;
    }

    private static bool Includes(string relative)
    {
        var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // A bundle inside the payload is the OUTPUT of a previous run, never its input.
        if (segments.Any(segment => segment.EndsWith(".app", StringComparison.OrdinalIgnoreCase)))
            return false;

        // Debug symbols are not payload. They are also not merely wasteful: codesign cannot sign
        // one, and refuses the bundle that holds it.
        if (relative.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)) return false;

        // runtimes/ is an ALLOWLIST, not a denylist: only RIDs a macOS process can actually load
        // belong in the bundle. The denylist this replaces named win/linux/android/ios and let
        // `browser-wasm` through — Microsoft.Data.Sqlite ships a static archive (e_sqlite3.a) under
        // it, which is not Mach-O, and codesign refused the entire bundle. The next RID the
        // ecosystem invents is excluded here by construction instead of re-opening this.
        if (segments is ["runtimes", var rid, ..])
            return rid.StartsWith("osx", StringComparison.OrdinalIgnoreCase)
                || rid.StartsWith("unix", StringComparison.OrdinalIgnoreCase);

        return true;
    }

    /// <summary>
    /// Rebuilds <c>Contents/MacOS</c> from the payload — REBUILDS, not tops up.
    /// <para>
    /// A copy alone never removes anything, so a file the payload stopped including (a package
    /// dropped, a RID newly excluded) stayed in the bundle forever and kept failing codesign long
    /// after the exclusion was fixed. Removing the directory first is exactly the sentence "the
    /// bundle is the payload".
    /// </para>
    /// </summary>
    public static int Populate(string payloadDir, string bundleDir, params string[] excluded)
    {
        var files = Select(payloadDir, excluded);
        var destination = Path.Combine(bundleDir, "Contents", "MacOS");
        if (Directory.Exists(destination)) Directory.Delete(destination, recursive: true);
        Directory.CreateDirectory(destination);

        foreach (var relative in files)
        {
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(payloadDir, relative), target, overwrite: true);
        }

        return files.Count;
    }

    private static string EnsureTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;
}
