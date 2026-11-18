using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The web's half of #1606's style: every button, selection control and progress component takes its
/// family's slot record as <c>style</c>, and resolves it ahead of the theme's slot, member by member
/// (<c>docs/ELEMENT-TREE-PLAN.md</c> §3.10, the first step of the order). Read from the declarations the
/// lowering writes, in the atomic stylesheet and inline. Photon's half is
/// <c>ButtonsSelectionAndProgressStyleTests</c>.
/// </summary>
public class ButtonsSelectionAndProgressStyleRealizerTests
{
    private static readonly PhotonTheme Photon = PhotonTheme.Instance;

    /// <summary>A component built with no style and with its own, a theme whose slot names one value, and
    /// what the instance names for the same member, as the CSS property they write.</summary>
    public sealed record Case(string Component, Func<VisualNode> Plain, Func<VisualNode> Own, IAppTheme Theme,
        string Property, float SlotValue, float OwnValue)
    {
        public override string ToString() => Component;

        public string Slot => $"{Property}:{SlotValue}px";
        public string Mine => $"{Property}:{OwnValue}px";
    }

    private static ControlsTheme Height(SizeVariant size, float value) => new()
    {
        Height = new DensityLadder
        {
            Comfortable = size == SizeVariant.Small ? new SizeLadder { Small = value } : new SizeLadder { Medium = value },
        },
    };

    public static TheoryData<Case> Cases => new()
    {
        new Case("Button", () => new Button("Save"), () => new Button("Save", style: new ButtonTheme { MinWidth = 191 }),
            Photon.With(button: new ButtonTheme { MinWidth = 173 }), "min-width", 173, 191),
        new Case("FormSubmit", () => new FormSubmit(new FormController(), "Send", () => Task.CompletedTask),
            () => new FormSubmit(new FormController(), "Send", () => Task.CompletedTask,
                style: new ButtonTheme { MinWidth = 191 }),
            Photon.With(button: new ButtonTheme { MinWidth = 173 }), "min-width", 173, 191),
        new Case("IconButton", () => new IconButton(new Icon(Icons.Search), "Search"),
            () => new IconButton(new Icon(Icons.Search), "Search",
                style: new IconButtonTheme { Controls = Height(SizeVariant.Medium, 57) }),
            Photon.With(iconButton: new IconButtonTheme { Controls = Height(SizeVariant.Medium, 49) }), "height", 49, 57),
        new Case("Chip", () => new Chip("Income"),
            () => new Chip("Income", style: new ChipTheme { Controls = Height(SizeVariant.Small, 47) }),
            Photon.With(chip: new ChipTheme { Controls = Height(SizeVariant.Small, 41) }), "height", 41, 47),
        new Case("Checkbox", () => new Checkbox(true, () => { }, "Remember me"),
            () => new Checkbox(true, () => { }, "Remember me",
                style: new SelectionControlsTheme { Box = new DensityPair { Comfortable = 33 } }),
            Photon.With(selectionControls: new SelectionControlsTheme { Box = new DensityPair { Comfortable = 29 } }),
            "width", 29, 33),
        new Case("RadioGroup", () => new RadioGroup(["Card", "Cash"], 0, _ => { }),
            () => new RadioGroup(["Card", "Cash"], 0, _ => { },
                style: new SelectionControlsTheme { RadioSize = new DensityPair { Comfortable = 33 } }),
            Photon.With(selectionControls: new SelectionControlsTheme { RadioSize = new DensityPair { Comfortable = 29 } }),
            "width", 29, 33),
        new Case("Switch", () => new Switch(true, () => { }),
            () => new Switch(true, () => { },
                style: new SelectionControlsTheme { SwitchWidth = new DensityPair { Comfortable = 67 } }),
            Photon.With(selectionControls: new SelectionControlsTheme { SwitchWidth = new DensityPair { Comfortable = 61 } }),
            "width", 61, 67),
        new Case("Slider", () => new Slider(0.5f, _ => { }),
            () => new Slider(0.5f, _ => { }, style: new SelectionControlsTheme { SliderThumbSize = 35 }),
            Photon.With(selectionControls: new SelectionControlsTheme { SliderThumbSize = 29 }), "width", 29, 35),
        new Case("ProgressBar", () => new ProgressBar(0.5f),
            () => new ProgressBar(0.5f, style: new ProgressTheme { BarHeight = 11 }),
            Photon.With(progress: new ProgressTheme { BarHeight = 7 }), "height", 7, 11),
        new Case("PullToRefresh", () => new PullToRefresh(Content(), () => { }),
            () => new PullToRefresh(Content(), () => { }, style: new ProgressTheme { RefreshSpinner = IconSize.Lg }),
            Photon.With(progress: new ProgressTheme { RefreshSpinner = IconSize.Md }), "width", IconSize.Md, IconSize.Lg),
        new Case("Skeleton", () => new Skeleton(SkeletonShape.Line, 200),
            () => new Skeleton(SkeletonShape.Line, 200, style: new ProgressTheme { SkeletonLineHeight = 17 }),
            Photon.With(progress: new ProgressTheme { SkeletonLineHeight = 13 }), "height", 13, 17),
    };

    private static VisualNode Content() => new Box(new BoxStyle { Height = 100 });

    [Theory]
    [MemberData(nameof(Cases))]
    public void AComponentsOwnStyle_ComesAheadOfTheThemesSlot(Case c)
    {
        var plain = Declarations(c.Plain(), Photon);
        plain.Should().NotContain(c.Slot, $"no {c.Component} writes {c.Slot} under Photon, so it is the style's");
        plain.Should().NotContain(c.Mine, $"no {c.Component} writes {c.Mine} under Photon, so it is the style's");
        Declarations(c.Plain(), c.Theme).Should().Contain(c.Slot,
            $"with no style of its own, the {c.Component} writes the slot's {c.Slot}");

        var own = Declarations(c.Own(), c.Theme);
        own.Should().Contain(c.Mine, $"the {c.Component}'s own {c.Mine} wins");
        own.Should().NotContain(c.Slot, $"the slot's {c.Slot} is laid under it");
    }

    /// <summary>What the instance's style leaves null is the slot's: a radio group that names only its dot
    /// keeps the ring the theme's slot names.</summary>
    [Fact]
    public void AMemberTheOwnStyleLeavesNull_IsTheSlots()
    {
        var theme = Photon.With(selectionControls: new SelectionControlsTheme { RadioSize = new DensityPair { Comfortable = 29 } });
        var group = new RadioGroup(["Card", "Cash"], 0, _ => { },
            style: new SelectionControlsTheme { RadioDot = new DensityPair { Comfortable = 13 } });

        var declarations = Declarations(group, theme);
        declarations.Should().Contain("width:13px", "the group's own dot");
        declarations.Should().Contain("width:29px", "the ring the slot names");
    }

    /// <summary>The atomic stylesheet and every inline style, with the space after a colon dropped, so one
    /// spelling reads both.</summary>
    private static string Declarations(VisualNode node, IAppTheme theme)
    {
        var sink = new StyleSink();
        var root = WebRealizer.Lower(node, theme, 1f, sink, Density.Comfortable).Render();
        var inline = new List<string>();
        Collect(root, inline);
        return (sink.Css + ";" + string.Join(";", inline)).Replace(": ", ":", StringComparison.Ordinal);
    }

    private static void Collect(HtmlNode node, List<string> styles)
    {
        if (node.Attributes.TryGetValue("style", out var style)) styles.Add(style);
        foreach (var child in node.Children) Collect(child, styles);
    }
}
