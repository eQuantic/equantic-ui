using eQuantic.UI.Codegen;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The app icon declared in C# — a tree, rendered by the same engine that draws the app, into the
/// catalog its platform installs from. The sample ships a designer's artwork instead, which is the
/// other half of the same contract; this is what keeps the drawn half honest.
/// </summary>
public class AppIconTests
{
    private sealed class BrandIcon : IAppIcon
    {
        public VisualNode Build(ComponentContext context)
        {
            var brand = context.Theme.Colors(Variant.Primary);
            var field = new Box(new BoxStyle
            {
                Width = SizeValue.Fill,
                Height = SizeValue.Fill,
                Background = brand.Base,
            });

            var stack = new Stack();
            stack.Add(field);
            stack.Add(new Text("W", TypeRole.Display, brand.OnBase).Centered());
            return stack;
        }
    }

    [Fact]
    public void AnIconFillsItsCanvasEdgeToEdge_AndComesOutOpaque()
    {
        // Every platform masks the icon and composites it against whatever the launcher is showing,
        // so a corner that is not painted is a corner that shows the wallpaper through it.
        const int size = 128;
        const float canvas = 64f;
        var theme = PhotonTheme.Instance;
        var host = new PhotonHost(new BrandIcon().Build(new ComponentContext(theme)),
            theme, ThemeMode.Light, canvas, canvas)
        {
            RenderScale = size / canvas,
        };

        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(size, size);
        backend.Render(builder.Build(), surface);

        var rgba = new byte[size * size * 4];
        surface.ReadPixelsSrgb(rgba);

        var brand = theme.Colors(Variant.Primary).Base.Resolve(ThemeMode.Light);
        Pixel(rgba, size, 0, 0).Should().Be((brand.R, brand.G, brand.B), "the top-left corner is painted");
        Pixel(rgba, size, size - 1, size - 1).Should().Be((brand.R, brand.G, brand.B));
        Pixel(rgba, size, size / 2, size / 2).Should().NotBe((brand.R, brand.G, brand.B),
            "the mark sits in the middle of the field");
    }

    private static (byte R, byte G, byte B) Pixel(byte[] rgba, int stride, int x, int y)
    {
        var index = (y * stride + x) * 4;
        return (rgba[index], rgba[index + 1], rgba[index + 2]);
    }

    // ---- The files the SDK writes around it -----------------------------------------------------

    [Fact]
    public void TheAssetCatalogIsWrittenWithTheCommasInTheRightPlaces()
    {
        // A JSON writer earns its keep on exactly one thing: the last member never gets a comma and
        // every other member does.
        var json = JsonWriter.Document(document => document
            .Array("images", images => images.Element(image => image
                .String("filename", "AppIcon.png")
                .String("size", "1024x1024")))
            .Object("info", info => info
                .String("author", "eQuantic.UI")
                .Number("version", 1)));

        json.TrimEnd().Should().Be("""
            {
                "images" : [
                    {
                        "filename" : "AppIcon.png",
                        "size" : "1024x1024"
                    }
                ],
                "info" : {
                    "author" : "eQuantic.UI",
                    "version" : 1
                }
            }
            """.ReplaceLineEndings());
    }

    [Fact]
    public void ThePropertyListIsTabIndentedLikeEveryOtherPlistOnTheMachine()
    {
        var plist = PropertyListWriter.Document(dict => dict
            .EmptyDictionary("UILaunchScreen")
            .StringArray("UIRequiredDeviceCapabilities", "metal"));

        plist.Should().Contain("\t<key>UILaunchScreen</key>\n\t<dict/>")
             .And.Contain("\t<array>\n\t\t<string>metal</string>\n\t</array>");
    }
}
