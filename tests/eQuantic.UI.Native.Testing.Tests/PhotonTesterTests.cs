using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using Xunit;
using static eQuantic.UI.Components.UI;

namespace eQuantic.UI.Native.Testing.Tests;

/// <summary>
/// Every scenario of the <c>component-testing</c> spec's Photon requirements, through the package's
/// public surface alone, as an app's test project reaches it.
/// </summary>
public class PhotonTesterTests
{
    private const string Sentence = "The quick brown fox jumps over the lazy dog near the quiet river bank";

    [Fact]
    public void ATextsLines_AreTheOnesItsWidthAllows()
    {
        foreach (var (width, wraps) in new[] { (1280f, false), (160f, true) })
        {
            using var tester = new PhotonTester(width: width, height: 600);
            tester.Pump(Column(children: [Text(Sentence)]));

            var sentence = tester.Find(Sentence);
            Assert.NotNull(sentence.Text);
            if (wraps) Assert.True(sentence.Text!.Lines.Count > 1, $"At {width} dp the sentence measured {sentence}.");
            else Assert.Single(sentence.Text!.Lines);
            Assert.True(sentence.Bounds.X >= 0 && sentence.Bounds.Right <= width, $"{sentence} leaves a {width} dp viewport.");
        }
    }

    [Fact]
    public void TheDensityATestNames_IsTheOneTheControlsAreLaidOutAt()
    {
        float HeightAt(Density density)
        {
            using var tester = new PhotonTester(width: 400, height: 300, density: density);
            tester.Pump(Column(children: [Button("Save", onPressed: () => { })]));
            return tester.Find(SemanticRole.Button, "Save").Bounds.Height;
        }

        Assert.True(HeightAt(Density.Compact) < HeightAt(Density.Comfortable),
            $"Compact {HeightAt(Density.Compact)} dp, Comfortable {HeightAt(Density.Comfortable)} dp.");
    }

    public sealed class Counter : StatefulComponent
    {
        private int _count = 1;

        public void Grow() => SetState(() => _count = 1_000_000);

        public override VisualNode Build(ComponentContext context) => Column(children: [Text($"Count: {_count}")]);
    }

    [Fact]
    public void AMeasurementHeldAcrossAFrame_KeepsWhatItMeasured()
    {
        using var tester = new PhotonTester(width: 400, height: 300);
        var counter = new Counter();
        tester.Pump(counter);
        var before = tester.Find("Count: 1");
        var firstRoot = tester.Root;

        counter.Grow();
        Assert.True(tester.HasScheduledFrame);
        tester.Pump();

        // A text in a column takes the column's width, so the box holds still: the LINE is what grew.
        var after = tester.Find("Count: 1000000");
        Assert.True(after.Text!.Lines[0].Width > before.Text!.Lines[0].Width, $"Before {before}, after {after}.");
        Assert.Equal("Count: 1", Texts(firstRoot).Single());
        Assert.Same(before.Text, Find(firstRoot, "Count: 1").Text);
        Assert.Equal("Count: 1000000", Texts(tester.Root).Single());
        Assert.False(tester.HasScheduledFrame);
    }

    [Fact]
    public void AButton_IsFoundByItsLabel()
    {
        using var tester = new PhotonTester(width: 400, height: 300);
        tester.Pump(Card(Column(gap: Space.S2, children:
        [
            Text("Ana"),
            Button("Sign up", onPressed: () => { }),
        ])));

        var button = tester.Find("Sign up");
        var card = tester.Root.Bounds;
        Assert.True(button.Bounds.Width > 0 && button.Bounds.Height > 0, button.ToString());
        Assert.True(button.Bounds.X >= card.X && button.Bounds.Right <= card.Right
                    && button.Bounds.Y >= card.Y && button.Bounds.Bottom <= card.Bottom,
            $"{button} lies outside the card at {card}.");
    }

    [Fact]
    public void ANameNothingAnnounces_FailsWithWhatIsAnnounced()
    {
        using var tester = new PhotonTester(width: 400, height: 300);
        tester.Pump(Column(children: [Text("Ana"), Button("Sign up", onPressed: () => { })]));

        var failure = Assert.Throws<InvalidOperationException>(() => tester.Find("Log out"));
        Assert.Contains("\"Log out\"", failure.Message);
        Assert.Contains("\"Sign up\"", failure.Message);
        Assert.Contains("\"Ana\"", failure.Message);
    }

    [Fact]
    public void ANameTwoNodesAnnounce_FailsAndAsksForTheRole_WhichTellsThemApart()
    {
        using var tester = new PhotonTester(width: 400, height: 300);
        tester.Pump(Column(children: [Text("Save"), Button("Save", onPressed: () => { })]));

        var failure = Assert.Throws<InvalidOperationException>(() => tester.Find("Save"));
        Assert.Contains("name the role", failure.Message);
        Assert.IsAssignableFrom<VisualNode>(tester.Find(SemanticRole.Button, "Save").Node);
        Assert.NotNull(tester.Find(SemanticRole.StaticText, "Save").Text);
    }

    public sealed class Price : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => Column(children: [Text(1234.5.ToString("N1"))]);
    }

    [Fact]
    public void TheCultureATestNames_IsTheOneAComponentFormatsWith()
    {
        var before = CultureInfo.CurrentCulture;
        using var tester = new PhotonTester(culture: new CultureInfo("pt-BR"), width: 400, height: 300);
        tester.Pump(new Price());

        Assert.NotNull(tester.Find("1.234,5").Text);
        Assert.Equal(before, CultureInfo.CurrentCulture);
    }

    [Fact]
    public void AnOpenDialog_IsLaidOutAsALayer_AndItsTextIsFoundThere()
    {
        using var tester = new PhotonTester(width: 600, height: 400);
        tester.Pump(Column(children:
        [
            Text("Behind"),
            Dialog("Delete file", "This cannot be undone.", [new DialogAction("Delete")]),
        ]));

        var layer = Assert.Single(tester.Layers);
        var body = tester.Find("This cannot be undone.");
        Assert.Contains(body, Descendants(layer));
        Assert.DoesNotContain(body, Descendants(tester.Root));
    }

    public sealed class Lifecycle : StatefulComponent
    {
        public int Unmounted { get; private set; }

        protected override void OnUnmount() => Unmounted++;

        public override VisualNode Build(ComponentContext context) => Text("Alive");
    }

    [Fact]
    public void DisposingTheTester_TakesTheTreeDown()
    {
        var component = new Lifecycle();
        var tester = new PhotonTester(width: 400, height: 300);
        tester.Pump(component);
        Assert.Equal(0, component.Unmounted);

        tester.Dispose();

        Assert.Equal(1, component.Unmounted);
        Assert.Throws<ObjectDisposedException>(() => tester.Find("Alive"));
    }

    [Fact]
    public void ThePackage_IsMarkedAsTheNativeTrackIs()
    {
        var mark = typeof(PhotonTester).Assembly.GetCustomAttribute<ExperimentalAttribute>();
        Assert.NotNull(mark);
        Assert.Equal("EQX0001", mark!.DiagnosticId);
    }

    [Fact]
    public void ThePixels_AreTheViewportsSize_AndCarryTheThemesBackground()
    {
        using var tester = new PhotonTester(width: 40, height: 30);
        tester.Pump(Column(children: [Text("Hi")]));

        var pixels = tester.ReadPixelsSrgb();
        Assert.Equal(40 * 30 * 4, pixels.Length);
        // The corner far from the text is the page's background, opaque.
        Assert.Equal(255, pixels[^1]);
    }

    private static IEnumerable<MeasuredNode> Descendants(MeasuredNode node) =>
        new[] { node }.Concat(node.Children.SelectMany(Descendants));

    private static IEnumerable<string> Texts(MeasuredNode node) =>
        Descendants(node).Where(each => each.Text is not null && each.Node is Text).Select(each => ((Text)each.Node).Content);

    private static MeasuredNode Find(MeasuredNode root, string content) =>
        Descendants(root).Single(each => each.Node is Text text && text.Content == content);
}
