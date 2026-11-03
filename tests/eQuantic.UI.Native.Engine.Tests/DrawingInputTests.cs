using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The shapes of a <see cref="Drawing"/> as inputs (#674): a concelho on a map is pressed where it is
/// painted, reached by Tab, pressed by Enter, announced by its name, and hovered where a press would
/// land. Two triangles split a 200dp square along its diagonal, so each one's box holds the other's
/// half: a box test answers the wrong triangle for every point on one side.
/// </summary>
public class DrawingInputTests
{
    private const string Upper = "M0 0 L100 0 L0 100 Z";
    private const string Lower = "M100 0 L100 100 L0 100 Z";

    private sealed class Map
    {
        public readonly List<string> Pressed = [];
        public string? Chosen;

        public Drawing Draw() => new(new VectorDrawing(0, 0, 100, 100,
        [
            Shape(Upper, "Porto"),
            Shape(Lower, "Matosinhos"),
        ]), width: 200, height: 200);

        private VectorShape Shape(string path, string name) =>
            new(path, VectorPaint.Solid(Color.Black))
            {
                Label = name,
                OnPressed = () => Pressed.Add(name),
                Selected = Chosen == name,
                Hover = new StyleDiff { Background = new ColorToken(Color.White) },
            };
    }

    private static PhotonHost Host(VisualNode root)
    {
        var host = new PhotonHost(root, PhotonTheme.Instance, ThemeMode.Light, 200, 200);
        host.RenderFrame(new DisplayListBuilder());
        return host;
    }

    private static void Tap(PhotonHost host, float x, float y)
    {
        host.PressDown(x, y);
        host.RenderFrame(new DisplayListBuilder());
        host.PressUp(x, y);
        host.RenderFrame(new DisplayListBuilder());
    }

    [Fact]
    public void APressLandsOnTheShapeItIsPaintedIn_NotOnTheBoxAround_It()
    {
        var map = new Map();
        var host = Host(map.Draw());

        // Inside both boxes, inside the upper triangle's path only: the lower triangle is drawn after
        // and its box is topmost, so a box test gives it this press.
        Tap(host, 40, 40);
        Tap(host, 160, 160);

        map.Pressed.Should().Equal("Porto", "Matosinhos");
    }

    [Fact]
    public void APressIsTestedInTheDrawingsOwnBox_WhereverItIsLaidOut()
    {
        // Laid out 40dp in from the window's corner: the path is on the artwork's grid, and the press
        // is taken there from the drawing's box, not from the window's origin.
        var map = new Map();
        var inset = new Column { Padding = EdgeInsets.All(40) };
        inset.Add(map.Draw());
        var host = new PhotonHost(inset, PhotonTheme.Instance, ThemeMode.Light, 280, 280);
        host.RenderFrame(new DisplayListBuilder());

        Tap(host, 40 + 40, 40 + 40);
        Tap(host, 40 + 160, 40 + 160);
        Tap(host, 20, 20);

        map.Pressed.Should().Equal("Porto", "Matosinhos");
    }

    [Fact]
    public void AReleaseOnAnotherShapeOfTheSameDrawingCancelsThePress()
    {
        var map = new Map();
        var host = Host(map.Draw());

        host.PressDown(40, 40);
        host.RenderFrame(new DisplayListBuilder());
        host.PressUp(160, 160);

        map.Pressed.Should().BeEmpty("the two shapes share one position, and the press is the upper one's");
    }

    [Fact]
    public void TabReachesEachShapeAndEnterPressesIt()
    {
        var map = new Map();
        var host = Host(map.Draw());

        host.FocusNext().Should().BeTrue();
        host.ActivateFocused().Should().BeTrue();
        host.FocusNext();
        host.ActivateFocused();

        map.Pressed.Should().Equal("Porto", "Matosinhos");
    }

    [Fact]
    public void EachShapeIsAnnouncedAsAButtonByItsName_AndAReaderActivatesIt()
    {
        var map = new Map { Chosen = "Matosinhos" };
        var host = Host(map.Draw());
        var frame = host.RenderFrame(new DisplayListBuilder());

        var buttons = host.Semantics().Where(node => node.Role == SemanticRole.Button).ToList();
        buttons.Select(node => node.Label).Should().Equal("Porto", "Matosinhos");
        buttons.Select(node => node.Selected).Should().Equal(false, true);
        buttons[0].Bounds.Should().Be(new Rect(0, 0, 200, 200), "the hull of a triangle on its half of the square");
        buttons.Select(node => node.Id).Should().BeEquivalentTo(frame.FocusStops.Select(stop => stop.Id),
            "a reader's activation runs the shape the keyboard stops on");
        buttons[0].Id.Should().NotBe(buttons[1].Id);

        host.Activate(buttons[1].Id).Should().BeTrue();
        map.Pressed.Should().Equal("Matosinhos");
    }

    [Fact]
    public void TheHoverFollowsTheShapeAPressWouldLandOn()
    {
        var map = new Map();
        var host = Host(map.Draw());
        var ids = host.Semantics().Where(node => node.Role == SemanticRole.Button).Select(node => node.Id).ToList();

        host.PointerMove(40, 40);
        host.HoveredPart.Should().Be(ids[0]);
        host.PointerMove(160, 160);
        host.HoveredPart.Should().Be(ids[1]);
        host.PointerLeave();
        host.HoveredPart.Should().Be(0);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void AHoleIsInsideTheShapeOnlyByItsFillRule(bool evenOdd, bool pressed)
    {
        // A square with a square inside it, both drawn clockwise: nonzero fills the hole, even-odd
        // leaves it open, and the web's `visibleFill` answers the same by the same rule.
        var count = 0;
        var ring = new VectorShape("M0 0 H100 V100 H0 Z M25 25 H75 V75 H25 Z", VectorPaint.Solid(Color.Black),
            EvenOdd: evenOdd) { Label = "Ring", OnPressed = () => count++ };
        var host = Host(new Drawing(new VectorDrawing(0, 0, 100, 100, [ring]), width: 200, height: 200));

        Tap(host, 100, 100);
        Tap(host, 20, 20);

        count.Should().Be(pressed ? 2 : 1, "the band around the hole is the shape either way");
    }

    [Fact]
    public void AShapeWithNothingToDoStaysAPicture()
    {
        var host = Host(new Drawing(new VectorDrawing(0, 0, 100, 100,
            [new VectorShape(Upper, VectorPaint.Solid(Color.Black)) { Label = "Porto" }]), width: 200, height: 200));
        var frame = host.RenderFrame(new DisplayListBuilder());

        frame.HitRegions.Should().BeEmpty();
        frame.FocusStops.Should().BeEmpty();
        host.Semantics().Should().NotContain(node => node.Role == SemanticRole.Button);
    }

    [Fact]
    public void AHoverAShapeCannotDrawIsRefused()
    {
        var refused = () => new VectorShape(Upper, VectorPaint.Solid(Color.Black))
        {
            Hover = new StyleDiff { Elevation = 2 },
        };

        refused.Should().Throw<ArgumentException>().WithMessage("*fill (Background)*");
    }
}
