using System.Globalization;
using System.Text;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// Serializes any <see cref="IAppTheme"/> to the compact JSON the browser runtime rehydrates into an
/// <c>AppTheme</c> (see <c>materializeTheme</c> in <c>theme-bridge.ts</c>) — the SSR→client THEME
/// BRIDGE. The server emits this blob into <c>window.__EQ_THEME__</c> next to <c>__EQ_CONFIG</c>, and
/// boot calls <c>setPhotonTheme</c> with it before hydration, so client re-renders resolve the SAME
/// colors/shape the SSR markup baked in (both sides read resolved values, not CSS vars — see
/// <see cref="TokenCss.Value"/>). The wire shape mirrors the <c>DesignSystemTsGenerator</c>'s
/// <c>photonTheme</c> exactly; the round-trip is cross-pinned in vitest against <c>photonTheme</c>.
/// </summary>
public static class ThemeBridge
{
    /// <summary>The theme as a single-line JSON object: surfaces, disabledOpacity, per-variant color
    /// groups, the type scale, elevations 0–5 and the shape scale — colors as <c>[[r,g,b,a],[r,g,b,a]]</c>
    /// light/dark tuples, keys camelCased to match the enum-lowered names the client looks up.</summary>
    public static string SerializeJson(IAppTheme theme)
    {
        var sb = new StringBuilder();
        sb.Append('{');

        sb.Append("\"surfaces\":{");
        var first = true;
        foreach (var property in typeof(IAppTheme).GetProperties()
                     .Where(p => p.PropertyType == typeof(ColorToken)))
        {
            Comma(sb, ref first);
            Key(sb, Camel(property.Name));
            Token(sb, (ColorToken)property.GetValue(theme)!);
        }
        sb.Append('}');

        sb.Append(",\"disabledOpacity\":").Append(Num(theme.DisabledOpacity));

        sb.Append(",\"variants\":{");
        first = true;
        foreach (var variant in Enum.GetValues<Variant>())
        {
            Comma(sb, ref first);
            Key(sb, Camel(variant.ToString()));
            var c = theme.Colors(variant);
            sb.Append('[');
            Token(sb, c.Base); sb.Append(',');
            Token(sb, c.OnBase); sb.Append(',');
            Token(sb, c.Pressed); sb.Append(',');
            Token(sb, c.Subtle); sb.Append(',');
            Token(sb, c.OnSubtle);
            sb.Append(']');
        }
        sb.Append('}');

        sb.Append(",\"type\":{");
        first = true;
        foreach (var role in Enum.GetValues<TypeRole>())
        {
            Comma(sb, ref first);
            Key(sb, Camel(role.ToString()));
            var s = theme.Type(role);
            sb.Append('[').Append(Num(s.Size)).Append(',').Append(Num(s.LineHeight))
              .Append(",\"").Append(Camel(s.Weight.ToString())).Append("\",")
              .Append(Num(s.Tracking)).Append(',').Append(Num(s.MaxScale)).Append(']');
        }
        sb.Append('}');

        sb.Append(",\"elevations\":[");
        for (var level = 0; level <= 5; level++)
        {
            if (level > 0) sb.Append(',');
            var e = theme.Elevation(level);
            sb.Append('[').Append(Num(e.OffsetY)).Append(',').Append(Num(e.Blur)).Append(',')
              .Append(Num(e.Spread)).Append(',');
            Token(sb, e.Color);
            sb.Append(']');
        }
        sb.Append(']');

        sb.Append(",\"shape\":{");
        first = true;
        foreach (var scale in Enum.GetValues<ShapeScale>())
        {
            Comma(sb, ref first);
            Key(sb, Camel(scale.ToString()));
            sb.Append(Num(theme.Shape(scale)));
        }
        sb.Append('}');

        sb.Append('}');
        return sb.ToString();
    }

    private static void Token(StringBuilder sb, ColorToken token)
    {
        sb.Append('[');
        Color(sb, token.Light);
        sb.Append(',');
        Color(sb, token.Dark);
        sb.Append(']');
    }

    private static void Color(StringBuilder sb, Color c) => sb
        .Append('[').Append(c.R).Append(',').Append(c.G).Append(',').Append(c.B).Append(',').Append(c.A).Append(']');

    private static void Key(StringBuilder sb, string name) => sb.Append('"').Append(name).Append("\":");

    private static void Comma(StringBuilder sb, ref bool first)
    {
        if (!first) sb.Append(',');
        first = false;
    }

    private static string Num(float value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private static string Camel(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
