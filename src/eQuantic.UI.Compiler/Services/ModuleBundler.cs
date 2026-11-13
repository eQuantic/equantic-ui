using System.Diagnostics;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// The step after eqc writes its TypeScript: bun bundles the entry modules into the JavaScript the
/// browser loads, and each module's map is composed with the map its TypeScript carries, so a
/// debugger lands on the C#. One owner, so the build and the test that proves a thrown error's frame
/// leads back to its C# line (#293) run the same bundling, flags and all.
/// </summary>
public static class ModuleBundler
{
    /// <summary>
    /// Bundles <paramref name="entryPoints"/>, TypeScript files under <paramref name="intermediateDir"/>,
    /// into <paramref name="outputDir"/> with <paramref name="bunPath"/> run in
    /// <paramref name="workingDirectory"/>, and composes the maps <paramref name="sourceMaps"/> asks
    /// for. Answers null, or bun's error output when it fails. A map that cannot be composed stays as
    /// bun wrote it, and <paramref name="warn"/> hears why.
    /// </summary>
    public static string? Bundle(string bunPath, string workingDirectory, IReadOnlyCollection<string> entryPoints,
        string outputDir, string intermediateDir, SourceMapMode sourceMaps, Action<string>? warn = null)
    {
        // --root pins the output-path base to the intermediate TS dir (where every entry .ts lives),
        // so bun writes entries FLAT as "<Page>.js" in outDir. Without it bun infers the root from the
        // common ancestor of the absolute entry paths (the repo/cwd) and nests entries under that
        // relative path (e.g. wwwroot/_equantic/samples/.../ts/Dashboard.js), which the boot — loading
        // the flat "/_equantic/<Page>.js" — then 404s on.
        // The maps already here, and when each was written: once bun has run, that is what tells its
        // maps from the rest. Nothing is removed before it runs, so the folder holds every map while
        // it runs and a bundle that fails leaves the folder as it found it, the contract the SDK
        // keeps for the whole folder (the generated-files spec).
        var started = DateTime.UtcNow.AddSeconds(-2);
        var before = Outputs(outputDir);
        string[] mapArguments = sourceMaps switch
        {
            SourceMapMode.Full => ["--sourcemap"],
            SourceMapMode.External => ["--sourcemap=external"],
            _ => [],
        };
        string[] arguments =
        [
            "build", .. entryPoints, "--outdir", outputDir, "--root", intermediateDir, "--splitting",
            .. mapArguments, "--minify-syntax", "--minify-whitespace", "--target", "browser",
            "--external", "@equantic/runtime",
        ];

        // bun names each module by its path from the folder it runs in, and mixes those names into the
        // hash a shared chunk is named by. So that folder is named here and never inherited: a bundle
        // is the same bytes wherever its caller runs, and the SDK runs eqc in the folder of its
        // response file, not the project's (#836). Each argument is handed whole, as a list: a
        // command line of quoted paths lost every double quote a folder's name held.
        var startInfo = new ProcessStartInfo
        {
            FileName = bunPath,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = startInfo };
        process.Start();
        // Both streams drain at once: one read to its end first can fill the other's pipe and
        // stall the child.
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEnd();
        output.Wait();
        process.WaitForExit();
        if (process.ExitCode != 0) return error;

        // A module, a chunk or a map is this bundle's when bun wrote it, new or written again: bun
        // writes every file of a bundle on every run. Any other is removed now, after bun has written,
        // never before, so the folder holds every module while a compile runs: a removed or renamed
        // component, a shared chunk bun renamed, the maps a Release build does not write (the output
        // folder is shared by every configuration, so a Debug build's maps would ride out with a
        // Release publish). This is the folder's one prune, and it goes by what the bundle wrote, which
        // a compile served to a running app needs as much as a build (#1279). One written in the two
        // seconds before the bundle started is kept: a file system that stores whole seconds cannot
        // tell it from one bun wrote again, and the next bundle removes it.
        var written = new List<string>();
        foreach (var (file, time) in Outputs(outputDir))
        {
            if (before.TryGetValue(file, out var earlier) && earlier == time && time < started) File.Delete(file);
            else if (file.EndsWith(".map", StringComparison.Ordinal)) written.Add(file);
        }

        // A module's map leads to the TypeScript eqc wrote (bun), and that TypeScript's map to the
        // C# (eqc): composed here, so a debugger lands on the C#. This was a script over an npm
        // package that bun's auto-install fetched on a developer's first Debug build (#356). Only
        // this bundle's maps: an earlier bundle's leads to the C# already.
        if (sourceMaps != SourceMapMode.None)
        {
            foreach (var mapFile in written)
            {
                try
                {
                    SourceMapComposer.ComposeFile(mapFile);
                }
                catch (Exception ex) when (ex is FormatException or System.Text.Json.JsonException or IOException)
                {
                    // A map is a debugging aid, so one that cannot be composed stays as bun wrote
                    // it, leading to the TypeScript, and the build says so.
                    warn?.Invoke($"⚠️ {Path.GetFileName(mapFile)} leads to the TypeScript only: {ex.Message}");
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Every module, chunk and map under <paramref name="outputDir"/>, with the time it was last written:
    /// what bun writes, and the runtime aside, which the SDK copies there and is not the bundle's.
    /// </summary>
    private static Dictionary<string, DateTime> Outputs(string outputDir) =>
        Directory.Exists(outputDir)
            ? Directory.GetFiles(outputDir, "*.js", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(outputDir, "*.js.map", SearchOption.AllDirectories))
                .Where(file => !Path.GetFileName(file).StartsWith("runtime.js", StringComparison.Ordinal))
                .ToDictionary(file => file, File.GetLastWriteTimeUtc, StringComparer.Ordinal)
            : new Dictionary<string, DateTime>(StringComparer.Ordinal);
}
