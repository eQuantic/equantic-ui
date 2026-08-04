using eQuantic.UI.Native.Engine;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Dimensions from a HEADER — a few dozen bytes rather than megabytes of decoded pixels. A caller
/// wants them to decide what to do next, and decoding first would cost more than the answer.
/// </summary>
public class ImageHeaderTests
{
    [Fact]
    public void APng_ReadsItsIHDR()
    {
        var png = PngCodec.Encode(37, 11, new byte[37 * 11 * 4]);
        ImageHeader.Measure(png).Should().Be((37, 11));
    }

    [Fact]
    public void AJpeg_WalksToItsFrameHeader()
    {
        // SOI, an APP0 nobody cares about, then the SOF0 that actually says the size — which is why
        // a fixed offset does not work: what comes before depends on the camera.
        var jpeg = new byte[]
        {
            0xFF, 0xD8,
            0xFF, 0xE0, 0x00, 0x06, 0x4A, 0x46, 0x49, 0x46,
            0xFF, 0xC0, 0x00, 0x11, 0x08, 0x01, 0x2C, 0x02, 0x58, 0x03,
        };
        ImageHeader.Measure(jpeg).Should().Be((600, 300));
    }

    [Fact]
    public void AGif_ReadsItsLittleEndianScreen()
    {
        var gif = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x40, 0x01, 0xC8, 0x00, 0, 0, 0, 0, 0, 0 };
        ImageHeader.Measure(gif).Should().Be((320, 200));
    }

    [Fact]
    public void SomethingElse_AnswersZero_RatherThanThrowing()
    {
        // Not knowing how big a picture is has never been a reason to refuse to hand it over.
        ImageHeader.Measure(new byte[64]).Should().Be((0, 0));
        ImageHeader.Measure([1, 2, 3]).Should().Be((0, 0));
        ImageHeader.Measure(default).Should().Be((0, 0));
    }

    [Fact]
    public void AMalformedJpeg_StopsInsteadOfSpinning()
    {
        // A zero-length segment would walk the cursor nowhere for ever.
        var broken = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x00, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        ImageHeader.Measure(broken).Should().Be((0, 0));
    }
}
