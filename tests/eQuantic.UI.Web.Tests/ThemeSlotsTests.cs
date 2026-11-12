using System.Reflection;
using System.Text.Json;
using eQuantic.UI.Components;
using eQuantic.UI.Material;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The theme slots' mechanism (E6a, #1241; <c>docs/ELEMENT-TREE-PLAN.md</c> §3.10): the control ladder and
/// the Button reach a component in the slot order, every member of the theme crosses to the browser in its
/// JSON, and <see cref="AppThemeExtensions.With"/> replaces a slot and forwards the rest.
/// </summary>
public class ThemeSlotsTests
{
    private static readonly PhotonTheme Photon = PhotonTheme.Instance;

    // ---- every member crosses -------------------------------------------------------------------

    /// <summary>
    /// Each member of <see cref="IAppTheme"/> and the JSON key it crosses under. A member this table does
    /// not name fails the test that reads it, so a member added to the theme without a way to the browser
    /// is a build failure rather than a palette that changes after hydration (#936).
    /// </summary>
    private static readonly Dictionary<string, string?> CrossesAs = new(StringComparer.Ordinal)
    {
        [nameof(IAppTheme.OnSelection)] = "onSelection",
        [nameof(IAppTheme.Colors)] = "variants",
        [nameof(IAppTheme.Type)] = "type",
        [nameof(IAppTheme.MonoFamily)] = "monoFamily",
        [nameof(IAppTheme.TextFamily)] = "textFamily",
        [nameof(IAppTheme.Elevation)] = "elevations",
        [nameof(IAppTheme.Shape)] = "shape",
        [nameof(IAppTheme.DisabledOpacity)] = "disabledOpacity",
        [nameof(IAppTheme.Data)] = "data",
        [nameof(IAppTheme.Sheet)] = "sheet",
        [nameof(IAppTheme.Code)] = "code",
        [nameof(IAppTheme.Controls)] = "controls",
        [nameof(IAppTheme.Button)] = "button",
        [nameof(IAppTheme.IconButton)] = "iconButton",
        [nameof(IAppTheme.Input)] = "input",
        [nameof(IAppTheme.SelectionControls)] = "selectionControls",
        [nameof(IAppTheme.Segmented)] = "segmented",
        [nameof(IAppTheme.Chip)] = "chip",
    };

    [Fact]
    public void EveryMemberOfTheTheme_CrossesToTheBrowser()
    {
        var theme = new EveryMemberTheme(Photon);
        using var json = JsonDocument.Parse(ThemeBridge.SerializeJson(theme));
        var root = json.RootElement;
        var surfaces = root.GetProperty("surfaces");

        var missing = new List<string>();
        foreach (var member in typeof(IAppTheme).GetMembers(BindingFlags.Public | BindingFlags.Instance)
                     .Where(member => member is PropertyInfo || member is MethodInfo { IsSpecialName: false })
                     .Select(member => member.Name).Distinct())
        {
            var property = typeof(IAppTheme).GetProperty(member);
            if (property?.PropertyType == typeof(ColorToken))
            {
                if (!surfaces.TryGetProperty(char.ToLowerInvariant(member[0]) + member[1..], out _))
                    missing.Add($"{member}: no surfaces entry");
                continue;
            }
            if (!CrossesAs.TryGetValue(member, out var key))
            {
                missing.Add($"{member}: no key in this table, so nothing says how it crosses");
                continue;
            }
            if (key is not null && !root.TryGetProperty(key, out _))
                missing.Add($"{member}: \"{key}\" is not in the theme's JSON");
        }

        missing.Should().BeEmpty("every member of the theme reaches the browser, or a re-render there answers another theme");
    }

    /// <summary>
    /// Every member of every slot has a form in the theme's JSON: each slot record, every member set,
    /// written and read back by name, nested records included. A member of a kind the writer has no form
    /// for throws there, so a slot a family adds is held here with nothing written.
    /// </summary>
    [Fact]
    public void EveryMemberOfEverySlot_CrossesUnderItsName()
    {
        var slots = ThemeBridge.SlotProperties().Select(property => property.PropertyType).ToList();
        slots.Should().Contain([typeof(SheetTheme), typeof(ControlsTheme), typeof(ButtonTheme)]);

        var missing = new List<string>();
        foreach (var slot in slots)
        {
            var record = Populated(slot);
            using var json = JsonDocument.Parse(ThemeBridge.SlotJson(record));
            Walk(slot.Name, record, json.RootElement, missing);
        }

        missing.Should().BeEmpty("a slot member the browser never reads is a look the page loses when it hydrates");
    }

    private static void Walk(string path, object record, JsonElement written, List<string> missing)
    {
        foreach (var property in SlotMembers(record.GetType()))
        {
            var name = char.ToLowerInvariant(property.Name[0]) + property.Name[1..];
            if (!written.TryGetProperty(name, out var value)) { missing.Add($"{path}.{name}"); continue; }
            if (IsRecord(property.PropertyType)) Walk($"{path}.{name}", property.GetValue(record)!, value, missing);
        }
    }

    private static IEnumerable<PropertyInfo> SlotMembers(Type record) =>
        record.GetProperties().Where(property => property.SetMethod is { IsPublic: true });

    private static bool IsRecord(Type type) =>
        type is { IsClass: true } && type.Assembly == typeof(IAppTheme).Assembly && type != typeof(string);

    /// <summary>A slot record with every member set to a value of its kind.</summary>
    private static object Populated(Type record, int seed = 1)
    {
        var instance = Activator.CreateInstance(record)!;
        foreach (var property in SlotMembers(record))
        {
            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            object value = type switch
            {
                _ when type == typeof(float) => (float)seed,
                _ when type == typeof(int) => seed,
                _ when type == typeof(bool) => seed == 1,
                { IsEnum: true } => Enum.GetValues(type).GetValue((seed - 1) % Enum.GetValues(type).Length)!,
                _ when type == typeof(ColorToken) => new ColorToken(seed == 1 ? Color.Black : Color.FromRgb((byte)seed, 0, 0)),
                _ when type == typeof(EdgeInsets) => new EdgeInsets(seed, 2, 3, 4),
                _ when type == typeof(TypeStyle) => TypeStyle.OfSize(12 + seed, FontWeight.SemiBold),
                _ when IsRecord(type) => Populated(type, seed),
                _ => throw new NotSupportedException($"{record.Name}.{property.Name} is a {type.Name}, which this test cannot set"),
            };
            property.SetValue(instance, value);
        }
        return instance;
    }

    [Fact]
    public void ASlot_CrossesWithTheRungsItNames_AndNothingElse()
    {
        var slot = new ControlsTheme
        {
            Height = new DensityLadder { Comfortable = new SizeLadder { Medium = 52 } },
        };
        ThemeBridge.SlotJson(slot).Should().Be("{\"height\":{\"comfortable\":{\"medium\":52}}}");

        // Laid over Photon's ladder, the theme crosses Photon's rungs with the one it named.
        using var json = JsonDocument.Parse(ThemeBridge.SerializeJson(Photon.With(controls: slot)));
        var comfortable = json.RootElement.GetProperty("controls").GetProperty("height").GetProperty("comfortable");
        comfortable.GetProperty("medium").GetSingle().Should().Be(52);
        comfortable.GetProperty("small").GetSingle().Should().Be(Photon.Controls!.Height!.Comfortable!.Small!.Value);
    }

    [Fact]
    public void TheCodePalette_CrossesKindByKind()
    {
        using var json = JsonDocument.Parse(ThemeBridge.SerializeJson(Photon));
        var code = json.RootElement.GetProperty("code");
        code.EnumerateObject().Select(kind => kind.Name)
            .Should().BeEquivalentTo(Enum.GetNames<CodeTokenKind>().Select(name => char.ToLowerInvariant(name[0]) + name[1..]));
    }

    private static IEnumerable<string> Names<T>() => typeof(T).GetProperties()
        .Where(property => property.Name != "EqualityContract")
        .Select(property => char.ToLowerInvariant(property.Name[0]) + property.Name[1..]);

    // ---- With ------------------------------------------------------------------------------------

    /// <summary>A member of the theme the override left to the interface would answer the interface's
    /// default instead of the wrapped theme's: the loss <see cref="AppThemeExtensions.With"/> exists to prevent.</summary>
    [Fact]
    public void With_DeclaresEveryMemberOfTheTheme()
    {
        var overridden = Photon.With().GetType();
        var map = overridden.GetInterfaceMap(typeof(IAppTheme));
        map.TargetMethods.Where(method => method.DeclaringType != overridden).Select(method => method.Name)
            .Should().BeEmpty("the override forwards every member, the interface's defaults included");
    }

    [Fact]
    public void With_LaysTheSlotItIsGivenOverTheThemes_AndForwardsTheRest()
    {
        var theme = new EveryMemberTheme(Photon).With(button: new ButtonTheme { MinWidth = 88 });

        theme.Button!.MinWidth.Should().Be(88);
        theme.Button.LabelWeight.Should().Be(Photon.Button!.LabelWeight, "a member the given slot leaves null is the theme's");
        theme.Button.OutlineColor.Should().Be(Photon.Button.OutlineColor);
        theme.Controls.Should().BeSameAs(Photon.Controls, "a slot it was not given is the wrapped theme's");
        theme.Sheet.Should().NotBeNull("a default the wrapped theme answers is forwarded, not the interface's");
        theme.Code(CodeTokenKind.Keyword).Should().Be(new EveryMemberTheme(Photon).Code(CodeTokenKind.Keyword));
        theme.Colors(Variant.Primary).Should().Be(Photon.Colors(Variant.Primary));
        theme.With(controls: new ControlsTheme()).Button!.MinWidth.Should().Be(88, "a With on the override keeps its slot");
    }

    /// <summary>The case that found it (#1472's review): a theme that overrides one member of a slot its
    /// design system fills keeps every other member, nested ladders included.</summary>
    [Fact]
    public void With_OneMember_KeepsEveryOtherTheDesignSystemSet()
    {
        var material = MaterialTheme.Instance;
        var theme = material.With(button: new ButtonTheme { MinWidth = 88 });

        theme.Button!.MinWidth.Should().Be(88);
        theme.Button.LabelWeight.Should().Be(material.Button!.LabelWeight);
        theme.Button.LinkPaddingX.Should().Be(material.Button.LinkPaddingX);
        theme.Button.Controls.Should().Be(material.Button.Controls, "the button's own ladder is Material's, rung for rung");

        var taller = Photon.With(controls: new ControlsTheme
        {
            Height = new DensityLadder { Comfortable = new SizeLadder { Medium = 44 } },
        });
        taller.Controls!.Height!.Comfortable!.Medium.Should().Be(44);
        taller.Controls.Height.Comfortable.Small.Should().Be(Photon.Controls!.Height!.Comfortable!.Small, "a rung left null is the theme's");
        taller.Controls.Height.Compact.Should().Be(Photon.Controls.Height.Compact, "a side left null is the theme's");
        taller.Controls.Radius.Should().Be(Photon.Controls.Radius, "a ladder left null is the theme's");
    }

    /// <summary>
    /// Every slot record merges member by member: for each of its members, an override that names only that
    /// member wins it and keeps every other member of the record it is laid over. A member a record's
    /// <c>Merge</c> forgot would be dropped by every <c>With</c> that names any other one, and a record with
    /// no <c>Merge</c> could not be laid over a theme at all.
    /// </summary>
    [Fact]
    public void EverySlotRecord_MergesMemberByMember()
    {
        var records = new HashSet<Type>();
        void Collect(Type record)
        {
            if (!records.Add(record)) return;
            foreach (var member in SlotMembers(record))
                if (IsRecord(Nullable.GetUnderlyingType(member.PropertyType) ?? member.PropertyType))
                    Collect(member.PropertyType);
        }
        // The slots With takes, and every slot record they hold.
        foreach (var parameter in typeof(AppThemeExtensions).GetMethod(nameof(AppThemeExtensions.With))!.GetParameters().Skip(1))
            Collect(Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType);
        records.Should().Contain([typeof(SizeLadder), typeof(DensityLadder), typeof(ControlsTheme), typeof(ButtonTheme)]);

        var wrong = new List<string>();
        foreach (var record in records)
        {
            var merge = record.GetMethod("Merge", BindingFlags.Public | BindingFlags.Static, [record, record]);
            if (merge is null) { wrong.Add($"{record.Name} has no public static Merge({record.Name}?, {record.Name}?)"); continue; }

            var under = Populated(record, 1);
            if (merge.Invoke(null, [under, null]) != under) wrong.Add($"{record.Name}.Merge(under, null) is not under");
            if (merge.Invoke(null, [null, under]) != under) wrong.Add($"{record.Name}.Merge(null, over) is not over");
            foreach (var member in SlotMembers(record))
            {
                var over = Activator.CreateInstance(record)!;
                var named = member.GetValue(Populated(record, 2));
                member.SetValue(over, named);
                var merged = merge.Invoke(null, [under, over])!;
                if (!Equals(member.GetValue(merged), named)) wrong.Add($"{record.Name}.{member.Name}: the override's value does not win");
                foreach (var other in SlotMembers(record).Where(other => other != member))
                    if (!Equals(other.GetValue(merged), other.GetValue(under)))
                        wrong.Add($"{record.Name}.{other.Name}: dropped when {member.Name} is overridden");
            }
        }

        wrong.Should().BeEmpty("a With that names one member keeps every other member of the slot it is laid over");
    }

    // ---- the ladder --------------------------------------------------------------------------------

    /// <summary>Photon's slot is the ladder rung for rung, which is what keeps every pin byte for byte.</summary>
    [Fact]
    public void PhotonsControlsSlot_IsTheLadder()
    {
        var controls = Photon.Controls!;
        foreach (var size in Enum.GetValues<SizeVariant>())
        {
            foreach (var density in Enum.GetValues<Density>())
            {
                var side = density == Density.Compact ? 0 : 1;
                Rung(side == 0 ? controls.Height!.Compact : controls.Height!.Comfortable, size).Should().Be(Sizing.Height(size, density));
                Rung(side == 0 ? controls.PaddingX!.Compact : controls.PaddingX!.Comfortable, size).Should().Be(Sizing.PaddingX(size, density));
                Rung(side == 0 ? controls.LabelSize!.Compact : controls.LabelSize!.Comfortable, size).Should().Be(Sizing.LabelSize(size, density));
            }
            Rung(controls.Gap, size).Should().Be(Sizing.Gap(size));
            Rung(controls.Icon, size).Should().Be(Sizing.Icon(size));
            Rung(controls.Radius, size).Should().Be(Sizing.Radius(size));
        }
    }

    private static float? Rung(SizeLadder? ladder, SizeVariant size) => size switch
    {
        SizeVariant.Small => ladder?.Small,
        SizeVariant.Medium => ladder?.Medium,
        SizeVariant.Large => ladder?.Large,
        _ => ladder?.XLarge,
    };

    /// <summary>
    /// The controls that read the control height at their medium rung, and why the others that read it
    /// are not here. <see cref="EveryControlThatReadsTheHeight_IsMeasured"/> holds the table to the
    /// sources, so a control that starts reading the ladder joins it.
    /// </summary>
    internal static readonly Dictionary<string, Func<VisualNode>?> MediumControls = new(StringComparer.Ordinal)
    {
        ["Button"] = () => new Button("Continue"),
        ["IconButton"] = () => new IconButton(new Icon(Icons.Search), "Search"),
        ["TextInput"] = () => new TextInput("ana@example.com", _ => { }, label: "Email", size: SizeVariant.Medium),
        ["SegmentedControl"] = () => new SegmentedControl(["All", "Income"], 0, _ => { }),
        ["Stepper"] = () => new Stepper(2, _ => { }),
        ["Select"] = () => new Select(["Lisbon", "Porto"], 0, _ => { }),
        ["SearchField"] = () => new SearchField("harbour", _ => { }),
        ["TimePicker"] = () => new TimePicker(new TimeOnly(9, 30), _ => { }),
        // Its rows, which only an open menu shows.
        ["Menu"] = null,
        // Its small rung, which a chip always is.
        ["Chip"] = null,
    };

    [Fact]
    public void EveryControlThatReadsTheHeight_IsMeasured()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Directory.Build.props"))) dir = dir.Parent;
        var components = Path.Combine(dir!.FullName, "src", "eQuantic.UI.Components");
        var readers = Directory.EnumerateFiles(components, "*.cs")
            .Where(file => !file.EndsWith("ControlMetrics.cs", StringComparison.Ordinal)
                && File.ReadAllText(file).Contains("ControlMetrics.Height(", StringComparison.Ordinal))
            .Select(Path.GetFileNameWithoutExtension);

        readers.Should().BeEquivalentTo(MediumControls.Keys, "a control that reads the height is measured below, or says why not");
    }

    /// <summary>A theme that changes the medium height changes every medium control, on both densities.</summary>
    [Theory]
    [InlineData(Density.Comfortable, 52)]
    [InlineData(Density.Compact, 44)]
    public void AThemeThatChangesTheMediumHeight_ChangesEveryMediumControl_OnTheWeb(Density density, int height)
    {
        var taller = Photon.With(controls: Photon.Controls! with
        {
            Height = new DensityLadder { Comfortable = new SizeLadder { Medium = 52 }, Compact = new SizeLadder { Medium = 44 } },
        });

        var unchanged = new List<string>();
        foreach (var (name, build) in MediumControls)
        {
            if (build is null) continue;
            var css = Css(build(), taller, density);
            if (!css.Contains($"height:{height}px", StringComparison.Ordinal)) unchanged.Add(name);
            Css(build(), Photon, density).Should().NotContain($"height:{height}px", $"{name} is not that tall under Photon");
        }

        unchanged.Should().BeEmpty("every medium control reads the theme's height");
    }

    /// <summary>
    /// An icon is drawn only at a size of the §07 ladder, and refuses any other: a slot that names an icon
    /// size off it puts every button with an icon behind its boundary's fallback, which a gallery of
    /// buttons without icons never shows. Every shipped theme, every size, with an icon.
    /// </summary>
    [Fact]
    public void AButtonWithAnIcon_BuildsUnderEveryShippedTheme()
    {
        foreach (var theme in new IAppTheme[] { Photon, eQuantic.UI.Material.MaterialTheme.Instance })
            foreach (var size in Enum.GetValues<SizeVariant>())
            {
                var html = HtmlRenderer.RenderNode(WebRealizer.Lower(new Button("Search", size: size) { Leading = Icons.Search }, theme).Render());
                html.Should().NotContain("could not be displayed", $"{theme.GetType().Name} at {size}")
                    .And.NotContain("failed to render", $"{theme.GetType().Name} at {size}");
            }
    }

    private static string Css(VisualNode node, IAppTheme theme, Density density)
    {
        var sink = new StyleSink();
        WebRealizer.Lower(node, theme, 1f, sink, density).Render();
        return sink.Css;
    }

    // ---- a theme that answers every member ---------------------------------------------------------

    /// <summary>The wrapped theme, with every optional member answered: a face, the selected text's
    /// colour, a spreadsheet slot and a code palette of its own.</summary>
    private sealed class EveryMemberTheme(IAppTheme inner) : IAppTheme
    {
        public ColorToken Background => inner.Background;
        public ColorToken Surface => inner.Surface;
        public ColorToken SurfaceSubtle => inner.SurfaceSubtle;
        public ColorToken SurfaceHighlight => inner.SurfaceHighlight;
        public ColorToken Border => inner.Border;
        public ColorToken BorderStrong => inner.BorderStrong;
        public ColorToken TextPrimary => inner.TextPrimary;
        public ColorToken TextSecondary => inner.TextSecondary;
        public ColorToken TextMuted => inner.TextMuted;
        public ColorToken TextInverse => inner.TextInverse;
        public ColorToken FocusRing => inner.FocusRing;
        public ColorToken LinkColor => inner.LinkColor;
        public ColorToken Scrim => inner.Scrim;
        public ColorToken? OnSelection => new(Color.White);
        public FontFamily? MonoFamily => new("JetBrains Mono");
        public FontFamily? TextFamily => new("Inter");
        public SheetTheme? Sheet => new() { Gridline = new ColorToken(Color.Black) };
        public ControlsTheme? Controls => inner.Controls;
        public ButtonTheme? Button => inner.Button;
        public IconButtonTheme? IconButton => inner.IconButton;
        public InputTheme? Input => inner.Input;
        public SelectionControlsTheme? SelectionControls => inner.SelectionControls;
        public SegmentedTheme? Segmented => inner.Segmented;
        public ChipTheme? Chip => inner.Chip;
        public ColorToken Code(CodeTokenKind kind) => new(Color.FromRgb(0x11, 0x22, (byte)kind));
        public float DisabledOpacity => inner.DisabledOpacity;
        public VariantColors Colors(Variant variant) => inner.Colors(variant);
        public TypeStyle Type(TypeRole role) => inner.Type(role);
        public ShadowSpec Elevation(int level) => inner.Elevation(level);
        public float Shape(ShapeScale scale) => inner.Shape(scale);
    }
}
