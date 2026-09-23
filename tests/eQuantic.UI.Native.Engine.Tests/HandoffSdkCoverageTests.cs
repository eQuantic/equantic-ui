using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The token pin's other direction, for what the SDK ADDS.
///
/// <para>
/// <see cref="HandoffTokenPinTests"/> compares every number <c>docs/design/tokens.json</c>
/// publishes, and fails when a key it asks about disappears. It can only ask about the keys it
/// lists, though. A rung added to <see cref="Sizing"/> or a constant added to <see cref="Touch"/>
/// ships, the pages quote it, and nothing holds it: the switch, checkbox, radio and avatar ladders
/// and the wheel line all lived like that, in the SDK and on the pages but never in the export.
/// </para>
///
/// <para>
/// So this reflects over the token classes and requires every public member to be PUBLISHED, at a
/// tokens.json path some test compares, or EXEMPT, with the reason it is not a design value. A new
/// member fails here until someone makes that call, which is the decision the handoff exists to
/// record. The values the older pin does not read (selection, avatar, wheel line, gutter and the
/// curves) are compared below, so every published path has a comparison behind it.
/// </para>
/// </summary>
public class HandoffSdkCoverageTests
{
    private static readonly string Root = FindRoot();

    private static readonly JsonElement Handoff = JsonDocument.Parse(
        File.ReadAllText(Path.Combine(Root, "docs", "design", "tokens.json"))).RootElement;

    private static string FindRoot()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !File.Exists(Path.Combine(here.FullName, "docs", "design", "tokens.json")))
            here = here.Parent;

        here.Should().NotBeNull("the token export is the subject of this suite, so a missing file is a "
            + "failure and never an empty pass");
        return here!.FullName;
    }

    /// <summary>
    /// Where each public member is published. A member that takes a <see cref="SizeVariant"/> or a
    /// <see cref="Density"/> names one rung here, and the value tests read every rung.
    /// </summary>
    private static readonly Dictionary<string, string> Published = new(StringComparer.Ordinal)
    {
        ["Space.S1"] = "space.s1", ["Space.S2"] = "space.s2", ["Space.S3"] = "space.s3",
        ["Space.S4"] = "space.s4", ["Space.S5"] = "space.s5", ["Space.S6"] = "space.s6",
        ["Space.S8"] = "space.s8", ["Space.S10"] = "space.s10", ["Space.S12"] = "space.s12",
        ["Space.S16"] = "space.s16", ["Space.Gutter"] = "space.gutter",

        ["Radius.Xs"] = "radius.xs", ["Radius.Sm"] = "radius.sm", ["Radius.Md"] = "radius.md",
        ["Radius.Lg"] = "radius.lg", ["Radius.Xl"] = "radius.xl", ["Radius.Full"] = "radius.full",

        ["IconSize.Sm"] = "icon.sm", ["IconSize.Dense"] = "icon.dense", ["IconSize.Md"] = "icon.md",
        ["IconSize.Lg"] = "icon.lg",

        ["Sizing.Height"] = "controlMetrics.medium.height",
        ["Sizing.PaddingX"] = "controlMetrics.medium.padX",
        ["Sizing.Gap"] = "controlMetrics.medium.gap",
        ["Sizing.LabelSize"] = "controlMetrics.medium.labelSize",
        ["Sizing.Icon"] = "controlMetrics.medium.iconSize",
        ["Sizing.Radius"] = "controlMetrics.medium.radius",
        ["Sizing.HitTarget"] = "controlMetrics.medium.hit",
        ["Sizing.AvatarInitials"] = "controlMetrics.medium.avatarInitials",
        ["Sizing.ButtonMinWidth"] = "controlMetrics.buttonMinWidth",
        ["Sizing.Avatar"] = "avatar.medium",
        ["Sizing.SwitchWidth"] = "selection.comfortable.switchWidth",
        ["Sizing.SwitchHeight"] = "selection.comfortable.switchHeight",
        ["Sizing.SwitchThumb"] = "selection.comfortable.switchThumb",
        ["Sizing.SwitchTravel"] = "selection.comfortable.switchTravel",
        ["Sizing.SwitchInset"] = "selection.switchInset",
        ["Sizing.SelectionBox"] = "selection.comfortable.selectionBox",
        ["Sizing.RadioDot"] = "selection.comfortable.radioDot",

        ["Touch.MinTarget"] = "touch.minTarget",
        ["Touch.PressCancelSlop"] = "touch.pressCancelSlop",
        ["Touch.WheelLine"] = "touch.wheelLine",

        ["Motion.FastMs"] = "motion.duration.fast",
        ["Motion.BaseMs"] = "motion.duration.base",
        ["Motion.SlowMs"] = "motion.duration.slow",
        ["Motion.ReducedCrossfadeMs"] = "motion.reducedMotion.ms",
        ["Motion.Press"] = "motion.roles.press.ms",
        ["Motion.State"] = "motion.roles.state.ms",
        ["Motion.Enter"] = "motion.roles.enter.ms",
        ["Motion.Exit"] = "motion.roles.exit.ms",

        ["Curve.Standard"] = "motion.curves.standard",
        ["Curve.Decelerate"] = "motion.curves.decelerate",
        ["Curve.Accelerate"] = "motion.curves.accelerate",

        ["SpringSpec.Default"] = "motion.spring.default.stiffness",

        ["WindowSizeClasses.MediumMinDp"] = "window.mediumMinDp",
        ["WindowSizeClasses.ExpandedMinDp"] = "window.expandedMinDp",
    };

    /// <summary>Public members that are not design values, each with the reason.</summary>
    private static readonly Dictionary<string, string> Exempt = new(StringComparer.Ordinal)
    {
        ["Touch.WheelTravel"] = "a rule applied to an input event (delta, precise), not a value. It reads "
            + "Touch.WheelLine, which is published",
        ["Motion.ExitFor"] = "the exit RULE (enter × 2 / 3, integer math), stated as prose under "
            + "motion.exitFor. Motion.Exit, the value it produces, is published",
        ["WindowSizeClasses.FromWidth"] = "the rule that reads the two thresholds, which are published",
    };

    private static readonly Type[] TokenTypes =
    [
        typeof(Primitives.Space), typeof(Primitives.Radius), typeof(IconSize), typeof(Sizing),
        typeof(Touch), typeof(Primitives.Motion), typeof(Primitives.Curve), typeof(SpringSpec),
        typeof(WindowSizeClasses),
    ];

    /// <summary>The types <c>Tokens.cs</c> declares that hold no values of their own: the enum that
    /// picks a rung, and the shapes a token is made of.</summary>
    private static readonly Dictionary<string, string> NotTokenTypes = new(StringComparer.Ordinal)
    {
        ["Density"] = "the enum that selects a rung; every rung it selects is published through Sizing",
        ["ShadowSpec"] = "the shape of an elevation's shadow; the values are elevation.*, which "
            + "HandoffTokenPinTests compares",
        ["MotionSpec"] = "the shape of a motion role; the roles are Motion's, which are published",
    };

    /// <summary>
    /// The scanned set above is a list, and a list misses what nobody added to it. So the source
    /// that DEFINES the tokens is read: every type <c>Tokens.cs</c> declares is scanned or named
    /// here with its reason, and a token class added there fails until someone decides which.
    /// </summary>
    [Fact]
    public void EveryTypeTokensCsDeclaresIsScannedOrNamed()
    {
        var source = File.ReadAllText(Path.Combine(Root, "src", "eQuantic.UI.Primitives", "Theme", "Tokens.cs"));
        var declared = Regex.Matches(source,
                @"^public (?:(?:static|readonly|sealed|partial|abstract|record|class|struct|enum|interface) )+(\w+)",
                RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
        declared.Should().Contain("Sizing", "the scan must reach the file it exists for");

        var scanned = TokenTypes.Select(type => type.Name).ToHashSet(StringComparer.Ordinal);
        declared.Where(name => !scanned.Contains(name) && !NotTokenTypes.ContainsKey(name))
            .Order(StringComparer.Ordinal).Should().BeEmpty(
                "Tokens.cs declares these, and this suite neither scans them nor says why not. Add each "
                + "to TokenTypes, or to NotTokenTypes with the reason");
        NotTokenTypes.Keys.Where(name => !declared.Contains(name)).Should().BeEmpty(
            "an entry names a type Tokens.cs no longer declares");
    }

    /// <summary>Fields, properties and ordinary methods; operators and accessors are not tokens.</summary>
    private static IEnumerable<string> PublicMembers(Type type) =>
        type.GetMembers(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(member => member switch
            {
                FieldInfo => true,
                PropertyInfo => true,
                MethodInfo method => !method.IsSpecialName,
                _ => false,
            })
            .Select(member => $"{type.Name}.{member.Name}")
            .Distinct(StringComparer.Ordinal);

    [Fact]
    public void EveryPublicTokenIsPublishedOrExempt()
    {
        var members = TokenTypes.SelectMany(PublicMembers).ToHashSet(StringComparer.Ordinal);

        var unaccounted = members.Where(m => !Published.ContainsKey(m) && !Exempt.ContainsKey(m))
            .OrderBy(m => m, StringComparer.Ordinal).ToArray();
        unaccounted.Should().BeEmpty(
            "the SDK has these and the handoff neither publishes nor exempts them. Publish each one in "
            + "docs/design/tokens.json and compare it, or exempt it here with the reason:" + List(unaccounted));

        // The map cannot outlive the SDK either: an entry for a member that is gone is a claim this
        // file makes about nothing.
        var gone = Published.Keys.Concat(Exempt.Keys).Where(k => !members.Contains(k))
            .OrderBy(k => k, StringComparer.Ordinal).ToArray();
        gone.Should().BeEmpty("these entries name SDK members that no longer exist:" + List(gone));

        var unresolved = Published.Where(p => !Resolves(p.Value))
            .Select(p => $"{p.Key} → {p.Value}").ToArray();
        unresolved.Should().BeEmpty("a published member has to name a path tokens.json has:" + List(unresolved));
    }

    // ---- the values the older pin does not read -------------------------------------------------

    [Fact]
    public void SelectionLadder()
    {
        Compare("selection.switchInset", Sizing.SwitchInset);
        foreach (var density in Enum.GetValues<Density>())
        {
            var rung = $"selection.{Camel(density.ToString())}";
            Compare($"{rung}.switchWidth", Sizing.SwitchWidth(density));
            Compare($"{rung}.switchHeight", Sizing.SwitchHeight(density));
            Compare($"{rung}.switchThumb", Sizing.SwitchThumb(density));
            Compare($"{rung}.switchTravel", Sizing.SwitchTravel(density));
            Compare($"{rung}.selectionBox", Sizing.SelectionBox(density));
            Compare($"{rung}.radioDot", Sizing.RadioDot(density));
        }

        Settle("selection");
    }

    [Fact]
    public void AvatarLadder()
    {
        foreach (var size in Enum.GetValues<SizeVariant>())
            Compare($"avatar.{size.ToString().ToLowerInvariant()}", Sizing.Avatar(size));

        Settle("avatar");
    }

    /// <summary>Two numbers in sections the older pin settles with its own rules, compared here
    /// one by one: `gutter` is listed there as prose, and `touch` is never settled.</summary>
    [Fact]
    public void WheelLineAndGutter()
    {
        Compare("touch.wheelLine", Touch.WheelLine);
        Compare("space.gutter", Primitives.Space.Gutter);

        _differed.Should().BeEmpty("the design system and the implementation must agree:" + List(_differed));
    }

    /// <summary>The window size classes (Foundations §11): both thresholds and every ordinal, which
    /// cross the wire by number.</summary>
    [Fact]
    public void WindowClasses()
    {
        Compare("window.mediumMinDp", WindowSizeClasses.MediumMinDp);
        Compare("window.expandedMinDp", WindowSizeClasses.ExpandedMinDp);
        foreach (var sizeClass in Enum.GetValues<WindowSizeClass>())
            Compare($"window.values.{sizeClass}", (int)sizeClass);

        Settle("window");
    }

    /// <summary>
    /// The curves are STRINGS in the export, and the schema walk only reads numbers, so a change to a
    /// control point, or a role moving to another curve, passed every other test.
    /// </summary>
    [Fact]
    public void CurvesAndTheCurveEachRoleUses()
    {
        var named = new Dictionary<string, Primitives.Curve>(StringComparer.Ordinal)
        {
            ["standard"] = Primitives.Curve.Standard,
            ["decelerate"] = Primitives.Curve.Decelerate,
            ["accelerate"] = Primitives.Curve.Accelerate,
        };

        var curves = Resolve("motion.curves");
        if (curves is not { ValueKind: JsonValueKind.Object } published)
        {
            _differed.Add("motion.curves: not published");
        }
        else
        {
            foreach (var property in published.EnumerateObject().Where(p => p.Name != "note"))
            {
                if (!named.TryGetValue(property.Name, out var sdk))
                {
                    _differed.Add($"motion.curves.{property.Name}: published, and the SDK has no such curve");
                    continue;
                }

                var points = Points(property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null);
                if (points.Length != 4
                    || Math.Abs(points[0] - sdk.X1) > 0.0001f || Math.Abs(points[1] - sdk.Y1) > 0.0001f
                    || Math.Abs(points[2] - sdk.X2) > 0.0001f || Math.Abs(points[3] - sdk.Y2) > 0.0001f)
                    _differed.Add($"motion.curves.{property.Name}: handoff {property.Value} · sdk {Describe(sdk)}");
            }

            foreach (var name in named.Keys.Where(n => !published.TryGetProperty(n, out _)))
                _differed.Add($"motion.curves.{name}: the SDK has it and the handoff does not publish it");
        }

        foreach (var (role, spec) in new (string, MotionSpec)[] {
            ("press", Primitives.Motion.Press), ("state", Primitives.Motion.State),
            ("enter", Primitives.Motion.Enter), ("exit", Primitives.Motion.Exit) })
        {
            var want = Resolve($"motion.roles.{role}.curve") is { ValueKind: JsonValueKind.String } text
                ? text.GetString()
                : null;
            if (want is null || !named.TryGetValue(want, out var curve))
                _differed.Add($"motion.roles.{role}.curve: \"{want}\" names no published curve");
            else if (curve != spec.Curve)
                _differed.Add($"motion.roles.{role}.curve: handoff {want} · sdk {Describe(spec.Curve)}");
        }

        _differed.Should().BeEmpty("the design system and the implementation must agree:" + List(_differed));
    }

    // ---- reading the export ---------------------------------------------------------------------

    private readonly List<string> _differed = [];
    private readonly HashSet<string> _compared = new(StringComparer.Ordinal);

    private void Compare(string path, float sdk)
    {
        _compared.Add(path);
        if (!TryNumber(path, out var want))
            _differed.Add($"{path}: not published · sdk {sdk.ToString(CultureInfo.InvariantCulture)}");
        else if (Math.Abs(want - sdk) > 0.0001f)
            _differed.Add($"{path}: handoff {want.ToString(CultureInfo.InvariantCulture)} · sdk {sdk.ToString(CultureInfo.InvariantCulture)}");
    }

    /// <summary>Every number under the section was compared, and none of them differed.</summary>
    private void Settle(string section)
    {
        var root = Resolve(section);
        root.Should().NotBeNull($"the handoff publishes {section}");

        var uncompared = Numbers(root!.Value, section).Where(path => !_compared.Contains(path)).ToArray();
        uncompared.Should().BeEmpty("the handoff publishes these and nothing compared them:" + List(uncompared));
        _differed.Should().BeEmpty("the design system and the implementation must agree:" + List(_differed));
    }

    private static IEnumerable<string> Numbers(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            yield return path;
            yield break;
        }

        if (element.ValueKind != JsonValueKind.Object) yield break;
        foreach (var property in element.EnumerateObject())
            foreach (var leaf in Numbers(property.Value, $"{path}.{property.Name}"))
                yield return leaf;
    }

    private static JsonElement? Resolve(string path)
    {
        var node = Handoff;
        foreach (var part in path.Split('.'))
        {
            if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty(part, out var next)) return null;
            node = next;
        }
        return node;
    }

    private static bool Resolves(string path) =>
        Resolve(path) is { ValueKind: JsonValueKind.Number or JsonValueKind.String };

    private static bool TryNumber(string path, out float value)
    {
        value = 0;
        if (Resolve(path) is not { ValueKind: JsonValueKind.Number } node) return false;
        value = node.GetSingle();
        return true;
    }

    /// <summary>"cubic(0.2,0,0,1)" as its four control points.</summary>
    private static float[] Points(string? text)
    {
        if (text is null) return [];
        var open = text.IndexOf('(');
        var close = text.IndexOf(')');
        if (open < 0 || close < open) return [];
        return text[(open + 1)..close].Split(',')
            .Select(v => float.Parse(v.Trim(), CultureInfo.InvariantCulture)).ToArray();
    }

    private static string Describe(Primitives.Curve c) =>
        FormattableString.Invariant($"cubic({c.X1},{c.Y1},{c.X2},{c.Y2})");

    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    private static string List(IEnumerable<string> items) =>
        Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", items) + Environment.NewLine;
}
