using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Photon's half of the control ladder's slot (E6a, #1241): a theme that changes how tall a medium
/// control is changes every medium control, at the density the host is in. Measured by where each
/// control landed: the rounded rectangles it paints (a fill, a border) and the bounds its semantics
/// stop carries, since a field's stop is its entry, inside the box it draws.
/// </summary>
public class TheControlLadderSlotTests
{
    private static readonly PhotonTheme Photon = PhotonTheme.Instance;

    private static readonly IAppTheme Taller = Photon.With(controls: Photon.Controls! with
    {
        Height = new DensityLadder { Comfortable = new SizeLadder { Medium = 52 }, Compact = new SizeLadder { Medium = 44 } },
    });

    private static readonly (string Name, Func<VisualNode> Build)[] MediumControls =
    [
        ("Button", () => new Button("Continue")),
        ("IconButton", () => new IconButton(new Icon(Icons.Search), "Search")),
        ("TextInput", () => new TextInput("ana@example.com", _ => { }, label: "Email", size: SizeVariant.Medium)),
        ("SegmentedControl", () => new SegmentedControl(["All", "Income"], 0, _ => { })),
        ("Stepper", () => new Stepper(2, _ => { })),
        ("Select", () => new Select(["Lisbon", "Porto"], 0, _ => { })),
        ("SearchField", () => new SearchField("harbour", _ => { })),
        ("TimePicker", () => new TimePicker(new TimeOnly(9, 30), _ => { })),
    ];

    [Theory]
    [InlineData(Density.Comfortable, 52)]
    [InlineData(Density.Compact, 44)]
    public void AThemeThatChangesTheMediumHeight_ChangesEveryMediumControl_OnPhoton(Density density, int height)
    {
        var unchanged = new List<string>();
        foreach (var (name, build) in MediumControls)
        {
            if (!Heights(build(), Taller, density).Contains(height)) unchanged.Add(name);
            Heights(build(), Photon, density).Should().NotContain(height, $"{name} is not that tall under Photon");
        }

        unchanged.Should().BeEmpty("every medium control reads the theme's height");
    }

    private static float[] Heights(VisualNode root, IAppTheme theme, Density density)
    {
        var host = new PhotonHost(root, theme, ThemeMode.Light, 480, 320) { Density = density };
        try
        {
            var builder = new DisplayListBuilder();
            host.RenderFrame(builder);
            var painted = builder.Build().Commands.ToArray()
                .Where(command => command.Kind is DrawCommandKind.FillRRect or DrawCommandKind.StrokeRRect)
                .Select(command => MathF.Round(command.Shape.Rect.Height));
            return painted.Concat(host.Semantics().Select(stop => MathF.Round(stop.Bounds.Height))).ToArray();
        }
        finally
        {
            host.Unmount();
        }
    }
}
