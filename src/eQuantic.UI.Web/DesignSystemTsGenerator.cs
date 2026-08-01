using System.Globalization;
using System.Reflection;
using System.Text;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// Generates the TypeScript design-system module the browser runtime ships
/// (<c>src/shared/design-system.generated.ts</c>): spacing/radius/icon/touch/motion scales, the
/// Button size table, and the full <c>PhotonTheme</c> — every value read from the C# single source
/// (reflection over the token classes, real calls into <see cref="PhotonTheme.Instance"/> and
/// <see cref="ButtonStyles.Metrics"/>). Hand-writing these values client-side is forbidden by the
/// same rule that governs the generated CSS; a parity test regenerates and byte-compares the
/// committed file (<c>EQ_UPDATE_DESIGN_TS=1</c> to refresh it).
/// </summary>
public static class DesignSystemTsGenerator
{
    public static string Generate(IAppTheme theme)
    {
        var ts = new StringBuilder();
        ts.AppendLine("/**");
        ts.AppendLine(" * GENERATED — do not edit. Every value comes from the C# design-system single source");
        ts.AppendLine(" * (eQuantic.UI.Primitives). Regenerate: EQ_UPDATE_DESIGN_TS=1 dotnet test eQuantic.UI.Web.Tests");
        ts.AppendLine(" * (DesignSystemTsGeneratorTests pins this file byte-for-byte against the generator).");
        ts.AppendLine(" */");
        ts.AppendLine();
        ts.AppendLine("import { ColorToken, TypeStyle, VariantColors } from './value-types';");
        ts.AppendLine("import type { AppTheme, ShadowSpec } from './value-types';");
        ts.AppendLine("import type { ColorValue } from './nodes';");
        ts.AppendLine();
        ts.AppendLine("const c = (r: number, g: number, b: number, a: number): ColorValue => ({ r, g, b, a });");
        ts.AppendLine("const t = (light: ColorValue, dark: ColorValue): ColorToken => new ColorToken(light, dark);");

        AppendConstScale(ts, typeof(Space), "Space");
        AppendConstScale(ts, typeof(Radius), "Radius");
        AppendConstScale(ts, typeof(IconSize), "IconSize");
        AppendConstScale(ts, typeof(Touch), "Touch");
        AppendConstScale(ts, typeof(Motion), "Motion");
        AppendCurves(ts);

        AppendButtonStyles(ts);
        AppendVariantColors(ts, theme);
        AppendTypeScale(ts, theme);
        AppendElevations(ts, theme);
        AppendShape(ts, theme);
        AppendTheme(ts, theme);

        return ts.ToString();
    }

    /// <summary>The spec §06 easing curves → the CSS-ready control-point tuples the TS twin feeds
    /// <c>cubic-bezier()</c> (the C# TokenCss.Bezier mirror).</summary>
    private static void AppendCurves(StringBuilder ts)
    {
        ts.AppendLine();
        ts.AppendLine("export const Curve = {");
        foreach (var field in typeof(Curve).GetFields(BindingFlags.Public | BindingFlags.Static)
                     .Where(f => f.FieldType == typeof(Curve)))
        {
            var curve = (Curve)field.GetValue(null)!;
            ts.AppendLine($"  {Camel(field.Name)}: [{Num(curve.X1)}, {Num(curve.Y1)}, {Num(curve.X2)}, {Num(curve.Y2)}],");
        }
        ts.AppendLine("} as const;");
    }

    /// <summary>A static token class of numeric consts (Space, Radius, …) → a const object of camelCase members.</summary>
    private static void AppendConstScale(StringBuilder ts, Type scale, string exportName)
    {
        ts.AppendLine();
        ts.AppendLine($"export const {exportName} = {{");
        foreach (var field in scale.GetFields(BindingFlags.Public | BindingFlags.Static)
                     .Where(f => f.IsLiteral))
        {
            var value = System.Convert.ToSingle(field.GetRawConstantValue(), CultureInfo.InvariantCulture);
            ts.AppendLine($"  {Camel(field.Name)}: {Num(value)},");
        }
        ts.AppendLine("} as const;");
    }

    /// <summary>The spec A12 size table, one entry per <see cref="SizeVariant"/> — emitted as the ARRAY the
    /// transpiled tuple deconstruction (`let [height, padX, …] = ButtonStyles.metrics(size)`) expects.
    /// The C# switch's `_` arm (XLarge) becomes the `default` case, preserving unknown-value behavior.</summary>
    private static void AppendButtonStyles(StringBuilder ts)
    {
        ts.AppendLine();
        ts.AppendLine("/** Height · PadX · Gap · Label · Icon · Radius · HitTarget — the spec A12 size table. */");
        ts.AppendLine("export const ButtonStyles = {");
        ts.AppendLine($"  minWidth: {Num(ButtonStyles.MinWidth)},");
        ts.AppendLine("  metrics(size: string): [number, number, number, number, number, number, number] {");
        ts.AppendLine("    switch (size) {");
        foreach (var size in Enum.GetValues<SizeVariant>())
        {
            var (height, padX, gap, label, icon, radius, hit) = ButtonStyles.Metrics(size);
            var row = $"[{Num(height)}, {Num(padX)}, {Num(gap)}, {Num(label)}, {Num(icon)}, {Num(radius)}, {Num(hit)}]";
            ts.AppendLine(size == SizeVariant.XLarge
                ? $"      default: return {row};"
                : $"      case '{Camel(size.ToString())}': return {row};");
        }
        ts.AppendLine("    }");
        ts.AppendLine("  },");
        ts.AppendLine("};");
    }

    private static void AppendVariantColors(StringBuilder ts, IAppTheme theme)
    {
        ts.AppendLine();
        ts.AppendLine("const variantColors: Record<string, VariantColors> = {");
        foreach (var variant in Enum.GetValues<Variant>())
        {
            var colors = theme.Colors(variant);
            ts.AppendLine($"  {Camel(variant.ToString())}: new VariantColors(");
            ts.AppendLine($"    {Token(colors.Base)},");
            ts.AppendLine($"    {Token(colors.OnBase)},");
            ts.AppendLine($"    {Token(colors.Pressed)},");
            ts.AppendLine($"    {Token(colors.Subtle)},");
            ts.AppendLine($"    {Token(colors.OnSubtle)},");
            ts.AppendLine("  ),");
        }
        ts.AppendLine("};");
    }

    private static void AppendTypeScale(StringBuilder ts, IAppTheme theme)
    {
        ts.AppendLine();
        ts.AppendLine("const typeScale: Record<string, TypeStyle> = {");
        foreach (var role in Enum.GetValues<TypeRole>())
        {
            var style = theme.Type(role);
            ts.AppendLine(
                $"  {Camel(role.ToString())}: new TypeStyle({Num(style.Size)}, {Num(style.LineHeight)}, " +
                $"'{Camel(style.Weight.ToString())}', {Num(style.Tracking)}, {Num(style.MaxScale)}),");
        }
        ts.AppendLine("};");
    }

    private static void AppendElevations(StringBuilder ts, IAppTheme theme)
    {
        ts.AppendLine();
        ts.AppendLine("const elevations: ShadowSpec[] = [");
        for (var level = 0; level <= 5; level++)
        {
            var spec = theme.Elevation(level);
            ts.AppendLine(
                $"  {{ offsetY: {Num(spec.OffsetY)}, blur: {Num(spec.Blur)}, spread: {Num(spec.Spread)}, " +
                $"color: {Token(spec.Color)} }},");
        }
        ts.AppendLine("];");
    }

    private static void AppendShape(StringBuilder ts, IAppTheme theme)
    {
        ts.AppendLine();
        ts.AppendLine("const shapeScale: Record<string, number> = {");
        foreach (ShapeScale scale in Enum.GetValues<ShapeScale>())
            ts.AppendLine($"  {Camel(scale.ToString())}: {Num(theme.Shape(scale))},");
        ts.AppendLine("};");
    }

    private static void AppendTheme(StringBuilder ts, IAppTheme theme)
    {
        ts.AppendLine();
        ts.AppendLine("export const photonTheme: AppTheme = {");
        // Every ColorToken property of the theme CONTRACT, in declaration order — new tokens flow
        // through automatically (and the TS AppTheme interface fails compilation until it catches up).
        foreach (var property in typeof(IAppTheme).GetProperties()
                     .Where(p => p.PropertyType == typeof(ColorToken)))
        {
            var token = (ColorToken)property.GetValue(theme)!;
            ts.AppendLine($"  {Camel(property.Name)}: {Token(token)},");
        }
        ts.AppendLine($"  disabledOpacity: {Num(theme.DisabledOpacity)},");
        ts.AppendLine("  colors(variant: string): VariantColors {");
        ts.AppendLine($"    return variantColors[variant] ?? variantColors.{Camel(nameof(Variant.Primary))};");
        ts.AppendLine("  },");
        ts.AppendLine("  type(role: string): TypeStyle {");
        ts.AppendLine($"    return typeScale[role] ?? typeScale.{Camel(nameof(TypeRole.BodyL))};");
        ts.AppendLine("  },");
        ts.AppendLine("  elevation(level: number): ShadowSpec {");
        ts.AppendLine("    return elevations[Math.max(0, Math.min(5, Math.trunc(level)))];");
        ts.AppendLine("  },");
        ts.AppendLine("  shape(scale: string): number {");
        ts.AppendLine("    return shapeScale[scale] ?? shapeScale.medium;");
        ts.AppendLine("  },");
        ts.AppendLine("};");
        ts.AppendLine();
        ts.AppendLine("/** The transpiled shape of `PhotonTheme.Instance` references. */");
        ts.AppendLine("export const PhotonTheme = { instance: photonTheme };");
    }

    private static string Token(ColorToken token) => $"t({Color(token.Light)}, {Color(token.Dark)})";

    private static string Color(Color color) => $"c({color.R}, {color.G}, {color.B}, {color.A})";

    private static string Num(float value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private static string Camel(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
