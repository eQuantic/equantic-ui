using System.Globalization;
using System.Text.Json;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The design system and the implementation, compared. Every number in
/// <c>docs/design/tokens.json</c> is read and checked against the value the SDK actually returns.
///
/// <para>
/// The handoff used to live in Claude Design and reach this repo as an audit DOCUMENT, so the two
/// drifted in both directions at once and nothing could say which side was right — a token export
/// nobody compared, and rows that go stale the moment either side moves. Two of those rows were
/// already stale when this was written, and a third had me about to "correct" five files in the
/// wrong direction.
/// </para>
///
/// <para>
/// So the alignment is a TEST. A correction flows either way — the code moves when the design is
/// right, the token file moves when the implementation is — but it cannot be FORGOTTEN, and it
/// cannot be half-done: this fails with the name of every token that differs, not with the first.
/// </para>
///
/// <para>
/// <see cref="DesignTokenTests"/> is the sibling and stays: it recomputes the WCAG claims rather
/// than comparing values, which is the half no token file can check itself.
/// </para>
/// </summary>
public class HandoffTokenPinTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;
    private static readonly JsonElement Handoff = Load();

    private static JsonElement Load()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !File.Exists(Path.Combine(here.FullName, "docs", "design", "tokens.json")))
            here = here.Parent;

        here.Should().NotBeNull("the token export is the subject of this suite — a missing file is a "
            + "failure, never an empty pass");
        return JsonDocument.Parse(
            File.ReadAllText(Path.Combine(here!.FullName, "docs", "design", "tokens.json"))).RootElement;
    }

    // ---- reading the two sides in the same units ------------------------------------------------

    /// <summary>
    /// A handoff colour, as bytes. It writes <c>#RRGGBB</c> and <c>rgba(r,g,b,a)</c> with a decimal
    /// alpha, and the SDK holds bytes — so both are parsed rather than formatted and string-compared,
    /// which would fail on <c>0.55</c> against <c>140/255</c> for no reason.
    /// </summary>
    private static (int R, int G, int B, int A) Parse(string value)
    {
        var text = value.Trim();
        if (text.StartsWith('#'))
            return (Convert.ToInt32(text[1..3], 16), Convert.ToInt32(text[3..5], 16),
                Convert.ToInt32(text[5..7], 16), 255);

        var parts = text[(text.IndexOf('(') + 1)..text.IndexOf(')')].Split(',');
        var alpha = parts.Length > 3
            ? (int)Math.Round(double.Parse(parts[3], CultureInfo.InvariantCulture) * 255)
            : 255;
        return (int.Parse(parts[0].Trim()), int.Parse(parts[1].Trim()), int.Parse(parts[2].Trim()), alpha);
    }

    /// <summary>The alpha byte and a decimal alpha never round-trip exactly; one step apart is the
    /// same colour, two is a different one.</summary>
    private static bool Same(Color sdk, string handoff)
    {
        var (r, g, b, a) = Parse(handoff);
        return sdk.R == r && sdk.G == g && sdk.B == b && Math.Abs(sdk.A - a) <= 1;
    }

    private static float Number(JsonElement e) => e.ValueKind == JsonValueKind.String
        ? float.Parse(e.GetString()!.Replace("px", "").Replace("ms", "").Trim(), CultureInfo.InvariantCulture)
        : e.GetSingle();

    private readonly List<string> _differed = [];
    private int _compared;

    private void Colour(string name, ColorToken token, JsonElement pair)
    {
        foreach (var (mode, colour) in new[] { ("light", token.Light), ("dark", token.Dark) })
        {
            if (!pair.TryGetProperty(mode, out var want)) continue;
            _compared++;
            if (!Same(colour, want.GetString()!))
                _differed.Add($"{name}.{mode}: handoff {want.GetString()} · sdk "
                    + $"rgba({colour.R},{colour.G},{colour.B},{colour.A})");
        }
    }

    private void Value(string name, float sdk, JsonElement want)
    {
        _compared++;
        var expected = Number(want);
        if (Math.Abs(expected - sdk) > 0.0001f)
            _differed.Add($"{name}: handoff {expected} · sdk {sdk}");
    }

    private void Settle(int atLeast)
    {
        _compared.Should().BeGreaterThanOrEqualTo(atLeast,
            "a pin that compared nothing passes for the wrong reason — this asserts it did the work");
        // The whole list, not "at least one item". A pin that names one divergence at a time turns a
        // ten-minute reconciliation into ten runs, and the point of comparing a machine-readable
        // export is that it can say everything it knows in one go.
        _differed.Should().BeEmpty(
            $"the design system and the implementation must agree — move whichever side is wrong:"
            + $"{Environment.NewLine}  {string.Join(Environment.NewLine + "  ", _differed)}"
            + $"{Environment.NewLine}");
    }

    // ---- colour ----------------------------------------------------------------------------------

    [Fact]
    public void Surfaces()
    {
        var surface = Handoff.GetProperty("color").GetProperty("surface");
        foreach (var (name, token) in new (string, ColorToken)[] {
            ("background", Theme.Background), ("surface", Theme.Surface),
            ("surfaceSubtle", Theme.SurfaceSubtle), ("surfaceHighlight", Theme.SurfaceHighlight),
            ("border", Theme.Border), ("borderStrong", Theme.BorderStrong) })
            Colour($"surface.{name}", token, surface.GetProperty(name));

        Settle(12);
    }

    [Fact]
    public void TextTiers()
    {
        var text = Handoff.GetProperty("color").GetProperty("text");
        foreach (var (name, token) in new (string, ColorToken)[] {
            ("primary", Theme.TextPrimary), ("secondary", Theme.TextSecondary),
            ("muted", Theme.TextMuted), ("inverse", Theme.TextInverse) })
            Colour($"text.{name}", token, text.GetProperty(name));

        Settle(8);
    }

    [Fact]
    public void Utility()
    {
        var utility = Handoff.GetProperty("color").GetProperty("utility");
        foreach (var (name, token) in new (string, ColorToken)[] {
            ("focusRing", Theme.FocusRing), ("linkColor", Theme.LinkColor), ("scrim", Theme.Scrim) })
            Colour($"utility.{name}", token, utility.GetProperty(name));

        Settle(6);
    }

    /// <summary>
    /// The five-tuple per variant. Outline and Ghost carry a <c>derivesFrom</c> sentence instead of
    /// values and are checked by the rule they state, not by transcription; Link lists only the two
    /// slots it uses, which the handoff's own colour rules call the audited exception.
    /// </summary>
    [Fact]
    public void Variants()
    {
        var variants = Handoff.GetProperty("color").GetProperty("variant");
        foreach (var variant in Enum.GetValues<Variant>())
        {
            var name = char.ToLowerInvariant(variant.ToString()[0]) + variant.ToString()[1..];
            if (!variants.TryGetProperty(name, out var want)) continue;

            var colors = Theme.Colors(variant);
            foreach (var (slot, token) in new (string, ColorToken)[] {
                ("base", colors.Base), ("onBase", colors.OnBase), ("pressed", colors.Pressed),
                ("subtle", colors.Subtle), ("onSubtle", colors.OnSubtle) })
                if (want.TryGetProperty(slot, out var pair) && pair.ValueKind == JsonValueKind.Object)
                    Colour($"variant.{name}.{slot}", token, pair);
        }

        Settle(40);
    }

    // ---- scales ----------------------------------------------------------------------------------

    [Fact]
    public void Space()
    {
        var space = Handoff.GetProperty("space");
        foreach (var (name, value) in new (string, float)[] {
            ("s1", Primitives.Space.S1), ("s2", Primitives.Space.S2), ("s3", Primitives.Space.S3),
            ("s4", Primitives.Space.S4), ("s5", Primitives.Space.S5), ("s6", Primitives.Space.S6),
            ("s8", Primitives.Space.S8), ("s10", Primitives.Space.S10),
            ("s12", Primitives.Space.S12), ("s16", Primitives.Space.S16) })
            if (space.TryGetProperty(name, out var want) && want.ValueKind != JsonValueKind.String)
                Value($"space.{name}", value, want);

        Settle(8);
    }

    [Fact]
    public void RadiusAndShape()
    {
        var radius = Handoff.GetProperty("radius");
        foreach (var (name, value) in new (string, float)[] {
            ("xs", Primitives.Radius.Xs), ("sm", Primitives.Radius.Sm), ("md", Primitives.Radius.Md),
            ("lg", Primitives.Radius.Lg), ("xl", Primitives.Radius.Xl), ("full", Primitives.Radius.Full) })
            if (radius.TryGetProperty(name, out var want) && want.ValueKind == JsonValueKind.Number)
                Value($"radius.{name}", value, want);

        var shape = Handoff.GetProperty("shapeScale");
        foreach (var scale in Enum.GetValues<ShapeScale>())
        {
            var name = char.ToLowerInvariant(scale.ToString()[0]) + scale.ToString()[1..];
            if (shape.TryGetProperty(name, out var want) && want.ValueKind == JsonValueKind.Number)
                Value($"shapeScale.{name}", Theme.Shape(scale), want);
        }

        Settle(10);
    }

    [Fact]
    public void TypeRoles()
    {
        var roles = Handoff.GetProperty("typography").GetProperty("roles");
        foreach (var role in Enum.GetValues<TypeRole>())
        {
            var name = char.ToLowerInvariant(role.ToString()[0]) + role.ToString()[1..];
            roles.TryGetProperty(name, out var want).Should().BeTrue(
                $"the handoff names every role the SDK has, and {role} is one of them");

            var style = Theme.Type(role);
            Value($"role.{name}.size", style.Size, want.GetProperty("size"));
            Value($"role.{name}.lineHeight", style.LineHeight, want.GetProperty("lineHeight"));
            Value($"role.{name}.weight", (int)style.Weight, want.GetProperty("weight"));
            Value($"role.{name}.tracking", style.Tracking, want.GetProperty("tracking"));
            Value($"role.{name}.maxScale", style.MaxScale, want.GetProperty("maxScale"));
        }

        Settle(50);
    }

    [Fact]
    public void IconsTouchAndOpacity()
    {
        var icon = Handoff.GetProperty("icon");
        foreach (var (name, value) in new (string, float)[] {
            ("sm", IconSize.Sm), ("dense", IconSize.Dense), ("md", IconSize.Md), ("lg", IconSize.Lg) })
            if (icon.TryGetProperty(name, out var want) && want.ValueKind == JsonValueKind.Number)
                Value($"icon.{name}", value, want);

        Value("touch.minTarget", Touch.MinTarget, Handoff.GetProperty("touch").GetProperty("minTarget"));
        Value("touch.pressCancelSlop", Touch.PressCancelSlop,
            Handoff.GetProperty("touch").GetProperty("pressCancelSlop"));
        Value("disabledOpacity", Theme.DisabledOpacity, Handoff.GetProperty("disabledOpacity"));

        Settle(7);
    }

    [Fact]
    public void Elevation()
    {
        var levels = Handoff.GetProperty("elevation").GetProperty("levels");
        foreach (var want in levels.EnumerateArray())
        {
            var level = want.GetProperty("level").GetInt32();
            var spec = Theme.Elevation(level);
            Value($"elevation[{level}].offsetY", spec.OffsetY, want.GetProperty("offsetY"));
            Value($"elevation[{level}].blur", spec.Blur, want.GetProperty("blur"));
            Value($"elevation[{level}].spread", spec.Spread, want.GetProperty("spread"));
        }

        Settle(18);
    }

    [Fact]
    public void Motion()
    {
        var duration = Handoff.GetProperty("motion").GetProperty("duration");
        Value("motion.fast", Primitives.Motion.FastMs, duration.GetProperty("fast"));
        Value("motion.base", Primitives.Motion.BaseMs, duration.GetProperty("base"));
        Value("motion.slow", Primitives.Motion.SlowMs, duration.GetProperty("slow"));

        Settle(3);
    }

    /// <summary>
    /// Both densities. Compact is where the handoff and the implementation most easily part company,
    /// because it is the column a transcription skips.
    /// </summary>
    [Fact]
    public void ControlMetrics()
    {
        var metrics = Handoff.GetProperty("controlMetrics");
        foreach (var size in Enum.GetValues<SizeVariant>())
        {
            var name = size.ToString().ToLowerInvariant();
            if (!metrics.TryGetProperty(name, out var want)) continue;

            Read(want, size, Density.Comfortable, name);
            if (want.TryGetProperty("compact", out var compact))
                Read(compact, size, Density.Compact, $"{name}.compact");
        }

        Settle(20);

        void Read(JsonElement want, SizeVariant size, Density density, string label)
        {
            foreach (var (key, value) in new (string, float)[] {
                ("height", Sizing.Height(size, density)), ("padX", Sizing.PaddingX(size, density)),
                ("gap", Sizing.Gap(size)), ("labelSize", Sizing.LabelSize(size, density)),
                ("iconSize", Sizing.Icon(size)), ("radius", Sizing.Radius(size)),
                ("hit", Sizing.HitTarget(size, density)) })
                if (want.TryGetProperty(key, out var expected) && expected.ValueKind == JsonValueKind.Number)
                    Value($"controlMetrics.{label}.{key}", value, expected);
        }
    }

    // ---- shape of the vocabulary itself -----------------------------------------------------------

    /// <summary>
    /// The enums, by ORDINAL. An attribute crosses assemblies by name and an enum value crosses the
    /// wire by number, so a reordering that reads as cosmetic is a different colour on the client.
    /// </summary>
    [Fact]
    public void EnumOrdinals()
    {
        var variants = Handoff.GetProperty("variants").GetProperty("values");
        foreach (var property in variants.EnumerateObject())
        {
            Enum.TryParse<Variant>(property.Name, out var variant).Should().BeTrue(
                $"the handoff names Variant.{property.Name}");
            Value($"Variant.{property.Name}", (int)variant, property.Value);
        }

        var sizes = Handoff.GetProperty("sizeVariants").GetProperty("values");
        var index = 0;
        foreach (var name in sizes.EnumerateArray())
        {
            Enum.TryParse<SizeVariant>(name.GetString(), out var size).Should().BeTrue(
                $"the handoff names SizeVariant.{name.GetString()}");
            _compared++;
            if ((int)size != index)
                _differed.Add($"SizeVariant.{name.GetString()}: handoff {index} · sdk {(int)size}");
            index++;
        }

        Settle(14);
    }
}
