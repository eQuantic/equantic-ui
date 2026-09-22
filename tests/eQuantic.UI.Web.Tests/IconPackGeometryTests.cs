using System.Globalization;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Every glyph the SDK ships draws each of its elements where the icon drew it.
/// <para>
/// An <c>IconGlyph</c> is ONE path and most icons are several elements, so the pack generator joins
/// them. A relative moveto that opens an element is relative to the ORIGIN when the element stands
/// alone and to where the previous element ENDED once joined: Lucide's <c>circle-check</c> drew its
/// circle and not its tick, and 941 glyphs across six packs shipped that way in 0.2.0-preview.56
/// (#320). Both realizers read the same string, so the web and Photon agreed on the wrong picture
/// and every cross-pin in the repository held — agreement is not correctness.
/// </para>
/// <para>
/// Two assertions, over every pack found by its <c>EQuanticIconPack.marker</c> — derived, never
/// listed — and the curated set in Primitives. The SEAM: the generator writes each element's first
/// moveto absolute and drops whitespace before commands inside an element, so in a generated path a
/// command follows a space only at a join, and a relative moveto there IS the defect. The BOX: every
/// subpath starts inside its glyph's viewBox, within the stroke width or 5% of the box. The largest
/// honest overflow on the pinned sources is 2.99% (Font Awesome's <c>meetup</c>); the defect put
/// starts 30–40% out.
/// </para>
/// </summary>
public class IconPackGeometryTests
{
    private sealed record Glyph(
        string Source, string Name, string Path, bool Stroke, float Width, float Height, float StrokeWidth, bool Generated);

    private static readonly Lazy<(IReadOnlyList<Glyph> Glyphs, IReadOnlyList<string> Unparsed)> Scan = new(Load);

    private static readonly Regex SeamWithARelativeMoveto = new(@"\sm", RegexOptions.Compiled);
    private static readonly Regex Number = new(@"\G[+-]?(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?", RegexOptions.Compiled);

    [Fact]
    public void The_scan_reads_every_pack_and_every_glyph_it_declares()
    {
        var (glyphs, unparsed) = Scan.Value;

        unparsed.Should().BeEmpty("a glyph the tracer cannot read is a glyph neither assertion checked");
        glyphs.Where(g => g.Generated).Select(g => g.Source).Distinct().Should().NotBeEmpty(
            "the packs are found by their EQuanticIconPack.marker; none found means this class checked nothing");
        glyphs.Should().Contain(g => !g.Generated, "the curated set in Primitives is scanned too");
    }

    [Fact]
    public void No_element_of_a_generated_glyph_opens_with_a_relative_moveto()
    {
        var offenders = Scan.Value.Glyphs
            .Where(g => g.Generated && SeamWithARelativeMoveto.IsMatch(g.Path))
            .Select(g => $"{g.Source}: {g.Name}")
            .ToList();

        string.Join(Environment.NewLine, offenders.Take(40)).Should().BeEmpty(
            $"{offenders.Count} generated glyph(s) join an element whose first moveto is relative, so it starts where "
            + "the previous element ended rather than where the icon drew it — scripts/generate-icons.mjs writes every "
            + "element's first moveto absolute (#320)");
    }

    [Fact]
    public void Every_subpath_of_every_glyph_starts_inside_its_viewbox()
    {
        var offenders = new List<string>();
        foreach (var g in Scan.Value.Glyphs)
        {
            var tolerance = Math.Max(g.Stroke ? g.StrokeWidth : 0f, 0.05f * Math.Max(g.Width, g.Height));
            foreach (var (x, y) in SubpathStarts(g.Path))
            {
                if (x < -tolerance || x > g.Width + tolerance || y < -tolerance || y > g.Height + tolerance)
                {
                    offenders.Add($"{g.Source}: {g.Name} starts a subpath at ({x:0.##}, {y:0.##}) in a {g.Width:0.##}×{g.Height:0.##} box");
                    break;
                }
            }
        }

        string.Join(Environment.NewLine, offenders.Take(40)).Should().BeEmpty(
            $"{offenders.Count} glyph(s) start a subpath outside the box they are drawn in, which no honest icon on the "
            + "pinned sources does by more than 3% of the box");
    }

    // ---- reading the glyphs ---------------------------------------------------------------------

    private static (IReadOnlyList<Glyph>, IReadOnlyList<string>) Load()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "src", "eQuantic.UI.Runtime")))
            here = here.Parent;
        here.Should().NotBeNull("the suite runs inside the repository");
        var src = Path.Combine(here!.FullName, "src");

        var glyphs = new List<Glyph>();
        var unparsed = new List<string>();
        foreach (var pack in Directory.GetDirectories(src).Where(d => File.Exists(Path.Combine(d, "EQuanticIconPack.marker"))).Order(StringComparer.Ordinal))
        {
            var catalogs = Directory.GetFiles(pack, "*Icons.cs");
            catalogs.Should().HaveCount(1, $"a generated pack is one catalog file, and {Path.GetFileName(pack)} has {catalogs.Length}");
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(catalogs[0])).GetRoot();

            var declared = root.DescendantNodes().OfType<PropertyDeclarationSyntax>().Count(p => p.Type.ToString() == "IconGlyph");
            var read = Read(Path.GetFileName(pack), root.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>(), generated: true, unparsed);
            read.Should().HaveCount(declared, $"every IconGlyph {Path.GetFileName(pack)} declares is read, so none escapes the assertions");
            glyphs.AddRange(read);
        }

        var curated = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(src, "eQuantic.UI.Primitives", "Nodes", "IconGlyph.cs"))).GetRoot();
        glyphs.AddRange(Read("eQuantic.UI.Primitives (curated)",
            curated.DescendantNodes().OfType<ObjectCreationExpressionSyntax>().Where(o => o.Type.ToString() == "IconGlyph"),
            generated: false, unparsed));

        return (glyphs, unparsed);
    }

    private static List<Glyph> Read(string source, IEnumerable<BaseObjectCreationExpressionSyntax> creations, bool generated, List<string> unparsed)
    {
        var glyphs = new List<Glyph>();
        foreach (var creation in creations)
        {
            if (creation.ArgumentList is not { Arguments: { Count: >= 2 } args }
                || args[0].Expression is not LiteralExpressionSyntax { Token.Value: string name }
                || args[1].Expression is not LiteralExpressionSyntax { Token.Value: string path })
            {
                continue;
            }

            bool stroke = false; float width = 24, height = 24, strokeWidth = 2;
            foreach (var arg in args.Skip(2))
            {
                switch (arg.Expression)
                {
                    case MemberAccessExpressionSyntax style:
                        stroke = style.Name.Identifier.Text == "Stroke";
                        break;
                    case LiteralExpressionSyntax literal when literal.Token.Value is string viewBox:
                        var box = viewBox.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        width = float.Parse(box[2], CultureInfo.InvariantCulture);
                        height = float.Parse(box[3], CultureInfo.InvariantCulture);
                        break;
                    case LiteralExpressionSyntax literal when literal.Token.Value is float or double or int:
                        strokeWidth = Convert.ToSingle(literal.Token.Value, CultureInfo.InvariantCulture);
                        break;
                }
            }

            var glyph = new Glyph(source, name, path, stroke, width, height, strokeWidth, generated);
            try { _ = SubpathStarts(glyph.Path).ToList(); glyphs.Add(glyph); }
            catch (FormatException e) { unparsed.Add($"{source}: {glyph.Name} — {e.Message}"); }
        }
        return glyphs;
    }

    // ---- tracing a path ---------------------------------------------------------------------------

    /// <summary>The absolute start of every subpath. Arc flags are ONE character each, which compact
    /// path data depends on (<c>a1 1 0 01-1 1</c> is flags 0 and 1, then -1).</summary>
    private static IEnumerable<(float X, float Y)> SubpathStarts(string d)
    {
        float x = 0, y = 0, sx = 0, sy = 0;
        char command = '\0';
        var i = 0;
        while (true)
        {
            Skip(d, ref i);
            if (i >= d.Length) yield break;
            if (char.IsLetter(d[i]))
            {
                command = d[i++];
                if (char.ToUpperInvariant(command) == 'Z') { x = sx; y = sy; continue; }
            }
            else if (command == '\0')
            {
                throw new FormatException($"a number before any command at {i}");
            }

            var relative = char.IsLower(command);
            switch (char.ToUpperInvariant(command))
            {
                case 'M':
                    var (mx, my) = (Read(d, ref i), Read(d, ref i));
                    (x, y) = relative ? (x + mx, y + my) : (mx, my);
                    (sx, sy) = (x, y);
                    yield return (x, y);
                    command = relative ? 'l' : 'L';
                    break;
                case 'L': case 'T':
                    Move(ref x, ref y, relative, Read(d, ref i), Read(d, ref i));
                    break;
                case 'H':
                    var h = Read(d, ref i); x = relative ? x + h : h;
                    break;
                case 'V':
                    var v = Read(d, ref i); y = relative ? y + v : v;
                    break;
                case 'C':
                    for (var k = 0; k < 4; k++) Read(d, ref i);
                    Move(ref x, ref y, relative, Read(d, ref i), Read(d, ref i));
                    break;
                case 'S': case 'Q':
                    Read(d, ref i); Read(d, ref i);
                    Move(ref x, ref y, relative, Read(d, ref i), Read(d, ref i));
                    break;
                case 'A':
                    Read(d, ref i); Read(d, ref i); Read(d, ref i);
                    Flag(d, ref i); Flag(d, ref i);
                    Move(ref x, ref y, relative, Read(d, ref i), Read(d, ref i));
                    break;
                default:
                    throw new FormatException($"'{command}' is not a path command");
            }
        }
    }

    private static void Move(ref float x, ref float y, bool relative, float nx, float ny) =>
        (x, y) = relative ? (x + nx, y + ny) : (nx, ny);

    private static void Skip(string d, ref int i)
    {
        while (i < d.Length && (char.IsWhiteSpace(d[i]) || d[i] == ',')) i++;
    }

    private static float Read(string d, ref int i)
    {
        Skip(d, ref i);
        var m = Number.Match(d, i);
        if (!m.Success) throw new FormatException($"a number was expected at {i}");
        i += m.Length;
        return float.Parse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    private static void Flag(string d, ref int i)
    {
        Skip(d, ref i);
        if (i >= d.Length || (d[i] != '0' && d[i] != '1')) throw new FormatException($"an arc flag was expected at {i}");
        i++;
    }
}
