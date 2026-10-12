using System.Text.Json;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// Track L D3/D12: the per-culture string catalogs, from exactly the keys the compiled tree used. The
/// whole fallback chain is FLATTENED here (a key present only in a parent culture's resx, or only in
/// the neutral one, appears in every catalog that falls back through it), so the runtime does a flat
/// lookup and never reimplements .NET's resolution.
/// </summary>
internal static class StringCatalogs
{
    /// <summary>Writes <c>strings/&lt;culture&gt;.json</c> under <paramref name="outputDirectory"/> for
    /// what <paramref name="compiler"/> read, and says so on <paramref name="output"/>.</summary>
    /// <param name="compiler">The compiler every module of the build went through.</param>
    /// <param name="outputDirectory">The folder the bundled modules are written to.</param>
    /// <param name="appSourceDirectory">The app's own directory, which tells its resx from a library's.</param>
    /// <param name="isOfAReferencedProject">Whether a file belongs to a project the app references,
    /// whose reads this build sees as it sees the app's own.</param>
    /// <param name="output">Where the build narrates.</param>
    /// <param name="errors">Where the build warns.</param>
    public static void Write(ComponentCompiler compiler, string outputDirectory, string appSourceDirectory,
        Func<string, bool> isOfAReferencedProject, TextWriter output, TextWriter errors)
    {
        // Two contributions, and the difference is not a preference — it is what each side can KNOW.
        // The app's own resx contributes the keys its pages actually read (D3: unused keys never
        // ship), because this compilation sees those reads. A LIBRARY's resx (the SDK's own
        // SdkResources above all) is read by components already transpiled into runtime.js at the
        // SDK's build, so no read is visible here at all — it contributes WHOLE, which is exactly
        // D14's promise that an app with zero resx still announces "Marcado" to a pt-BR reader.
        // A project the app REFERENCES is on the app's side of that line (#624): its types are
        // compiled here when a page reaches them, so its reads are seen, and it contributes the
        // keys read. Whole, a shared library's catalogue would ship to every page of every app.
        var appRoot = Path.GetFullPath(appSourceDirectory);
        var libraryResources = compiler.DiscoveredResources
            .Where(pair => pair.Value.Length > 0
                && !Path.GetFullPath(pair.Value).StartsWith(appRoot, StringComparison.Ordinal)
                && !isOfAReferencedProject(pair.Value))
            .ToList();

        var uses = compiler.ResourceUses;
        if (uses.Count == 0 && libraryResources.Count == 0) return;

        var neutral = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var cultures = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        SortedDictionary<string, string> CatalogFor(string culture) =>
            culture.Length == 0
                ? neutral
                : cultures.TryGetValue(culture, out var existing)
                    ? existing
                    : cultures[culture] = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var (id, designerPath) in libraryResources)
        {
            foreach (var (culture, path) in ResxFiles.VariantsFor(designerPath))
            {
                var values = ResxFiles.Read(path);
                if (values is null) continue;
                var target = CatalogFor(culture);
                foreach (var (key, value) in values) target[$"{id}/{key}"] = value;
            }
        }

        foreach (var use in uses)
        {
            var variants = ResxFiles.VariantsFor(use.DesignerPath).ToList();
            if (variants.Count == 0)
            {
                errors.WriteLine($"⚠️  strings: no .resx found beside {use.DesignerPath} — " +
                    $"'{use.Id}' resolves on the server and native, but the web catalog has no values.");
                continue;
            }
            foreach (var (culture, path) in variants)
            {
                var values = ResxFiles.Read(path);
                if (values is null) continue;
                var target = CatalogFor(culture);
                foreach (var key in use.Keys)
                {
                    if (values.TryGetValue(key, out var value)) target[$"{use.Id}/{key}"] = value;
                    else if (culture.Length == 0)
                        errors.WriteLine($"⚠️  strings: key '{key}' is read by a page but " +
                            $"missing from {path}.");
                }
            }
        }

        // D12: flatten — the client lookup is flat, so each culture is folded along the chain .NET's
        // own ResourceManager walks, NEAREST ancestor first. Folding the neutral alone was the same
        // answer whenever a culture's parent IS the neutral one, and a different page when it is not:
        // `pt-BR` falls back through `pt`, so a key translated once in Strings.pt.resx and not repeated
        // in Strings.pt-BR.resx came back in ENGLISH on the client while the server rendered it in
        // Portuguese. TryAdd is what makes nearest win.
        ResxFiles.FoldFallbackChains(neutral, cultures);

        // The catalogs carry STRINGS and nothing else. How a culture formats a number or a date is not
        // the build's to say: the server writes it from .NET for every page, catalog or not, from the
        // culture the request is in (CultureBridge, #471). Written here, it travelled only with a
        // catalog, from the build machine's ICU, and for the UI culture rather than the format one.

        var stringsDir = Path.Combine(outputDirectory, "strings");
        Directory.CreateDirectory(stringsDir);
        var options = new JsonSerializerOptions { WriteIndented = false };
        File.WriteAllText(Path.Combine(stringsDir, "neutral.json"), JsonSerializer.Serialize(neutral, options));
        foreach (var (culture, strings) in cultures)
            File.WriteAllText(Path.Combine(stringsDir, culture + ".json"), JsonSerializer.Serialize(strings, options));
        output.WriteLine($"🌐 Strings: {neutral.Count} keys → neutral" +
            (cultures.Count > 0 ? " + " + string.Join(", ", cultures.Keys) : ""));
    }
}
