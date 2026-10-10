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
    /// into <paramref name="outputDir"/> with <paramref name="bunPath"/>, and composes the maps
    /// <paramref name="sourceMaps"/> asks for. Answers null, or bun's error output when it fails. A
    /// map that cannot be composed stays as bun wrote it, and <paramref name="warn"/> hears why.
    /// </summary>
    public static string? Bundle(string bunPath, IReadOnlyCollection<string> entryPoints, string outputDir,
        string intermediateDir, SourceMapMode sourceMaps, Action<string>? warn = null)
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
        var before = Maps(outputDir);
        var mapArg = sourceMaps switch
        {
            SourceMapMode.Full => " --sourcemap",
            SourceMapMode.External => " --sourcemap=external",
            _ => "",
        };
        var bunArgs = $"build {string.Join(" ", entryPoints.Select(p => $"\"{p}\""))} --outdir \"{outputDir}\" --root \"{intermediateDir}\" --splitting{mapArg} --minify-syntax --minify-whitespace --target browser --external @equantic/runtime";

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = bunPath,
                Arguments = bunArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        process.Start();
        // Both streams drain at once: one read to its end first can fill the other's pipe and
        // stall the child.
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEnd();
        output.Wait();
        process.WaitForExit();
        if (process.ExitCode != 0) return error;

        // A map is this bundle's when bun wrote it, new or written again; any other is removed now,
        // after bun has written. A map this build does not write must not survive from one that
        // did: the output folder is shared by every configuration, so a Debug build's maps would
        // ride out with a Release publish. Every map here is eqc's own. One written in the two
        // seconds before the bundle started is kept, as the SDK's prune keeps one: a file system
        // that stores whole seconds cannot tell it from a map bun wrote again, and composing an
        // earlier bundle's map again leaves it as it was.
        var written = new List<string>();
        foreach (var (map, time) in Maps(outputDir))
        {
            if (before.TryGetValue(map, out var earlier) && earlier == time && time < started) File.Delete(map);
            else written.Add(map);
        }

        // A module's map leads to the TypeScript eqc wrote (bun), and that TypeScript's map to the
        // C# (eqc): composed here, so a debugger lands on the C#. This was a script over an npm
        // package that bun's auto-install fetched on a developer's first Debug build (#356). Only
        // this bundle's maps: an earlier bundle's leads to the C# already.
        if (sourceMaps != SourceMapMode.None)
        {
            foreach (var mapFile in written)
            {
                var mapDir = Path.GetDirectoryName(mapFile)!;
                try
                {
                    File.WriteAllText(mapFile, SourceMapComposer.Compose(File.ReadAllText(mapFile), source =>
                    {
                        var inner = Path.GetFullPath(Path.Combine(mapDir, source)) + ".map";
                        return File.Exists(inner) ? File.ReadAllText(inner) : null;
                    }));
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

    /// <summary>Every map under <paramref name="outputDir"/>, with the time it was last written.</summary>
    private static Dictionary<string, DateTime> Maps(string outputDir) =>
        Directory.Exists(outputDir)
            ? Directory.GetFiles(outputDir, "*.js.map", SearchOption.AllDirectories)
                .ToDictionary(map => map, File.GetLastWriteTimeUtc, StringComparer.Ordinal)
            : new Dictionary<string, DateTime>(StringComparer.Ordinal);
}
