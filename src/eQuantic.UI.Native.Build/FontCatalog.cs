using System.Reflection;
using System.Text;
using eQuantic.UI.Codegen;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Build;

/// <summary>
/// The app's font files, read once at BUILD time and written as one C# class with a
/// <see cref="FontFamily"/> per family.
/// <para>
/// The files are embedded in the app's assembly by the SDK, under the resource names it hands over
/// here, and every fact a family needs is read from the files themselves (<see cref="OpenTypeFace"/>):
/// an app declares nothing but the file. The catalog carries the result as source, so a theme names
/// <c>Fonts.AtkinsonHyperlegibleNext</c> and a wrong name is a compile error, not a face that falls
/// back in silence.
/// </para>
/// </summary>
public static class FontCatalog
{
    /// <summary>The class the catalog is, which no member of it may be named after.</summary>
    private const string ClassName = "Fonts";

    /// <summary>The names a member of the class may not take: the class's own, and every member a class
    /// inherits from <see cref="object"/>, read from it rather than listed.</summary>
    private static readonly HashSet<string> Taken =
    [
        ClassName,
        .. typeof(object).GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Select(member => member.Name),
    ];

    /// <summary>One file the SDK embeds: where it is on disk, and the resource name it is embedded under.</summary>
    /// <param name="Path">The file on disk.</param>
    /// <param name="Resource">Its manifest resource name in the app's assembly.</param>
    public sealed record Source(string Path, string Resource);

    /// <summary>
    /// The files a <c>--list</c> file names, each as two lines: the file's path, then the resource name
    /// it is embedded under. The SDK writes it from the items it embeds, so nothing in it is set by hand.
    /// A value is a line whole: one line a file, <c>path|resource</c>, was cut at the first bar, which a
    /// path may hold on macOS and Linux, and the record cut wrong was passed over in silence, a catalog
    /// with no face in it (Copilot's second review of #1062). A list that does not hold is never read
    /// as fewer files: it throws, and the build stops on it, as eqc's list of projects does (#836).
    /// </summary>
    public static IReadOnlyList<Source> ReadList(string list)
    {
        if (!File.Exists(list)) throw new FileNotFoundException($"the list of fonts is not there: '{list}'", list);

        // Only the blank lines that end the file are nothing: one inside it would pair a file's path
        // with the next file's.
        var lines = File.ReadAllLines(list);
        var count = lines.Length;
        while (count > 0 && string.IsNullOrWhiteSpace(lines[count - 1])) count--;
        for (var at = 0; at < count; at++)
            if (string.IsNullOrWhiteSpace(lines[at]))
                throw new InvalidDataException($"line {at + 1} of the list of fonts is blank, where a path or a resource name was");
        if (count % 2 != 0)
            throw new InvalidDataException($"line {count} of the list of fonts is a file with no resource name after it: '{lines[count - 1]}'");

        var files = new List<Source>();
        for (var at = 0; at < count; at += 2)
        {
            if (!File.Exists(lines[at]))
                throw new FileNotFoundException($"the font line {at + 1} of the list names is not there: '{lines[at]}'", lines[at]);
            files.Add(new Source(lines[at], lines[at + 1]));
        }
        return files;
    }

    /// <summary>
    /// Writes the catalog of <paramref name="files"/>, embedded in <paramref name="assembly"/>, to
    /// <paramref name="outputPath"/>. Returns the families it carried, or the errors that stopped it,
    /// each one line in MSBuild's own form so the build reports it against the file. No files write
    /// nothing at all, so an app with no fonts gets no class to wonder about.
    /// </summary>
    public static (int Families, IReadOnlyList<string> Errors) Write(IReadOnlyList<Source> files, string assembly,
        string outputPath, string @namespace)
    {
        var errors = new List<string>();
        var faces = new List<(Source File, OpenTypeFace Face)>();
        foreach (var file in files.OrderBy(file => file.Resource, StringComparer.Ordinal))
        {
            try
            {
                var face = OpenTypeFace.Read(File.ReadAllBytes(file.Path));
                if (!FaceName.IsWellFormed(face.Family))
                    errors.Add(Error(file.Path, "EQ4005",
                        $"its family, '{face.Family}', is not a name a page can be set in: letters, digits, spaces and '-_.+' only"));
                else
                    faces.Add((file, face));
            }
            catch (Exception exception) when (exception is FormatException or IOException)
            {
                errors.Add(Error(file.Path, "EQ4005", $"{exception.Message}"));
            }
        }

        var named = faces
            .GroupBy(entry => entry.Face.Family, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => (Family: group.Key, Member: MemberName(group.Key),
                Files: group.OrderBy(entry => entry.Face.Italic).ThenBy(entry => entry.Face.MinWeight).ToList()))
            .ToList();
        foreach (var nameless in named.Where(family => family.Member is null))
            errors.Add(Error(nameless.Files[0].File.Path, "EQ4005",
                $"its family, '{nameless.Family}', has no letter or digit to name its member of the catalog by"));
        var families = named
            .Where(family => family.Member is not null)
            .Select(family => (family.Family, Member: family.Member!, family.Files))
            .ToList();

        foreach (var family in families)
        {
            for (var i = 0; i < family.Files.Count; i++)
            for (var j = i + 1; j < family.Files.Count; j++)
            {
                var (a, b) = (family.Files[i], family.Files[j]);
                if (a.Face.Italic == b.Face.Italic && a.Face.MinWeight <= b.Face.MaxWeight && b.Face.MinWeight <= a.Face.MaxWeight)
                    errors.Add(Error(b.File.Path, "EQ4006",
                        $"it sets {Describe(b.Face)} of '{family.Family}', and so does {System.IO.Path.GetFileName(a.File.Path)}; keep one"));
            }
        }
        foreach (var clash in families.GroupBy(family => family.Member, StringComparer.Ordinal).Where(group => group.Count() > 1))
            errors.Add(Error(clash.First().Files[0].File.Path, "EQ4006",
                $"the families {string.Join(" and ", clash.Select(family => $"'{family.Family}'"))} would all be {ClassName}.{clash.Key}; keep one"));

        if (errors.Count > 0 || families.Count == 0) return (0, errors);

        var code = new CodeWriter();
        code.AppendLine("// <auto-generated/>");
        code.AppendLine("// Written by eqicon from the font files under Assets — do not edit by hand.");
        code.AppendLine("#nullable enable");
        code.AppendLine("using eQuantic.UI.Primitives;");
        code.AppendLine();
        code.AppendLine($"[assembly: FontCatalog(typeof({@namespace}.{ClassName}))]");
        code.AppendLine();
        code.AppendLine($"namespace {@namespace};");
        code.AppendLine();
        code.AppendLine("/// <summary>The faces this app ships, read from the font files under its <c>Assets</c> folder at");
        code.AppendLine("/// build time. Name one in the theme: <c>TextFamily =&gt; Fonts.Name</c>.</summary>");
        code.AppendLine($"public static class {ClassName}");
        using (code.BeginScope())
        {
            foreach (var family in families)
            {
                code.AppendLine($"/// <summary>{family.Family}: "
                    + string.Join(", ", family.Files.Select(entry =>
                        $"<c>{System.IO.Path.GetFileName(entry.File.Path)}</c> ({Describe(entry.Face)})")) + ".</summary>");
                code.AppendLine($"public static FontFamily {family.Member} {{ get; }} = new({Literal(family.Family)},");
                code.IndentLevel++;
                for (var i = 0; i < family.Files.Count; i++)
                {
                    var (file, face) = family.Files[i];
                    code.AppendLine($"new FontFile({Literal(assembly)}, {Literal(file.Resource)}, {face.MinWeight}, {face.MaxWeight}"
                        + (face.Italic ? ", Italic: true)" : ")") + (i == family.Files.Count - 1 ? ");" : ","));
                }
                code.IndentLevel--;
                code.AppendLine();
            }
        }

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outputPath)!);
        var text = code.ToString();
        // Only when it CHANGED: rewriting the file every build re-triggers the compiler.
        if (!File.Exists(outputPath) || File.ReadAllText(outputPath) != text)
            File.WriteAllText(outputPath, text);
        return (families.Count, errors);
    }

    /// <summary>"weights 200 to 800, italic", or "weight 400".</summary>
    private static string Describe(OpenTypeFace face) =>
        (face.MinWeight == face.MaxWeight ? $"weight {face.MinWeight}" : $"weights {face.MinWeight} to {face.MaxWeight}")
        + (face.Italic ? ", italic" : "");

    /// <summary>An MSBuild error against a file, the form the build reports with its code and place.</summary>
    private static string Error(string path, string code, string message) =>
        $"{path} : error {code}: {System.IO.Path.GetFileName(path)} is not a face the SDK can ship: {message}.";

    /// <summary>
    /// A family's name as a C# member: <c>Atkinson Hyperlegible Next</c> → <c>AtkinsonHyperlegibleNext</c>.
    /// A name C# would refuse there takes what it needs: <c>_</c> before a digit, and <c>Family</c> after
    /// the class's own name or a member every class inherits, since <c>Fonts.Fonts</c> does not compile
    /// and a <c>ToString</c> hides object's. Null for a family with no letter or digit, which no member
    /// can be named after.
    /// </summary>
    private static string? MemberName(string family)
    {
        var name = new StringBuilder();
        var capitalize = true;
        foreach (var c in family)
        {
            if (!char.IsLetterOrDigit(c))
            {
                capitalize = true;
                continue;
            }
            name.Append(capitalize ? char.ToUpperInvariant(c) : c);
            capitalize = false;
        }
        if (name.Length == 0) return null;
        // A member cannot start with a digit, and `_3270` is what C# itself would need.
        var member = char.IsDigit(name[0]) ? "_" + name : name.ToString();
        return Taken.Contains(member) ? member + "Family" : member;
    }

    private static string Literal(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
