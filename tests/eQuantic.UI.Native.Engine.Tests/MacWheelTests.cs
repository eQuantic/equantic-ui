using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using eQuantic.UI.Native.Shell.MacOS;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;
using static eQuantic.UI.Native.Shell.Apple.ObjC;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The macOS shell's wheel, read from events QUARTZ builds, the kind the system hands a window: a
/// trackpad's swipe in pixels, which AppKit reports as precise deltas, and a wheel's notch in lines.
/// The host's tests start from the travel; this is where the travel starts, the selectors, the signs
/// and the precise flag, and a swipe sideways is where it went wrong before both axes were read: the
/// window read <c>scrollingDeltaY</c> alone, and a horizontal swipe moved nothing.
/// </summary>
[SupportedOSPlatform("macos")]
public partial class MacWheelTests
{
    private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const uint UnitPixel = 0;
    private const uint UnitLine = 1;

    [LibraryImport(CoreGraphics)]
    private static partial IntPtr CGEventCreateScrollWheelEvent2(IntPtr source, uint units, uint wheelCount,
        int wheel1, int wheel2, int wheel3);

    [LibraryImport(CoreFoundation)]
    private static partial void CFRelease(IntPtr value);

    /// <summary>The scroll event AppKit makes of Quartz's: <paramref name="down"/> on the first wheel,
    /// the vertical one, and <paramref name="across"/> on the second.</summary>
    private static (float Across, float Down, bool Stepped) Read(uint units, int down, int across)
    {
        var quartz = CGEventCreateScrollWheelEvent2(IntPtr.Zero, units, 2, down, across, 0);
        quartz.Should().NotBe(IntPtr.Zero);
        try
        {
            var appKit = Send(AppKitClass("NSEvent"), Sel("eventWithCGEvent:"), quartz);
            appKit.Should().NotBe(IntPtr.Zero, "AppKit makes an NSEvent of every Quartz event");
            return MacWheel.Read(appKit);
        }
        finally
        {
            CFRelease(quartz);
        }
    }

    private static IntPtr AppKitClass(string name) => AppKit.Class(name);

    [MacFact]
    public void ATrackpadSwipe_MovesBothAxesAsItCame()
    {
        // A swipe up and to the left: the content goes up and left, so the view travels down and across.
        var (across, down, stepped) = Read(UnitPixel, down: -30, across: -50);

        stepped.Should().BeFalse("a trackpad's deltas are precise, and apply as they come");
        across.Should().Be(50);
        down.Should().Be(30);
    }

    [MacFact]
    public void ASidewaysSwipe_MovesOnlyAcross()
    {
        var (across, down, _) = Read(UnitPixel, down: 0, across: 40);

        across.Should().Be(-40);
        down.Should().Be(0);
    }

    [MacFact]
    public void AWheelNotch_StepsByLines()
    {
        var (across, down, stepped) = Read(UnitLine, down: -3, across: 0);

        stepped.Should().BeTrue("a wheel's lines glide");
        down.Should().Be(3 * Touch.WheelLine);
        across.Should().Be(0);
    }
}
