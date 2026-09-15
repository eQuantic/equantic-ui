using System.Text;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Build;

/// <summary>Generates <c>icons.generated.ts</c> from <see cref="IconRegistry"/> — the client never
/// hand-writes glyph path data (the CSS/theme generation rule applied to icons).</summary>
public static class IconTsGenerator
{
    public static string Generate()
    {
        var ts = new StringBuilder();
        ts.AppendLf("/**");
        ts.AppendLf(" * GENERATED — do not edit. Glyph path data comes from the C# IconRegistry single source.");
        ts.AppendLf(" * Regenerate: EQ_UPDATE_ICONS_TS=1 dotnet test eQuantic.UI.Web.Tests (IconTsGeneratorTests).");
        ts.AppendLf(" */");
        ts.Append('\n');
        ts.AppendLf("export const iconPaths: Record<string, string> = {");
        foreach (var glyph in Enum.GetValues<Icons>())
        {
            var name = char.ToLowerInvariant(glyph.ToString()[0]) + glyph.ToString()[1..];
            ts.AppendLf($"  {name}: '{IconRegistry.Path(glyph)}',");
        }
        ts.AppendLf("};");
        return ts.ToString();
    }
}
