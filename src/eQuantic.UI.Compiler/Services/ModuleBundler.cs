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
        // A map this build does not write must not survive from one that did: the output folder
        // is shared by every configuration, so a Debug build's maps would ride out with a Release
        // publish. Every map here is eqc's own; the ones this build writes are written again.
        if (Directory.Exists(outputDir))
            foreach (var stale in Directory.GetFiles(outputDir, "*.js.map", SearchOption.AllDirectories))
                File.Delete(stale);
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

        // A module's map leads to the TypeScript eqc wrote (bun), and that TypeScript's map to the
        // C# (eqc): composed here, so a debugger lands on the C#. This was a script over an npm
        // package that bun's auto-install fetched on a developer's first Debug build (#356).
        if (sourceMaps != SourceMapMode.None)
        {
            foreach (var mapFile in Directory.GetFiles(outputDir, "*.js.map", SearchOption.AllDirectories))
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
}
