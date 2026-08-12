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
