using eQuantic.UI.Components;
using eQuantic.UI.Material;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Photon's half of the form control slots (E6b, #1242; <c>docs/ELEMENT-TREE-PLAN.md</c> §3.10): a value a
/// theme puts in a family's slot reaches the layout of a component of that family, at the density the
/// host is in, and Material 3's specifications reach it through <see cref="MaterialTheme"/>. Measured by
/// the boxes the layout placed.
/// </summary>
public class FormControlSlotsTests
{
    private static readonly PhotonTheme Photon = PhotonTheme.Instance;

    private static DensityLadder Rung(SizeVariant size, float comfortable, float compact) => new()
    {
        Comfortable = Ladder(size, comfortable),
        Compact = Ladder(size, compact),
    };

    private static SizeLadder Ladder(SizeVariant size, float value) => size switch
    {
        SizeVariant.Small => new SizeLadder { Small = value },
        SizeVariant.Medium => new SizeLadder { Medium = value },
        SizeVariant.Large => new SizeLadder { Large = value },
        _ => new SizeLadder { XLarge = value },
    };

    /// <summary>One component per family, a theme whose slot names one value, and the box that value
    /// draws, comfortable and compact.</summary>
    public sealed record Case(string Family, Func<VisualNode> Build, IAppTheme Theme, (float W, float H)? Comfortable,
        (float W, float H)? Compact, float? ComfortableHeight = null, float? CompactHeight = null)
    {
        public override string ToString() => Family;
    }

    public static TheoryData<Case> Cases => new()
    {
        new Case("IconButton", () => new IconButton(new Icon(Icons.Search), "Search"),
            Photon.With(iconButton: new IconButtonTheme
            {
                Controls = new ControlsTheme { Height = Rung(SizeVariant.Medium, 52, 44) },
            }),
            (52, 52), (44, 44)),
        new Case("Input", () => new TextInput("ana@example.com", _ => { }, label: "Email"),
            Photon.With(input: new InputTheme
            {
                Controls = new ControlsTheme { Height = Rung(SizeVariant.Large, 60, 50) },
            }),
            null, null, 60, 50),
        new Case("SelectionControls", () => new Checkbox(true, () => { }, "Remember me"),
            Photon.With(selectionControls: new SelectionControlsTheme
            {
                Box = new DensityPair { Comfortable = 30, Compact = 24 },
            }),
            (30, 30), (24, 24)),
        new Case("Segmented", () => new SegmentedControl(["All", "Income"], 0, _ => { }),
            Photon.With(segmented: new SegmentedTheme
            {
                Controls = new ControlsTheme { Height = Rung(SizeVariant.Medium, 52, 44) },
            }),
            null, null, 52, 44),
        new Case("Chip", () => new Chip("Income", ChipKind.Filter, false, () => { }),
            Photon.With(chip: new ChipTheme
            {
                Controls = new ControlsTheme { Height = Rung(SizeVariant.Small, 38, 30) },
            }),
            null, null, 38, 30),
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void AValueInAFamilysSlot_ReachesPhotonsLayout(Case c)
    {
        foreach (var (density, size, height) in new[]
                 {
                     (Density.Comfortable, c.Comfortable, c.ComfortableHeight),
                     (Density.Compact, c.Compact, c.CompactHeight),
                 })
        {
            var slotted = Boxes(c.Build(), c.Theme, density);
            var plain = Boxes(c.Build(), Photon, density);
            if (size is { } box)
            {
                slotted.Should().Contain(r => Near(r.Width, box.W) && Near(r.Height, box.H),
                    $"the {c.Family} slot names a {box.W}x{box.H} box at {density}");
                plain.Should().NotContain(r => Near(r.Width, box.W) && Near(r.Height, box.H),
                    $"no {c.Family} box is {box.W}x{box.H} under Photon at {density}");
            }
            if (height is { } h)
            {
                slotted.Should().Contain(r => Near(r.Height, h), $"the {c.Family} slot names a height of {h} at {density}");
                plain.Should().NotContain(r => Near(r.Height, h), $"no {c.Family} box is {h} tall under Photon at {density}");
            }
        }
    }

    /// <summary>A member a slot leaves null is the component's own: a checkbox whose slot names only its
    /// box draws the corner and the label's gap it draws with no slot.</summary>
    [Fact]
    public void AMemberLeftNull_IsTheComponentsOwn()
    {
        var theme = Photon.With(selectionControls: new SelectionControlsTheme { Box = new DensityPair { Comfortable = 22 } });

        Boxes(new Checkbox(false, () => { }, "Remember me"), theme, Density.Comfortable)
            .Should().Equal(Boxes(new Checkbox(false, () => { }, "Remember me"), new BareTheme(Photon), Density.Comfortable));
    }

    /// <summary>Material 3's specifications, read through the boxes a component of each family lays out
    /// under <see cref="MaterialTheme"/>.</summary>
    [Fact]
    public void MaterialTheme_LaysOutTheFormControls_AtMaterial3sSizes()
    {
        var material = MaterialTheme.Instance;
        var missing = new List<string>();

        void Expect(string what, VisualNode node, float width, float height)
        {
            if (!Boxes(node, material, Density.Comfortable).Any(r => Near(r.Width, width) && Near(r.Height, height)))
                missing.Add($"{what} {width}x{height}");
        }

        void ExpectHeight(string what, VisualNode node, float height)
        {
            if (!Boxes(node, material, Density.Comfortable).Any(r => Near(r.Height, height)))
                missing.Add($"{what} {height} tall");
        }

        Expect("the icon button's container", new IconButton(new Icon(Icons.Search), "Search"), 40, 40);
        ExpectHeight("the text field's container", new TextInput("ana", _ => { }, label: "Email"), 56);
        ExpectHeight("the search bar", new SearchField("harbour", _ => { }), 56);
        Expect("the checkbox", new Checkbox(false, () => { }, "Remember me"), 18, 18);
        Expect("the radio", new RadioGroup(["Card", "Cash"], 0, _ => { }), 20, 20);
        Expect("the radio's dot", new RadioGroup(["Card", "Cash"], 0, _ => { }), 10, 10);
        Expect("the switch's track", new Switch(true, () => { }), 52, 32);
        Expect("the switch's handle", new Switch(true, () => { }), 24, 24);
        ExpectHeight("the segmented button", new SegmentedControl(["All", "Income"], 0, _ => { }), 40);
        ExpectHeight("the chip", new Chip("Income", ChipKind.Filter, false, () => { }), 32);

        missing.Should().BeEmpty("MaterialTheme fills the form control slots from Material 3's specifications");
    }

    /// <summary>Material 3 gives a field, a segmented button and a chip one size, which a compact host and
    /// a size variant do not shrink: the ladder the slot names holds every rung at either density.</summary>
    [Fact]
    public void MaterialTheme_KeepsItsOneSize_AtEveryRungAndDensity()
    {
        var material = MaterialTheme.Instance;
        var missing = new List<string>();

        void ExpectHeight(string what, VisualNode node, float height)
        {
            if (!Boxes(node, material, Density.Compact).Any(r => Near(r.Height, height)))
                missing.Add($"{what} {height} tall");
        }

        ExpectHeight("the compact text field", new TextInput("ana", _ => { }, label: "Email"), 56);
        ExpectHeight("the compact search bar", new SearchField("harbour", _ => { }), 56);
        ExpectHeight("the compact chip", new Chip("Income", ChipKind.Filter, false, () => { }), 32);
        ExpectHeight("the compact small segmented button",
            new SegmentedControl(["All", "Income"], 0, _ => { }) { Size = SizeVariant.Small }, 40);

        missing.Should().BeEmpty("Material 3 does not shrink these controls for a dense layout or a size");
    }

    /// <summary>Every glyph a slot sizes is held to the §07 whitelist, which an icon enforces by throwing:
    /// a Material 3 size off it (the chip's 18) turned the control into its boundary's error panel. Each
    /// control that draws a slot-sized glyph is built under both themes and must not fall back.</summary>
    [Fact]
    public void EveryGlyphASlotSizes_BuildsUnderBothThemes()
    {
        VisualNode[] glyphs =
        [
            new IconButton(new Icon(Icons.Search), "Search", size: SizeVariant.Small),
            new IconButton(new Icon(Icons.Search), "Search", size: SizeVariant.XLarge),
            new TextInput("ana", _ => { }, label: "Email", leading: Icons.Mail),
            new SearchField("harbour", _ => { }),
            new TimePicker(new TimeOnly(9, 30), _ => { }),
            new Stepper(2, _ => { }),
            new Chip("All", ChipKind.Filter, selected: true, onPressed: () => { }),
            new Chip("Ana", ChipKind.Input, onRemove: () => { }),
        ];

        foreach (var theme in new IAppTheme[] { Photon, MaterialTheme.Instance })
        {
            var broken = glyphs.Where(node => FellBack(node, theme)).Select(node => node.GetType().Name).ToList();
            broken.Should().BeEmpty($"every slot-sized glyph is on the §07 whitelist under {theme.GetType().Name}");
        }
    }

    private static bool FellBack(VisualNode node, IAppTheme theme)
    {
        var result = PhotonRealizer.Realize(node, 400, 400, theme, ThemeMode.Light, new DisplayListBuilder());
        return Any(result.Root, n => n.Source is Text { Content: var content }
            && (content.Contains("could not be displayed", StringComparison.Ordinal)
                || content.Contains("failed to render", StringComparison.Ordinal)));
    }

    private static bool Any(LayoutNode node, Func<LayoutNode, bool> match)
    {
        if (match(node)) return true;
        foreach (var child in node.Children)
            if (Any(child, match)) return true;
        return false;
    }

    private static bool Near(float a, float b) => MathF.Abs(a - b) < 0.5f;

    private static List<Rect> Boxes(VisualNode node, IAppTheme theme, Density density)
    {
        var result = PhotonRealizer.Realize(node, 400, 400, theme, ThemeMode.Light, new DisplayListBuilder(),
            density: density);
        var found = new List<Rect>();
        Walk(result.Root, found);
        return found;
    }

    private static void Walk(LayoutNode node, List<Rect> found)
    {
        found.Add(node.Bounds);
        foreach (var child in node.Children) Walk(child, found);
    }

    /// <summary>A theme that answers every abstract member of Photon's and leaves every slot to the
    /// interface: the component's own defaults.</summary>
    private sealed class BareTheme(IAppTheme inner) : IAppTheme
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
        public ColorToken Selection => inner.Selection;
        public ColorToken Placeholder => inner.Placeholder;
        public ColorToken ScrollbarThumb => inner.ScrollbarThumb;
        public ColorToken ScrollbarTrack => inner.ScrollbarTrack;
        public VariantColors Colors(Variant variant) => inner.Colors(variant);
        public TypeStyle Type(TypeRole role) => inner.Type(role);
        public ShadowSpec Elevation(int level) => inner.Elevation(level);
        public float Shape(ShapeScale scale) => inner.Shape(scale);
        public float DisabledOpacity => inner.DisabledOpacity;
        public DataPalette Data => inner.Data;
    }
}
