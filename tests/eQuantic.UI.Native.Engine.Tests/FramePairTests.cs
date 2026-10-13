using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The handoff between the thread that writes a live frame and the one that draws it: what was
/// handed over is not written again until something else has been handed over after it.
/// </summary>
public class FramePairTests
{
    [Fact]
    public void BeforeTheFirstFrame_NothingIsPublished() =>
        new FramePair().Published.Should().BeNull();

    [Fact]
    public void TheArrayToWriteIsNeverTheOnePublished()
    {
        var frames = new FramePair();
        for (var frame = 0; frame < 20; frame++)
        {
            var written = frames.Next(16);
            written.Should().NotBeSameAs(frames.Published, "the published frame is the one a renderer may be reading");
            frames.Publish();
            frames.Published.Should().BeSameAs(written);
        }
    }

    /// <summary>What a reader was handed stays whole for a full frame of the source: the next frame
    /// is written elsewhere, and only the one after that comes back to this array.</summary>
    [Fact]
    public void AFrameHandedOver_IsNotWrittenUntilAnotherHasBeenPublishedAfterIt()
    {
        var frames = new FramePair();
        frames.Next(4)[0] = 1;
        frames.Publish();
        var handedOver = frames.Published!;

        var following = frames.Next(4);
        following[0] = 2;

        following.Should().NotBeSameAs(handedOver);
        handedOver[0].Should().Be(1, "writing the following frame did not touch the one handed over");

        frames.Publish();
        frames.Next(4).Should().BeSameAs(handedOver, "only now is it the array to write");
    }

    [Fact]
    public void ASourceThatRuns_UsesTwoArraysAndNoMore()
    {
        var frames = new FramePair();
        var seen = new HashSet<byte[]>(ReferenceEqualityComparer.Instance);
        for (var frame = 0; frame < 100; frame++)
        {
            frames.Next(64);
            frames.Publish();
            seen.Add(frames.Published!);
        }

        seen.Should().HaveCount(2, "a realizer keeps a texture per array: two, not one per frame");
    }

    /// <summary>A phone turned: the frame's size changes, and the frame still on screen is not the
    /// array the new size is written into.</summary>
    [Fact]
    public void AFrameOfAnotherSize_GetsItsOwnArray_AndThePublishedOneStays()
    {
        var frames = new FramePair();
        frames.Next(8);
        frames.Publish();
        var onScreen = frames.Published!;

        var turned = frames.Next(12);

        turned.Should().HaveCount(12).And.NotBeSameAs(onScreen);
        frames.Published.Should().BeSameAs(onScreen).And.HaveCount(8);
    }

    [Fact]
    public void PublishingWithNothingWritten_IsRefused() =>
        FluentActions.Invoking(() => new FramePair().Publish()).Should().Throw<InvalidOperationException>();
}
