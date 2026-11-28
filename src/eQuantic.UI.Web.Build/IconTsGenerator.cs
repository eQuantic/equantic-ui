using eQuantic.UI.Codegen;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Build;

/// <summary>Generates <c>icons.generated.ts</c> from <see cref="IconRegistry"/> — the client never
/// hand-writes glyph path data, nor which glyphs match the text direction (the CSS/theme generation rule
/// applied to icons).</summary>
public static class IconTsGenerator
{
    public static string Generate()
    {
        var ts = new CodeWriter();
        ts.AppendLine("/**");
        ts.AppendLine(" * GENERATED — do not edit. Glyph path data comes from the C# IconRegistry single source.");
        ts.AppendLine(" * Regenerate: EQ_UPDATE_ICONS_TS=1 dotnet test eQuantic.UI.Web.Tests (IconTsGeneratorTests).");
        ts.AppendLine(" */");
        ts.AppendLine();
        ts.AppendLine("export const iconPaths: Record<string, string> = {");
        foreach (var glyph in Enum.GetValues<Icons>())
        {
            var name = char.ToLowerInvariant(glyph.ToString()[0]) + glyph.ToString()[1..];
            ts.AppendLine($"  {name}: '{IconRegistry.Path(glyph)}',");
        }
        ts.AppendLine("};");
        ts.AppendLine();
        // The curated glyphs that point along the reading (IconGlyph.MatchTextDirection), read from
        // CuratedIcons.Resolve itself: the set is whatever the C# says, never a list kept by hand here.
        ts.AppendLine("export const iconsMatchingTextDirection: ReadonlySet<string> = new Set<string>([");
        foreach (var glyph in Enum.GetValues<Icons>())
        {
            if (!CuratedIcons.Resolve(glyph).MatchTextDirection) continue;
            var name = char.ToLowerInvariant(glyph.ToString()[0]) + glyph.ToString()[1..];
            ts.AppendLine($"  '{name}',");
        }
        ts.AppendLine("]);");
        return ts.ToString();
    }
}
