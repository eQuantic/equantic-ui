using eQuantic.UI.Components;
using eQuantic.UI.Material;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The web's half of the form control slots (E6b, #1242; <c>docs/ELEMENT-TREE-PLAN.md</c> §3.10): a value
/// a theme puts in a family's slot reaches the web's lowering of a component of that family, at the
/// density the page is in, and Material 3's specifications reach it through <see cref="MaterialTheme"/>.
/// Read from the atomic stylesheet the lowering writes.
/// </summary>
public class FormControlSlotsRealizerTests
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

    /// <summary>One component per family, a theme whose slot names one value, and the declarations that
    /// value writes, comfortable and compact.</summary>
    public sealed record Case(string Family, Func<VisualNode> Build, IAppTheme Theme, string[] Comfortable,
        string[] Compact)
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
            ["width:52px", "height:52px"], ["width:44px", "height:44px"]),
        new Case("Input", () => new TextInput("ana@example.com", _ => { }, label: "Email"),
            Photon.With(input: new InputTheme
            {
                Controls = new ControlsTheme { Height = Rung(SizeVariant.Large, 60, 50) },
            }),
            ["height:60px"], ["height:50px"]),
        new Case("SelectionControls", () => new Checkbox(true, () => { }, "Remember me"),
            Photon.With(selectionControls: new SelectionControlsTheme
            {
                Box = new DensityPair { Comfortable = 30, Compact = 24 },
            }),
            ["width:30px", "height:30px"], ["width:24px", "height:24px"]),
        new Case("Segmented", () => new SegmentedControl(["All", "Income"], 0, _ => { }),
            Photon.With(segmented: new SegmentedTheme
            {
                Controls = new ControlsTheme { Height = Rung(SizeVariant.Medium, 52, 44) },
            }),
            ["height:52px"], ["height:44px"]),
        new Case("Chip", () => new Chip("Income", ChipKind.Filter, false, () => { }),
            Photon.With(chip: new ChipTheme
            {
                Controls = new ControlsTheme { Height = Rung(SizeVariant.Small, 38, 30) },
            }),
            ["height:38px"], ["height:30px"]),
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void AValueInAFamilysSlot_ReachesTheWebsLowering(Case c)
    {
        foreach (var (density, declarations) in new[] { (Density.Comfortable, c.Comfortable), (Density.Compact, c.Compact) })
        {
            var slotted = Css(c.Build(), c.Theme, density);
            var plain = Css(c.Build(), Photon, density);
            foreach (var declaration in declarations)
            {
                slotted.Should().Contain(declaration, $"the {c.Family} slot writes {declaration} at {density}");
                plain.Should().NotContain(declaration, $"no {c.Family} box writes {declaration} under Photon at {density}");
            }
        }
    }

    /// <summary>Material 3's specifications, read from the declarations a component of each family lowers
    /// to under <see cref="MaterialTheme"/>.</summary>
    [Fact]
    public void MaterialTheme_LowersTheFormControls_AtMaterial3sSizes()
    {
        var material = MaterialTheme.Instance;
        var missing = new List<string>();

        void Expect(string what, VisualNode node, params string[] declarations)
        {
            var css = Css(node, material, Density.Comfortable);
            foreach (var declaration in declarations)
                if (!css.Contains(declaration, StringComparison.Ordinal)) missing.Add($"{what}: {declaration}");
        }

        Expect("the icon button's container", new IconButton(new Icon(Icons.Search), "Search"), "width:40px", "height:40px");
        Expect("the text field's container", new TextInput("ana", _ => { }, label: "Email"), "height:56px", "border-radius:4px");
        Expect("the checkbox", new Checkbox(false, () => { }, "Remember me"), "width:18px", "height:18px", "border-radius:2px");
        Expect("the radio", new RadioGroup(["Card", "Cash"], 0, _ => { }), "width:20px", "width:10px");
        Expect("the switch", new Switch(true, () => { }), "width:52px", "height:32px", "width:24px");
        Expect("the segmented button", new SegmentedControl(["All", "Income"], 0, _ => { }), "height:40px",
            "border:1px solid");
        Expect("the chip", new Chip("Income", ChipKind.Filter, false, () => { }), "height:32px", "border-radius:8px");

        missing.Should().BeEmpty("MaterialTheme fills the form control slots from Material 3's specifications");
    }

    /// <summary>Material 3 gives a field, a segmented button and a chip one size, which a compact page and
    /// a size variant do not shrink: the ladder the slot names holds every rung at either density.</summary>
    [Fact]
    public void MaterialTheme_KeepsItsOneSize_AtEveryRungAndDensity()
    {
        var material = MaterialTheme.Instance;
        var missing = new List<string>();

        void Expect(string what, VisualNode node, string declaration)
        {
            if (!Css(node, material, Density.Compact).Contains(declaration, StringComparison.Ordinal))
                missing.Add($"{what}: {declaration}");
        }

        Expect("the compact text field", new TextInput("ana", _ => { }, label: "Email"), "height:56px");
        Expect("the compact search bar", new SearchField("harbour", _ => { }), "height:56px");
        Expect("the compact chip", new Chip("Income", ChipKind.Filter, false, () => { }), "height:32px");
        Expect("the compact small segmented button",
            new SegmentedControl(["All", "Income"], 0, _ => { }) { Size = SizeVariant.Small }, "height:40px");

        missing.Should().BeEmpty("Material 3 does not shrink these controls for a dense layout or a size");
    }

    /// <summary>A picker's panel rows are the field's height unless the slot names their own: Material's
    /// menu items are 48dp under a 56dp field, and Photon's rows stay the field's height.</summary>
    [Fact]
    public void APickersPanelRows_TakeTheSlotsRowHeight_OrTheFields()
    {
        float?[] RowHeights(IAppTheme theme)
        {
            var select = new Select(["Card", "Cash", "Transfer"], 0, _ => { });
            return Walk(select.BuildContained(new ComponentContext(theme)), theme)
                .OfType<Pressable>()
                .Where(p => p.Role == PressableRole.Option)
                .Select(p => p.Child is Box { Style.Height: { Kind: SizeKind.Fixed } height } ? height.Value : (float?)null)
                .ToArray();
        }

        RowHeights(MaterialTheme.Instance).Should().Equal(48f, 48f, 48f);
        var field = Sizing.Height(SizeVariant.Medium, Density.Comfortable);
        RowHeights(Photon).Should().Equal(field, field, field);
    }

    private static IEnumerable<VisualNode> Walk(VisualNode node, IAppTheme theme)
    {
        yield return node;
        var children = node switch
        {
            FlexNode flex => flex.Children,
            Box box => box.Child is { } only ? new[] { only } : Array.Empty<VisualNode>(),
            Pressable pressable => new[] { pressable.Child },
            Flexible flexible => new[] { flexible.Child },
            Anchored anchored => new[] { anchored.Anchor, anchored.Panel },
            Shortcut shortcut => new[] { shortcut.Child },
            UiComponent component => new[] { component.BuildContained(new ComponentContext(theme)) },
            _ => Array.Empty<VisualNode>(),
        };
        foreach (var child in children)
            foreach (var descendant in Walk(child, theme))
                yield return descendant;
    }

    private static string Css(VisualNode node, IAppTheme theme, Density density)
    {
        var sink = new StyleSink();
        WebRealizer.Lower(node, theme, 1f, sink, density).Render();
        return sink.Css;
    }
}
