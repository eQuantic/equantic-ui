using System.Xml.Linq;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// Reads the <c>.resx</c> files beside a Designer (Track L D3 catalogs, EQ2100 validation). The
/// Designer path is the anchor: <c>Strings.Designer.cs</c> sits beside <c>Strings.resx</c> and its
/// culture variants (<c>Strings.pt-BR.resx</c>) — the ResXFileCodeGenerator contract, which is
/// also why the SDK keeps generated Designers next to their resx rather than under obj/.
/// </summary>
public static class ResxFiles
{
    private const string DesignerSuffix = ".Designer.cs";

    /// <summary>The neutral resx path for a Designer, or null when the path has no Designer shape.</summary>
    public static string? NeutralPathFor(string designerPath)
    {
        if (!designerPath.EndsWith(DesignerSuffix, StringComparison.OrdinalIgnoreCase)) return null;
        return designerPath[..^DesignerSuffix.Length] + ".resx";
    }

    /// <summary>
    /// The cultures a lookup falls back THROUGH, nearest first and the neutral catalogue (<c>""</c>)
    /// last — the chain .NET's own <c>ResourceManager</c> walks, which is what the server already
    /// answers with.
    /// <para>
    /// The client catalogue is flat, so it has to be built by folding these in order. Folding the
    /// NEUTRAL alone is the same answer for a culture whose parent IS neutral, and a different page
    /// for one whose parent is not: <c>pt-BR</c> falls back through <c>pt</c>, so a key translated
    /// once in <c>Strings.pt.resx</c> and not repeated in <c>Strings.pt-BR.resx</c> came back in
    /// ENGLISH on the client while the server rendered it in Portuguese. Invisible in this
    /// repository's own resources, which declare <c>es</c> and <c>pt-BR</c> and no intermediate
    /// parent — so their chain and the neutral fold happen to agree.
    /// </para>
    /// <para>
    /// A name .NET does not know is not an error here: a resx variant may be named anything, and a
    /// catalogue nobody can place a parent for still falls back to the neutral one.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> FallbackChain(string culture)
    {
        if (culture.Length == 0) return [];
        var chain = new List<string>();
        try
        {
            for (var parent = System.Globalization.CultureInfo.GetCultureInfo(culture).Parent;
                 parent.Name.Length > 0;
                 parent = parent.Parent)
            {
                chain.Add(parent.Name);
            }
        }
        catch (System.Globalization.CultureNotFoundException)
        {
            // Unknown to .NET: it still gets the neutral catalogue below, like every other culture.
        }

        chain.Add("");
        return chain;
    }

    /// <summary>
    /// Folds every named culture's catalogue along its <see cref="FallbackChain"/>, so a flat client
    /// lookup answers what <c>ResourceManager</c> answers on the server. Nearest ancestor wins,
    /// because each key is added only where it is still missing.
    /// <para>
    /// Order-independent by construction: each culture walks its WHOLE chain rather than trusting
    /// its parent to have been folded already, so `pt-BR` reaches the neutral catalogue whether or
    /// not `pt` has been visited yet.
    /// </para>
    /// </summary>
    public static void FoldFallbackChains(
        IReadOnlyDictionary<string, string> neutral,
        IReadOnlyDictionary<string, SortedDictionary<string, string>> cultures)
    {
        foreach (var (culture, strings) in cultures)
        {
            foreach (var ancestor in FallbackChain(culture))
            {
                IReadOnlyDictionary<string, string>? values = ancestor.Length == 0
                    ? neutral
                    : cultures.TryGetValue(ancestor, out var parent) ? parent : null;
                if (values is null) continue;
                foreach (var (key, value) in values) strings.TryAdd(key, value);
            }
        }
    }

    /// <summary>Every culture the resx family declares: <c>("", neutral)</c> first, then each
    /// <c>Base.{culture}.resx</c> variant found beside it. Only families that exist on disk.</summary>
    public static IEnumerable<(string Culture, string Path)> VariantsFor(string designerPath)
    {
        var neutral = NeutralPathFor(designerPath);
        if (neutral is null || !File.Exists(neutral)) yield break;
        yield return ("", neutral);

        var directory = Path.GetDirectoryName(neutral);
        if (directory is null) yield break;
        var baseName = Path.GetFileNameWithoutExtension(neutral);
        foreach (var candidate in Directory.GetFiles(directory, baseName + ".*.resx"))
        {
            var middle = Path.GetFileNameWithoutExtension(candidate)[(baseName.Length + 1)..];
            if (middle.Length > 0) yield return (middle, candidate);
        }
    }

    /// <summary>The string entries of one resx — <c>data</c> elements without a <c>type</c> or
    /// <c>mimetype</c> (non-string resources are outside the track). Null when the file is missing
    /// or unreadable: validation and catalogs simply have nothing to say about it.</summary>
    public static Dictionary<string, string>? Read(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var document = XDocument.Load(path);
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var data in document.Root?.Elements("data") ?? [])
            {
                if (data.Attribute("type") is not null || data.Attribute("mimetype") is not null) continue;
                var name = data.Attribute("name")?.Value;
                if (string.IsNullOrEmpty(name)) continue;
                values[name] = data.Element("value")?.Value ?? "";
            }
            return values;
        }
        catch (Exception e) when (e is IOException or System.Xml.XmlException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
