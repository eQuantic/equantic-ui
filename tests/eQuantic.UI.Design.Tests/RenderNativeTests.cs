using eQuantic.UI.Design;
using FluentAssertions;

namespace eQuantic.UI.Design.Tests;

/// <summary>
/// One Photon frame of the buffer, rendered by the <c>eqphoton</c> child process.
/// <para>
/// The chain under test is the whole point of the feature: the buffer's REAL compilation emitted to
/// an assembly, loaded in a process of its own, the page instantiated, realized by
/// <see cref="eQuantic.UI.Native.Components.PhotonHost"/>, rasterized by the Reference backend, and
/// handed back as a PNG. Every link is exercised — a mock anywhere would leave the seam it mocks
/// unproven, and the seams are where this feature can break.
/// </para>
/// </summary>
public sealed class RenderNativeTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "eq-native-" + Guid.NewGuid().ToString("N"));
    private readonly string _probe;
    private readonly DesignSession _session = new();

    private const string Source = """
        using eQuantic.UI.Core;
        using eQuantic.UI.Primitives;
        using StatefulComponent = eQuantic.UI.Primitives.StatefulComponent;

        [Page("/probe")]
        public sealed class Probe : StatefulComponent
        {
            public override VisualNode Build(ComponentContext context)
            {
                var page = new Column(gap: Space.S3) { Width = SizeValue.Fill };
                page.Add(new Text("Photon", TypeRole.Heading, context.Theme.TextPrimary));
                page.Add(new Box(new BoxStyle
                {
                    Width = SizeValue.Fill,
                    Height = SizeValue.Fixed(120),
                    Background = context.Theme.Surface,
                    CornerRadius = new CornerRadii(12),
                    BorderWidth = 1,
                    BorderColor = context.Theme.Border,
                }));
                return new Box(new BoxStyle
                {
                    Padding = EdgeInsets.All(Space.S4),
                    Background = context.Theme.Background,
                }, page);
            }
        }
        """;

    public RenderNativeTests()
    {
        Directory.CreateDirectory(Path.Combine(_directory, "Screens"));
        _probe = Path.Combine(_directory, "Screens", "Probe.cs");
        File.WriteAllText(_probe, Source);
        File.WriteAllText(Path.Combine(_directory, "Probe.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var references = new[]
            {
                typeof(eQuantic.UI.Primitives.VisualNode).Assembly,
                typeof(eQuantic.UI.Core.PageAttribute).Assembly,
                typeof(object).Assembly,
            }
            .Concat(AppDomain.CurrentDomain.GetAssemblies())
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            .Select(assembly => assembly.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var refsFile = Path.Combine(_directory, "equantic.refs.txt");
        File.WriteAllLines(refsFile, references);
        _session.Initialize(_directory, refsFile, generatedDir: null);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void ABufferRendersToAPhotonFrame()
    {
        var frame = _session.RenderNative(_probe, Source, 390, 844, "comfortable");

        frame.Success.Should().BeTrue(frame.Reason);
        frame.Png.Should().NotBeNullOrEmpty();

        // A PNG, of the size that was asked for — read out of the BYTES, not trusted from the reply:
        // the signature, then IHDR's width and height at fixed offsets.
        var bytes = Convert.FromBase64String(frame.Png!);
        bytes.Take(8).Should().Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        ReadBigEndian(bytes, 16).Should().Be(390);
        ReadBigEndian(bytes, 20).Should().Be(844);

        if (OperatingSystem.IsMacOS())
        {
            frame.TextService.Should().BeTrue("CoreText is available on this host, so real glyphs must be");
        }
    }

    /// <summary>
    /// The hit map that makes the frame clickable: every rendered node carries the C# span that
    /// built it, and that origin is the SAME identity the inspector answers to — so a click on the
    /// picture and a click on the living web canvas open the same panel, reveal the same line.
    /// </summary>
    [Fact]
    public void TheFrameCarriesAHitMap_WhoseOriginsAnswerToTheInspector()
    {
        var frame = _session.RenderNative(_probe, Source, 390, 844, "comfortable");

        frame.Success.Should().BeTrue(frame.Reason);
        frame.Nodes.Should().NotBeNullOrEmpty("a frame nobody can click is a screenshot, not a canvas");
        frame.Nodes.Should().OnlyContain(node => node.Origin.StartsWith(_probe),
            "every origin must point into the buffer that was rendered");
        frame.Nodes.Should().OnlyContain(node => node.Width > 0 && node.Height > 0,
            "a zero-sized entry could never be hit, so it has no business on the map");

        // Paint order is the hit-test's contract: the map must not be empty of nesting — the page
        // Box comes before the Column it contains, so "last containing the point" finds the child.
        var box = frame.Nodes!.First(node => node.Name == "Box");
        var text = frame.Nodes!.First(node => node.Name == "Text");
        Array.IndexOf(frame.Nodes, box).Should().BeLessThan(Array.IndexOf(frame.Nodes, text));

        // The full circle, the thing the whole feature hangs on: a node picked off the map, its
        // origin handed to the inspector, and the inspector describing that same component.
        var inspected = _session.Inspect(_probe, Source, text.Origin);
        inspected.Should().NotBeNull();
        inspected!.Component.Should().Be("Text");
    }

    /// <summary>The frame shows the BUFFER, not the file: the same reason the whole design host
    /// exists. A hostile-looking probe: the text differs from what is on disk.</summary>
    [Fact]
    public void TheFrameRendersTheBufferNotTheDisk()
    {
        var edited = Source.Replace("Height = SizeValue.Fixed(120)", "Height = SizeValue.Fixed(9)");

        var frame = _session.RenderNative(_probe, edited, 200, 200, "comfortable");

        frame.Success.Should().BeTrue(frame.Reason);
    }

    [Fact]
    public void AFileWithNoComponent_IsRefusedWithTheReason()
    {
        var frame = _session.RenderNative(_probe, "public static class Helpers { }", 390, 844, null);

        frame.Success.Should().BeFalse();
        frame.Reason.Should().Contain("no component");
    }

    [Fact]
    public void ABufferThatDoesNotCompile_IsRefusedWithTheCompilersWords()
    {
        var frame = _session.RenderNative(_probe, Source.Replace("return", "retrun"), 390, 844, null);

        frame.Success.Should().BeFalse();
        frame.Reason.Should().Contain("CS");
    }

    /// <summary>
    /// THE safety property, and the reason the renderer is a process: a Build() that never returns
    /// takes down something disposable, and the host answers with a sentence instead of hanging the
    /// preview. Ten real seconds — expensive for a test, cheap for the promise it pins.
    /// </summary>
    [Fact]
    public void APageWhoseBuildNeverReturns_IsKilledAndExplained()
    {
        var hostile = Source.Replace(
            "var page = new Column(gap: Space.S3) { Width = SizeValue.Fill };",
            "while (true) { } var page = new Column(gap: Space.S3) { Width = SizeValue.Fill };");

        var frame = _session.RenderNative(_probe, hostile, 100, 100, null);

        frame.Success.Should().BeFalse();
        frame.Reason.Should().Contain("10 seconds");
    }

    private static int ReadBigEndian(byte[] bytes, int at) =>
        (bytes[at] << 24) | (bytes[at + 1] << 16) | (bytes[at + 2] << 8) | bytes[at + 3];
}
