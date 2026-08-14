using eQuantic.UI.Codegen;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The writers every generated file goes through. One indentation engine, and a writer per FILE
/// TYPE on top — which is the whole reason a plist comes out tab-indented and a JSON document comes
/// out with its commas in the right places without either one knowing about the other.
/// </summary>
public class CodeWriterTests
{
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
    public void TheWebManifestNamesTheAppAndItsInstallIcons()
    {
        var dir = Path.Combine(Path.GetTempPath(), "eq-icon-" + Guid.NewGuid().ToString("N"));
        try
        {
            // A flat field is enough: what is under test is the SET that gets written, not the art.
            const int source = 512;
            var rgba = new byte[source * source * 4];
            for (var i = 0; i < rgba.Length; i += 4)
            {
                rgba[i] = 10; rgba[i + 1] = 60; rgba[i + 2] = 140; rgba[i + 3] = 255;
            }

            eQuantic.UI.Build.WebIcons.Write(dir, "Wallet", source, rgba);

            // Each answers a different question — the tab, the pinned home screen, the install.
            new[] { "favicon-32.png", "apple-touch-icon.png", "icon-192.png", "icon-512.png", "site.webmanifest" }
                .Should().OnlyContain(file => File.Exists(Path.Combine(dir, file)));

            var manifest = File.ReadAllText(Path.Combine(dir, "site.webmanifest"));
            manifest.Should().Contain("\"name\" : \"Wallet\"").And.Contain("\"192x192\"").And.Contain("\"512x512\"");

            // The downscale AVERAGES rather than point-samples, so a flat field stays exactly flat.
            var small = PngCodec.Decode(File.ReadAllBytes(Path.Combine(dir, "favicon-32.png")));
            small.Width.Should().Be(32);
            small.Rgba.Take(4).Should().Equal([(byte)10, (byte)60, (byte)140, (byte)255]);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
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

    [Fact]
    public void AScopeCarriesItsOwnCloser_SoAnOpenerCannotBeLeftOpen()
    {
        // The characteristic bug in generated output is a brace that never closes, or closes at the
        // wrong depth. A `using` makes both unrepresentable — including for a language of tags.
        var writer = new CodeWriter();
        using (writer.BeginBlock("class Wallet {"))
        {
            writer.AppendLine("balance = 0;");
            using (writer.BeginBlock("total() {")) writer.AppendLine("return this.balance;");
        }

        writer.ToString().Should().Be("""
            class Wallet {
                balance = 0;
                total() {
                    return this.balance;
                }
            }
            """.ReplaceLineEndings() + System.Environment.NewLine);
        writer.IndentLevel.Should().Be(0, "every scope put back what it took");
    }

    [Fact]
    public void TheLineCounterCountsWhatWasActuallyWritten_NotHowManyCallsWereMade()
    {
        // CurrentLine is what a source map anchors a generated position against, and a writer is
        // routinely handed text that is already several lines — a converted method body, a doc
        // comment. Counting each call as one line shifted every mapping after it, which reads in a
        // debugger as breakpoints landing on the wrong statement.
        var writer = new CodeWriter();
        writer.AppendLine("const a = 1;");
        writer.AppendLine("const b = () => {\n    return 2;\n};");
        writer.Append("const c =\n    3;\n");

        var written = writer.ToString().ReplaceLineEndings("\n").TrimEnd('\n').Split('\n').Length;
        writer.CurrentLine.Should().Be(written + 1, "the counter names the line the NEXT write lands on");
    }
}
