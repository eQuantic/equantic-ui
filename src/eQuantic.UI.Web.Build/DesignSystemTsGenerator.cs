using System.Globalization;
using System.Reflection;
using System.Text;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Build;

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
        ts.AppendLf("/**");
        ts.AppendLf(" * GENERATED — do not edit. Every value comes from the C# design-system single source");
        ts.AppendLf(" * (eQuantic.UI.Primitives). Regenerate: EQ_UPDATE_DESIGN_TS=1 dotnet test eQuantic.UI.Web.Tests");
        ts.AppendLf(" * (DesignSystemTsGeneratorTests pins this file byte-for-byte against the generator).");
        ts.AppendLf(" */");
        ts.Append('\n');
        ts.AppendLf("import { ColorToken, TypeStyle, VariantColors, codeTokenColor } from './value-types';");
        ts.AppendLf("import type { AppTheme, ShadowSpec } from './value-types';");
        ts.AppendLf("import type { ColorValue } from './nodes';");
        ts.AppendLf("import { DataPalette, DivergingScale, StatusScale } from './data-palette';");
        ts.Append('\n');
        ts.AppendLf("const c = (r: number, g: number, b: number, a: number): ColorValue => ({ r, g, b, a });");
        ts.AppendLf("const t = (light: ColorValue, dark: ColorValue): ColorToken => new ColorToken(light, dark);");

        AppendConstScale(ts, typeof(Space), "Space");
        AppendConstScale(ts, typeof(Radius), "Radius");
        AppendConstScale(ts, typeof(IconSize), "IconSize");
        AppendConstScale(ts, typeof(Touch), "Touch");
        AppendCurves(ts);
        AppendConstScale(ts, typeof(Motion), "Motion");

        AppendSizing(ts);
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
        ts.Append('\n');
        ts.AppendLf("export const Curve = {");
        foreach (var field in typeof(Curve).GetFields(BindingFlags.Public | BindingFlags.Static)
                     .Where(f => f.FieldType == typeof(Curve)))
        {
            var curve = (Curve)field.GetValue(null)!;
            ts.AppendLf($"  {Camel(field.Name)}: [{Num(curve.X1)}, {Num(curve.Y1)}, {Num(curve.X2)}, {Num(curve.Y2)}],");
        }
        ts.AppendLf("} as const;");
    }

    /// <summary>A static token class of numeric consts (Space, Radius, …) → a const object of camelCase members.</summary>
    private static void AppendConstScale(StringBuilder ts, Type scale, string exportName)
    {
        ts.Append('\n');
        ts.AppendLf($"export const {exportName} = {{");
        foreach (var field in scale.GetFields(BindingFlags.Public | BindingFlags.Static)
                     .Where(f => f.IsLiteral))
        {
            var value = System.Convert.ToSingle(field.GetRawConstantValue(), CultureInfo.InvariantCulture);
            ts.AppendLf($"  {Camel(field.Name)}: {Num(value)},");
        }

        // NAMED roles (Motion.Press, Motion.Enter, …) are `static readonly MotionSpec`, not consts —
        // they carry a duration AND the curve the spec pairs with it, so they emit as objects.
        foreach (var field in scale.GetFields(BindingFlags.Public | BindingFlags.Static)
                     .Where(f => f.FieldType == typeof(MotionSpec)))
        {
            var role = (MotionSpec)field.GetValue(null)!;
            ts.AppendLf($"  {Camel(field.Name)}: {{ durationMs: {Num(role.DurationMs)}, curve: Curve.{Camel(CurveName(role.Curve))} }},");
        }

        ts.AppendLf("} as const;");
    }

    /// <summary>The bezier's DECLARED name, so a role emits `Curve.standard` and not a raw tuple.</summary>
    private static string CurveName(Curve curve) =>
        typeof(Curve).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(Curve))
            .First(f => ((Curve)f.GetValue(null)!).Equals(curve)).Name;

    /// <summary>
    /// The CONTROL LADDER (spec A12) — one method per measurement, each a switch over
    /// <see cref="SizeVariant"/>, reflected so a new rung never needs a generator edit.
    /// </summary>
    private static void AppendSizing(StringBuilder ts)
    {
        ts.Append('\n');
        ts.AppendLf("/** The control ladder — every control of a given size measures the same (spec A12). */");
        ts.AppendLf("export const Sizing = {");
        foreach (var method in typeof(Sizing).GetMethods(BindingFlags.Public | BindingFlags.Static)
                     .Where(m => m.ReturnType == typeof(float)
                                 && m.GetParameters() is { Length: 1 or 2 } parameters
                                 && parameters[0].ParameterType == typeof(SizeVariant)
                                 && (parameters.Length == 1
                                     || parameters[1].ParameterType == typeof(Density)))
                     .OrderBy(m => m.Name, StringComparer.Ordinal))
        {
            // A rung may be density-aware (two parameters) or not (one) — the generator reads which
            // from the SIGNATURE, so adding density to another rung never needs an edit here.
            var dense = method.GetParameters().Length == 2;
            ts.AppendLf(dense
                ? $"  {Camel(method.Name)}(size: string, density = 'comfortable'): number {{"
                : $"  {Camel(method.Name)}(size: string): number {{");
            if (dense)
            {
                ts.AppendLf("    if (density === 'compact') {");
                AppendSizeSwitch(ts, method, Density.Compact, "      ");
                ts.AppendLf("    }");
            }
            AppendSizeSwitch(ts, method, Density.Comfortable, "    ");
            ts.AppendLf("  },");
        }

        // Rungs with NO size axis at all — a switch, a checkbox and a radio have one of each, and
        // only the density moves them. Same reflection rule: the signature says what the twin gets.
        foreach (var method in typeof(Sizing).GetMethods(BindingFlags.Public | BindingFlags.Static)
                     .Where(m => m.ReturnType == typeof(float)
                                 && m.GetParameters() is [{ ParameterType: var only }]
                                 && only == typeof(Density))
                     .OrderBy(m => m.Name, StringComparer.Ordinal))
        {
            ts.AppendLf($"  {Camel(method.Name)}(density = 'comfortable'): number {{");
            ts.AppendLf($"    return density === 'compact' "
                + $"? {Num((float)method.Invoke(null, [Density.Compact])!)} "
                + $": {Num((float)method.Invoke(null, [Density.Comfortable])!)};");
            ts.AppendLf("  },");
        }

        // …and the plain constants of the ladder.
        foreach (var field in typeof(Sizing).GetFields(BindingFlags.Public | BindingFlags.Static)
                     .Where(f => f.IsLiteral && f.FieldType == typeof(float))
                     .OrderBy(f => f.Name, StringComparer.Ordinal))
        {
            ts.AppendLf($"  {Camel(field.Name)}: {Num((float)field.GetRawConstantValue()!)},");
        }
        ts.AppendLf("};");
    }

    /// <summary>The metrics tuple for every rung, at a given density.</summary>
    private static void AppendMetricsSwitch(StringBuilder ts, Density density, string indent)
    {
        ts.AppendLf($"{indent}switch (size) {{");
        foreach (var size in Enum.GetValues<SizeVariant>())
        {
            var (height, padX, gap, label, icon, radius, hit) = ButtonStyles.Metrics(size, density);
            var row = $"[{Num(height)}, {Num(padX)}, {Num(gap)}, {Num(label)}, {Num(icon)}, "
                + $"{Num(radius)}, {Num(hit)}]";
            ts.AppendLf(size == SizeVariant.XLarge
                ? $"{indent}  default: return {row};"
                : $"{indent}  case '{Camel(size.ToString())}': return {row};");
        }
        ts.AppendLf($"{indent}}}");
    }

    /// <summary>One switch over the size rungs, at a given density.</summary>
    private static void AppendSizeSwitch(StringBuilder ts, MethodInfo method, Density density, string indent)
    {
        ts.AppendLf($"{indent}switch (size) {{");
        foreach (var size in Enum.GetValues<SizeVariant>())
        {
            object?[] args = method.GetParameters().Length == 2 ? [size, density] : [size];
            var value = Num((float)method.Invoke(null, args)!);
            ts.AppendLf(size == SizeVariant.XLarge
                ? $"{indent}  default: return {value};"
                : $"{indent}  case '{Camel(size.ToString())}': return {value};");
        }
        ts.AppendLf($"{indent}}}");
    }

    /// <summary>The spec A12 size table, one entry per <see cref="SizeVariant"/> — emitted as the ARRAY the
    /// transpiled tuple deconstruction (`let [height, padX, …] = ButtonStyles.metrics(size)`) expects.
    /// The C# switch's `_` arm (XLarge) becomes the `default` case, preserving unknown-value behavior.</summary>
    private static void AppendButtonStyles(StringBuilder ts)
    {
        ts.Append('\n');
        ts.AppendLf("/** Height · PadX · Gap · Label · Icon · Radius · HitTarget — the spec A12 size table. */");
        ts.AppendLf("export const ButtonStyles = {");
        ts.AppendLf($"  minWidth: {Num(ButtonStyles.MinWidth)},");
        ts.AppendLf("  metrics(size: string, density = 'comfortable'): "
            + "[number, number, number, number, number, number, number] {");
        // The dense branch FIRST — the comfortable switch returns, so anything after it is dead.
        ts.AppendLf("    if (density === 'compact') {");
        AppendMetricsSwitch(ts, Density.Compact, "      ");
        ts.AppendLf("    }");
        AppendMetricsSwitch(ts, Density.Comfortable, "    ");
        ts.AppendLf("  },");
        ts.AppendLf("};");
    }

    private static void AppendVariantColors(StringBuilder ts, IAppTheme theme)
    {
        ts.Append('\n');
        ts.AppendLf("const variantColors: Record<string, VariantColors> = {");
        foreach (var variant in Enum.GetValues<Variant>())
        {
            var colors = theme.Colors(variant);
            ts.AppendLf($"  {Camel(variant.ToString())}: new VariantColors(");
            ts.AppendLf($"    {Token(colors.Base)},");
            ts.AppendLf($"    {Token(colors.OnBase)},");
            ts.AppendLf($"    {Token(colors.Pressed)},");
            ts.AppendLf($"    {Token(colors.Subtle)},");
            ts.AppendLf($"    {Token(colors.OnSubtle)},");
            ts.AppendLf("  ),");
        }
        ts.AppendLf("};");
    }

    private static void AppendTypeScale(StringBuilder ts, IAppTheme theme)
    {
        ts.Append('\n');
        ts.AppendLf("const typeScale: Record<string, TypeStyle> = {");
        foreach (var role in Enum.GetValues<TypeRole>())
        {
            var style = theme.Type(role);
            ts.AppendLf(
                $"  {Camel(role.ToString())}: new TypeStyle({Num(style.Size)}, {Num(style.LineHeight)}, " +
                $"'{Camel(style.Weight.ToString())}', {Num(style.Tracking)}, {Num(style.MaxScale)}),");
        }
        ts.AppendLf("};");
    }

    private static void AppendElevations(StringBuilder ts, IAppTheme theme)
    {
        ts.Append('\n');
        ts.AppendLf("const elevations: ShadowSpec[] = [");
        for (var level = 0; level <= 5; level++)
        {
            var spec = theme.Elevation(level);
            ts.AppendLf(
                $"  {{ offsetY: {Num(spec.OffsetY)}, blur: {Num(spec.Blur)}, spread: {Num(spec.Spread)}, " +
                $"color: {Token(spec.Color)} }},");
        }
        ts.AppendLf("];");
    }

    private static void AppendShape(StringBuilder ts, IAppTheme theme)
    {
        ts.Append('\n');
        ts.AppendLf("const shapeScale: Record<string, number> = {");
        foreach (ShapeScale scale in Enum.GetValues<ShapeScale>())
            ts.AppendLf($"  {Camel(scale.ToString())}: {Num(theme.Shape(scale))},");
        ts.AppendLf("};");
    }

    private static void AppendTheme(StringBuilder ts, IAppTheme theme)
    {
        AppendDataPalette(ts, theme.Data);
        ts.Append('\n');
        ts.AppendLf("export const photonTheme: AppTheme = {");
        // Every ColorToken property of the theme CONTRACT, in declaration order — new tokens flow
        // through automatically (and the TS AppTheme interface fails compilation until it catches up).
        foreach (var property in typeof(IAppTheme).GetProperties()
                     .Where(p => p.PropertyType == typeof(ColorToken)))
        {
            var token = (ColorToken)property.GetValue(theme)!;
            ts.AppendLf($"  {Camel(property.Name)}: {Token(token)},");
        }
        ts.AppendLf("  data: defaultData,");
        ts.AppendLf($"  disabledOpacity: {Num(theme.DisabledOpacity)},");
        // The code face, when the theme names one. `IsWellFormed` rather than `Usable` on purpose:
        // this runs at CODEGEN time, where there is no frame and no run tally to record a miss into.
        // The predicate is also what makes the single quotes below safe — it admits letters, digits,
        // space and `-_.+` and nothing that means anything to a TypeScript string.
        if (theme.MonoFamily is { Length: > 0 } mono && FaceName.IsWellFormed(mono))
            ts.AppendLf($"  monoFamily: '{mono}',");
        ts.AppendLf("  colors(variant: string): VariantColors {");
        ts.AppendLf($"    return variantColors[variant] ?? variantColors.{Camel(nameof(Variant.Primary))};");
        ts.AppendLf("  },");
        ts.AppendLf("  type(role: string): TypeStyle {");
        ts.AppendLf($"    return typeScale[role] ?? typeScale.{Camel(nameof(TypeRole.BodyL))};");
        ts.AppendLf("  },");
        ts.AppendLf("  elevation(level: number): ShadowSpec {");
        ts.AppendLf("    return elevations[Math.max(0, Math.min(5, Math.trunc(level)))];");
        ts.AppendLf("  },");
        ts.AppendLf("  shape(scale: string): number {");
        ts.AppendLf("    return shapeScale[scale] ?? shapeScale.medium;");
        ts.AppendLf("  },");
        ts.AppendLf("  code(kind: string): ColorToken {");
        ts.AppendLf("    return codeTokenColor(this, kind);");
        ts.AppendLf("  },");
        ts.AppendLf("};");
        ts.Append('\n');
        ts.AppendLf("/** The transpiled shape of `PhotonTheme.Instance` references. */");
        ts.AppendLf("export const PhotonTheme = { instance: photonTheme };");
    }

    /// <summary>The data palette (C# <c>IAppTheme.Data</c>), emitted from the values the theme holds and
    /// assigned to <c>DataPalette.default</c> here — this module is the one that knows the values, so no
    /// hex is ever written twice.</summary>
    private static void AppendDataPalette(StringBuilder ts, DataPalette data)
    {
        ts.Append('\n');
        ts.AppendLf("// The data palette: eight series slots in a FIXED order, the sequential ramp, the diverging");
        ts.AppendLf("// pair, the de-emphasis gray and the four status steps (C# `DataPalette.Default`).");
        ts.AppendLf("const defaultData = new DataPalette(");
        AppendTokenList(ts, data.Series);
        AppendTokenList(ts, data.Sequential);
        ts.AppendLf($"  new DivergingScale({Token(data.Diverging.Negative)}, {Token(data.Diverging.Midpoint)}, {Token(data.Diverging.Positive)}),");
        ts.AppendLf($"  {Token(data.Other)},");
        ts.AppendLf("  new StatusScale(");
        ts.AppendLf($"    {Token(data.Status.Good)},");
        ts.AppendLf($"    {Token(data.Status.Warning)},");
        ts.AppendLf($"    {Token(data.Status.Serious)},");
        ts.AppendLf($"    {Token(data.Status.Critical)},");
        ts.AppendLf("  ),");
        ts.AppendLf(");");
        ts.AppendLf("DataPalette.default = defaultData;");
    }

    private static void AppendTokenList(StringBuilder ts, IReadOnlyList<ColorToken> tokens)
    {
        ts.AppendLf("  [");
        foreach (var token in tokens) ts.AppendLf($"    {Token(token)},");
        ts.AppendLf("  ],");
    }

    private static string Token(ColorToken token) => $"t({Color(token.Light)}, {Color(token.Dark)})";

    private static string Color(Color color) => $"c({color.R}, {color.G}, {color.B}, {color.A})";

    private static string Num(float value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private static string Camel(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
