using eQuantic.UI.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Photon's half of #1606's style: every button, selection control and progress component takes its
/// family's slot record as <c>style</c>, and resolves it ahead of the theme's slot, member by member
/// (<c>docs/ELEMENT-TREE-PLAN.md</c> §3.10, the first step of the order). Measured by the boxes the layout
/// placed. The web's half is <c>ButtonsSelectionAndProgressStyleRealizerTests</c>.
/// </summary>
public class ButtonsSelectionAndProgressStyleTests
{
    private static readonly PhotonTheme Photon = PhotonTheme.Instance;

    /// <summary>A component built with no style and with its own, a theme whose slot names one value, and
    /// what the instance names for the same member, read from the boxes by <see cref="Read"/>.</summary>
    public sealed record Case(string Component, Func<VisualNode> Plain, Func<VisualNode> Own, IAppTheme Theme,
        Func<Rect, float> Read, float SlotValue, float OwnValue)
    {
        public override string ToString() => Component;
    }

    private static ControlsTheme Height(SizeVariant size, float value) => new()
    {
        Height = new DensityLadder
        {
            Comfortable = size == SizeVariant.Small ? new SizeLadder { Small = value } : new SizeLadder { Medium = value },
        },
    };

    private static float Width(Rect r) => r.Width;
    private static float Tall(Rect r) => r.Height;

    public static TheoryData<Case> Cases => new()
    {
        new Case("Button", () => new Button("Save"), () => new Button("Save", style: new ButtonTheme { MinWidth = 191 }),
            Photon.With(button: new ButtonTheme { MinWidth = 173 }), Width, 173, 191),
        new Case("FormSubmit", () => new FormSubmit(new FormController(), "Send", () => Task.CompletedTask),
            () => new FormSubmit(new FormController(), "Send", () => Task.CompletedTask,
                style: new ButtonTheme { MinWidth = 191 }),
            Photon.With(button: new ButtonTheme { MinWidth = 173 }), Width, 173, 191),
        new Case("IconButton", () => new IconButton(new Icon(Icons.Search), "Search"),
            () => new IconButton(new Icon(Icons.Search), "Search",
                style: new IconButtonTheme { Controls = Height(SizeVariant.Medium, 57) }),
            Photon.With(iconButton: new IconButtonTheme { Controls = Height(SizeVariant.Medium, 49) }), Width, 49, 57),
        new Case("Chip", () => new Chip("Income"),
            () => new Chip("Income", style: new ChipTheme { Controls = Height(SizeVariant.Small, 47) }),
            Photon.With(chip: new ChipTheme { Controls = Height(SizeVariant.Small, 41) }), Tall, 41, 47),
        new Case("Checkbox", () => new Checkbox(true, () => { }, "Remember me"),
            () => new Checkbox(true, () => { }, "Remember me",
                style: new SelectionControlsTheme { Box = new DensityPair { Comfortable = 33 } }),
            Photon.With(selectionControls: new SelectionControlsTheme { Box = new DensityPair { Comfortable = 29 } }),
            Width, 29, 33),
        new Case("RadioGroup", () => new RadioGroup(["Card", "Cash"], 0, _ => { }),
            () => new RadioGroup(["Card", "Cash"], 0, _ => { },
                style: new SelectionControlsTheme { RadioSize = new DensityPair { Comfortable = 33 } }),
            Photon.With(selectionControls: new SelectionControlsTheme { RadioSize = new DensityPair { Comfortable = 29 } }),
            Width, 29, 33),
        new Case("Switch", () => new Switch(true, () => { }),
            () => new Switch(true, () => { },
                style: new SelectionControlsTheme { SwitchWidth = new DensityPair { Comfortable = 67 } }),
            Photon.With(selectionControls: new SelectionControlsTheme { SwitchWidth = new DensityPair { Comfortable = 61 } }),
            Width, 61, 67),
        new Case("Slider", () => new Slider(0.5f, _ => { }),
            () => new Slider(0.5f, _ => { }, style: new SelectionControlsTheme { SliderThumbSize = 35 }),
            Photon.With(selectionControls: new SelectionControlsTheme { SliderThumbSize = 29 }), Width, 29, 35),
        new Case("ProgressBar", () => new ProgressBar(0.5f),
            () => new ProgressBar(0.5f, style: new ProgressTheme { BarHeight = 11 }),
            Photon.With(progress: new ProgressTheme { BarHeight = 7 }), Tall, 7, 11),
        new Case("PullToRefresh", () => new PullToRefresh(Content(), () => { }),
            () => new PullToRefresh(Content(), () => { }, style: new ProgressTheme { RefreshSpinner = IconSize.Lg }),
            Photon.With(progress: new ProgressTheme { RefreshSpinner = IconSize.Md }), Width, IconSize.Md, IconSize.Lg),
        new Case("Skeleton", () => new Skeleton(SkeletonShape.Line, 200),
            () => new Skeleton(SkeletonShape.Line, 200, style: new ProgressTheme { SkeletonLineHeight = 17 }),
            Photon.With(progress: new ProgressTheme { SkeletonLineHeight = 13 }), Tall, 13, 17),
    };

    private static VisualNode Content() => new Box(new BoxStyle { Height = 100 });

    [Theory]
    [MemberData(nameof(Cases))]
    public void AComponentsOwnStyle_ComesAheadOfTheThemesSlot(Case c)
    {
        Boxes(c.Plain(), Photon).Should().NotContain(r => Near(c.Read(r), c.SlotValue) || Near(c.Read(r), c.OwnValue),
            $"no {c.Component} box measures either value under Photon, so each one is the style's");
        Boxes(c.Plain(), c.Theme).Should().Contain(r => Near(c.Read(r), c.SlotValue),
            $"with no style of its own, the {c.Component} draws the slot's {c.SlotValue}");

        var own = Boxes(c.Own(), c.Theme);
        own.Should().Contain(r => Near(c.Read(r), c.OwnValue), $"the {c.Component}'s own {c.OwnValue} wins");
        own.Should().NotContain(r => Near(c.Read(r), c.SlotValue), $"the slot's {c.SlotValue} is laid under it");
    }

    /// <summary>What the instance's style leaves null is the slot's: a radio group that names only its dot
    /// keeps the ring the theme's slot names.</summary>
    [Fact]
    public void AMemberTheOwnStyleLeavesNull_IsTheSlots()
    {
        var theme = Photon.With(selectionControls: new SelectionControlsTheme { RadioSize = new DensityPair { Comfortable = 29 } });
        var group = new RadioGroup(["Card", "Cash"], 0, _ => { },
            style: new SelectionControlsTheme { RadioDot = new DensityPair { Comfortable = 13 } });

        var boxes = Boxes(group, theme);
        boxes.Should().Contain(r => Near(r.Width, 13) && Near(r.Height, 13), "the group's own dot");
        boxes.Should().Contain(r => Near(r.Width, 29) && Near(r.Height, 29), "the ring the slot names");
    }

    private static bool Near(float a, float b) => MathF.Abs(a - b) < 0.5f;

    private static List<Rect> Boxes(VisualNode node, IAppTheme theme)
    {
        var result = PhotonRealizer.Realize(node, 400, 400, theme, ThemeMode.Light, ContrastLevel.Standard,
            new DisplayListBuilder(), density: Density.Comfortable);
        var found = new List<Rect>();
        Walk(result.Root, found);
        return found;
    }

    private static void Walk(LayoutNode node, List<Rect> found)
    {
        found.Add(node.Bounds);
        foreach (var child in node.Children) Walk(child, found);
    }
}
