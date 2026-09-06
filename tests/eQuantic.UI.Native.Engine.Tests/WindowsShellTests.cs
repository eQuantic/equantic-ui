using System.Runtime.Versioning;
using eQuantic.UI.Build;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Shell.Windows;
using eQuantic.UI.Native.Shell.Windows.Graphics;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Microsoft.Win32;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The Windows shell against the REAL system — the same bar the Apple shells are held to. Every COM
/// call in the shell goes through a hand-numbered vtable slot, and a wrong slot is not an error the
/// compiler sees: it is a call into the neighbouring function. So each wrapper is exercised here
/// end to end (DirectWrite measures and rasters, Direct2D fills and strokes, WIC decodes) with
/// assertions on what came back, on the one machine kind that can answer.
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsShellTests
{
    private static TypeStyle Body => PhotonTheme.Instance.Type(TypeRole.BodyM);

    // ---- DirectWrite: measure ---------------------------------------------------------------------

    [WindowsFact]
    public void MeasuresRealGlyphs_AndWrapsAtTheWidth()
    {
        using var text = new DirectWriteTextService();
        const string sentence = "The quick brown fox jumps over the lazy dog";

        var single = text.Measure(sentence, Body, 1f, float.PositiveInfinity, 0);
        single.Lines.Should().HaveCount(1);
        single.Width.Should().BeGreaterThan(100, "a forty-character sentence is wider than a hundred dp");
        single.Height.Should().Be(Body.LineHeight);

        var wrapped = text.Measure(sentence, Body, 1f, 120, 0);
        wrapped.Lines.Count.Should().BeGreaterThan(1, "the sentence cannot fit one 120dp line");
        wrapped.Lines.Should().OnlyContain(line => line.Width > 0 && line.Width <= 120.5f);
        wrapped.Height.Should().Be(wrapped.Lines.Count * Body.LineHeight, "lines sit on the style's grid");
    }

    [WindowsFact]
    public void MaxLinesReportsTheCut()
    {
        using var text = new DirectWriteTextService();
        var cut = text.Measure("The quick brown fox jumps over the lazy dog", Body, 1f, 120, 1);
        cut.Lines.Should().HaveCount(1);
        cut.Lines[0].Ellipsized.Should().BeTrue("more lines existed than were shown");
    }

    [WindowsFact]
    public void AnEmptyStringMeasuresOneEmptyLine()
    {
        using var text = new DirectWriteTextService();
        var empty = text.Measure("", Body, 1f, float.PositiveInfinity, 0);
        empty.Lines.Should().ContainSingle().Which.Width.Should().Be(0);
        empty.Height.Should().Be(Body.LineHeight);
    }

    [WindowsFact]
    public void TheSystemFacesResolve()
    {
        using var text = new DirectWriteTextService();
        text.TextFamily.Should().NotBeNullOrEmpty();
        text.MonoFamily.Should().NotBeNullOrEmpty();
    }

    // ---- DirectWrite + Direct2D: raster ----------------------------------------------------------

    [WindowsFact]
    public void RastersCoverage_AtDeviceScale_AndKeepsTheDescenders()
    {
        using var text = new DirectWriteTextService();
        const string content = "gjpqy Ágüé";
        const float scale = 2f;
        var measured = text.Measure(content, Body, 1f, float.PositiveInfinity, 0);
        var raster = text.Rasterize(content, Body, 1f, float.PositiveInfinity, 0, scale);

        raster.Should().NotBeNull();
        raster!.Width.Should().BeInRange((int)(measured.Width * scale) - 1, (int)(measured.Width * scale) + 3,
            "the raster is the measured width, at device scale");
        raster.Height.Should().BeGreaterThanOrEqualTo((int)(Body.LineHeight * scale),
            "the bitmap holds at least the line box");
        raster.Alpha.Should().Contain(value => value > 200, "real glyphs have solid cores");

        // A descender's ink lands BELOW the text's centre line — the rows past the middle carry it.
        var lastInkRow = -1;
        for (var y = raster.Height - 1; y >= 0 && lastInkRow < 0; y--)
            for (var x = 0; x < raster.Width; x++)
                if (raster.Alpha[y * raster.Width + x] > 0) { lastInkRow = y; break; }
        lastInkRow.Should().BeGreaterThan(raster.Height / 2, "g, j, p, q and y hang below the baseline");
    }

    [WindowsFact]
    public void ARasterOfNothingIsNull()
    {
        using var text = new DirectWriteTextService();
        text.Rasterize("", Body, 1f, float.PositiveInfinity, 0, 1f).Should().BeNull();
    }

    // ---- Direct2D: icons ----------------------------------------------------------------------------

    [WindowsFact]
    public void FillsAndStrokesAnIconPath()
    {
        using var icons = new Direct2DIconRasterizer();
        var square = new IconGlyph("square", "M4 4H20V20H4Z");

        var filled = icons.Rasterize(square, 24, 24, 1f);
        filled.Should().NotBeNull();
        filled!.Width.Should().Be(24);
        filled.Alpha[12 * 24 + 12].Should().Be(255, "the centre of a filled square is solid");
        filled.Alpha[0].Should().Be(0, "the corner outside the path is clear");

        var stroked = icons.Rasterize(square with { Style = IconGlyphStyle.Stroke, StrokeWidth = 2 }, 24, 24, 1f);
        stroked.Should().NotBeNull();
        stroked!.Alpha[12 * 24 + 12].Should().Be(0, "a stroked square is hollow");
        stroked.Alpha[12 * 24 + 4].Should().BeGreaterThan(128, "the left edge carries the stroke");
    }

    // ---- WIC: images ----------------------------------------------------------------------------------

    [WindowsFact]
    public void DecodesWhatThePngCodecEncoded_AsStraightRgba()
    {
        var rgba = new byte[]
        {
            255, 0, 0, 255,   0, 255, 0, 255,   0, 0, 255, 255,
            10, 20, 30, 128,  255, 255, 255, 0,  40, 50, 60, 255,
        };
        var png = PngCodec.Encode(3, 2, rgba);

        using var loader = new WicImageLoader();
        var image = loader.Decode(png);

        image.Should().NotBeNull();
        image!.Width.Should().Be(3);
        image.Height.Should().Be(2);
        image.Rgba.Should().Equal(rgba, "PNG is straight alpha and so is the engine's contract — nothing to round");
    }

    [WindowsFact]
    public void LoadsADataUri_AndAnswersNullForNonsense()
    {
        var png = PngCodec.Encode(1, 1, [7, 8, 9, 255]);
        using var loader = new WicImageLoader();
        loader.Load("data:image/png;base64," + Convert.ToBase64String(png))!.Rgba.Should().Equal(7, 8, 9, 255);
        loader.Decode([1, 2, 3, 4, 5]).Should().BeNull();
        loader.Load(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid() + ".png")).Should().BeNull();
    }

    // ---- Keys and wheel --------------------------------------------------------------------------------

    [WindowsTheory]
    [InlineData(0x0Du, "Enter")]
    [InlineData(0x09u, "Tab")]
    [InlineData(0x08u, "Backspace")]
    [InlineData(0x1Bu, "Escape")]
    [InlineData(0x20u, " ")]
    [InlineData(0x25u, "ArrowLeft")]
    [InlineData(0x28u, "ArrowDown")]
    [InlineData(0x70u, "F1")]
    [InlineData(0x7Bu, "F12")]
    [InlineData(0x41u, "a")]
    [InlineData(0x4Bu, "k")]
    [InlineData(0x31u, "1")]
    [InlineData(0x6Au, "*")]
    public void KeysSpeakTheDomsNames(uint virtualKey, string expected) =>
        WindowsKeys.NameOf(virtualKey).Should().Be(expected);

    [Fact]
    public void AWheelNotchScrollsTheSystemsLines_UpIsNegative()
    {
        WindowsKeys.WheelTravel(120, 3).Should().Be(-3 * Touch.WheelLine);
        WindowsKeys.WheelTravel(-120, 3).Should().Be(3 * Touch.WheelLine);
        WindowsKeys.WheelTravel(60, 3).Should().Be(-1.5f * Touch.WheelLine, "a precision touchpad reports fractions of a notch");
        WindowsKeys.WheelTravel(120, uint.MaxValue).Should().BeLessThan(0, "page scrolling still has a direction");
    }

    [Fact]
    public void AChordsCharacterIsNeverTyped()
    {
        WindowsKeys.TypedText('a', KeyModifiers.Command).Should().BeEmpty();
        WindowsKeys.TypedText('\b', KeyModifiers.None).Should().BeEmpty();
        WindowsKeys.TypedText('a', KeyModifiers.Shift).Should().Be("a");
    }

    // ---- Clipboard ----------------------------------------------------------------------------------------

    [WindowsFact]
    public void TheClipboardRoundTripsText()
    {
        var clipboard = new WindowsClipboard();
        var before = clipboard.Read();
        try
        {
            clipboard.Write("héllo, 🌍");
            clipboard.Read().Should().Be("héllo, 🌍");
        }
        finally
        {
            if (before is not null) clipboard.Write(before);
        }
    }

    // ---- Storage and secrets -----------------------------------------------------------------------------

    [WindowsFact]
    public void AppStorageLivesInTheRegistry_UnderTheAppsOwnKey()
    {
        var key = @"Software\eQuantic.UI.Tests\" + Guid.NewGuid().ToString("N");
        var storage = new WindowsAppStorage(key);
        try
        {
            storage.Get("theme").Should().BeNull();
            storage.Set("theme", "dark");
            storage.Get("theme").Should().Be("dark");
            using (var written = Registry.CurrentUser.OpenSubKey(key))
                written!.GetValue("theme").Should().Be("dark", "it is an ordinary REG_SZ any tool can read");
            storage.Remove("theme");
            storage.Get("theme").Should().BeNull();
            storage.Remove("theme");   // forgetting twice is fine
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\eQuantic.UI.Tests", throwOnMissingSubKey: false);
        }
    }

    [WindowsFact]
    public void SecretsAreProtected_AndNeverOnDiskInTheClear()
    {
        var folder = Path.Combine(Path.GetTempPath(), "eq-secrets-" + Guid.NewGuid().ToString("N"));
        var vault = new WindowsSecretStore(folder);
        try
        {
            vault.Get("token").Should().BeNull();
            vault.Set("token", "eyJhbGciOiJSUzI1NiJ9.a-refresh-token-nobody-should-read");
            vault.Get("token").Should().Be("eyJhbGciOiJSUzI1NiJ9.a-refresh-token-nobody-should-read");

            var files = Directory.GetFiles(folder);
            files.Should().ContainSingle();
            var bytes = File.ReadAllBytes(files[0]);
            System.Text.Encoding.UTF8.GetString(bytes).Should().NotContain("refresh-token",
                "what is on disk is the DPAPI blob, not the secret");

            vault.Remove("token");
            vault.Get("token").Should().BeNull();
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    // ---- Locale, theme, workspace, deep links ------------------------------------------------------------

    [WindowsFact]
    public void TheLocaleResolvesToRealCultures()
    {
        var (ui, format) = WindowsLocale.Resolve();
        ui.Name.Should().NotBeNull();
        format.Name.Should().NotBeNull();
    }

    [WindowsFact]
    public void TheSystemModeIsOneOfTheTwo() =>
        WindowsTheme.SystemMode().Should().BeOneOf(ThemeMode.Light, ThemeMode.Dark);

    [WindowsFact]
    public void TheWorkspaceRefusesRelativePaths_AndAnswersFalseForWhatIsGone()
    {
        // The web policy and no logger: what the container hands it, minus the app's own schemes.
        var workspace = new WindowsWorkspace(OpenUrlPolicy.Web,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WindowsWorkspace>.Instance);
        var reveal = () => workspace.Reveal("relative/path");
        reveal.Should().Throw<ArgumentException>();
        var open = () => workspace.OpenUrl(new Uri("about", UriKind.Relative));
        open.Should().Throw<ArgumentException>();

        workspace.Reveal(Path.Combine(Path.GetTempPath(), "gone-" + Guid.NewGuid())).Should().BeFalse();
        workspace.OpenFile(Path.Combine(Path.GetTempPath(), "gone-" + Guid.NewGuid() + ".txt")).Should().BeFalse();
        workspace.CanOpen(new Uri("https://equantic.tech")).Should().BeTrue("a browser always claims https");
        workspace.CanOpen(new Uri("nobody-claims-this-" + Guid.NewGuid().ToString("N")[..8] + "://x")).Should().BeFalse();
    }

    [WindowsFact]
    public void ASchemeIsRegisteredPerUser_AndForwardingFindsNoStranger()
    {
        var scheme = "eqtest" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            WindowsDeepLinks.RegisterScheme(scheme);
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + scheme);
            key.Should().NotBeNull();
            key!.GetValue("URL Protocol").Should().Be("");
            using var command = key.OpenSubKey(@"shell\open\command");
            (command!.GetValue(null) as string).Should().Contain("\"%1\"");
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\" + scheme, throwOnMissingSubKey: false);
        }

        WindowsDeepLinks.ForwardToRunningInstance("EQPhoton.NoSuchApp." + Guid.NewGuid().ToString("N"),
            new Uri("acme://activate")).Should().BeFalse();
    }

    /// <summary>A name that is not a scheme registers NOTHING: a class Windows would never match
    /// looks like a link handler and is not.</summary>
    [WindowsFact]
    public void ANameThatIsNotASchemeIsNotRegistered()
    {
        var bogus = "not a scheme " + Guid.NewGuid().ToString("N")[..6];
        WindowsDeepLinks.RegisterScheme(bogus);
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + bogus);
        key.Should().BeNull();
        WindowsDeepLinks.RegisterScheme("   ");   // and nothing to register is not an error
    }

    // ---- The window itself -----------------------------------------------------------------------------------

    /// <summary>
    /// A REAL window, for two frames: the class registers, the HWND is created and placed, the
    /// presenter is chosen (Vulkan where a driver exists, the Reference backend where none does),
    /// DirectWrite lays the tree out, frames present, and the window closes. The self-test path
    /// every shell offers — here run from a test, on whatever thread xUnit gives it.
    /// </summary>
    [WindowsFact]
    public void OpensAWindow_PresentsFrames_AndCloses()
    {
        var window = new PhotonWindow("EQPhoton.Test." + Guid.NewGuid().ToString("N"), "eQuantic test", 420, 320);
        var root = new Column(gap: Space.S2);
        root.Add(new Text("Hello from Windows", TypeRole.Heading, PhotonTheme.Instance.TextPrimary));
        root.Add(new Pressable(new Text("Press", TypeRole.Label, PhotonTheme.Instance.TextPrimary), () => { }));

        window.Run(root, PhotonTheme.Instance, ThemeMode.Light, maxFrames: 2);

        window.FramesPresented.Should().BeGreaterThanOrEqualTo(2);
        window.PresenterName.Should().NotBe("nothing yet");
    }
}

/// <summary>The .ico writer is pure bytes and runs anywhere; what a Windows shell would read is
/// checked by parsing the directory it wrote.</summary>
public class WindowsIconTests
{
    [Fact]
    public void WritesADirectoryWindowsCanRead()
    {
        const int size = 256;
        var rgba = new byte[size * size * 4];
        for (var i = 0; i < rgba.Length; i += 4) { rgba[i] = 30; rgba[i + 1] = 144; rgba[i + 2] = 255; rgba[i + 3] = 255; }
        var path = Path.Combine(Path.GetTempPath(), "eq-icon-" + Guid.NewGuid().ToString("N") + ".ico");
        try
        {
            WindowsIcons.Write(path, size, rgba);
            var bytes = File.ReadAllBytes(path);

            BitConverter.ToUInt16(bytes, 0).Should().Be(0, "reserved");
            BitConverter.ToUInt16(bytes, 2).Should().Be(1, "type: icon");
            var count = BitConverter.ToUInt16(bytes, 4);
            count.Should().Be(8);

            var seen = new List<int>();
            for (var i = 0; i < count; i++)
            {
                var entry = 6 + 16 * i;
                var width = bytes[entry] == 0 ? 256 : bytes[entry];
                seen.Add(width);
                BitConverter.ToUInt16(bytes, entry + 6).Should().Be(32, "bits per pixel");
                var length = BitConverter.ToInt32(bytes, entry + 8);
                var offset = BitConverter.ToInt32(bytes, entry + 12);
                (offset + length).Should().BeLessThanOrEqualTo(bytes.Length, "every image lies inside the file");
                if (width == 256)
                    bytes.AsSpan(offset, 8).ToArray().Should().Equal([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A],
                        "the 256 travels as a PNG");
                else
                {
                    BitConverter.ToUInt32(bytes, offset).Should().Be(40, "a DIB starts with its header size");
                    BitConverter.ToInt32(bytes, offset + 8).Should().Be(width * 2, "XOR and AND planes stacked");
                }
            }
            seen.Should().Contain([16, 32, 48, 256]);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
