using eQuantic.UI.Native.Shell.Apple;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The REAL NWPathMonitor, on the machine the tests run on. Not a fake: the fake proves the
/// contract's shape, and the shape was never the risk — the risk is the dylib path, the block the
/// monitor copies, and the enum values read off the path, none of which a fake touches.
/// <para>
/// The assertions are therefore about COHERENCE, not about a particular answer: this Mac is
/// usually online, but a test that requires it would fail in the one situation the capability
/// exists to describe.
/// </para>
/// </summary>
public class NetworkStatusTests
{
    [Fact]
    public void TheMonitorAnswersAndTheAnswerIsCoherent()
    {
        var network = new AppleNetworkStatus();

        // The monitor reports asynchronously after starting; give the first report a moment.
        var state = network.Current;
        for (var waited = 0; waited < 2000 && !state.Online; waited += 50)
        {
            Thread.Sleep(50);
            state = network.Current;
        }

        // Offline with a kind, or online with none, would be the readout lying.
        if (state.Online) state.Kind.Should().NotBe(NetworkKind.None);
        else state.Kind.Should().Be(NetworkKind.None);
    }

    [Fact]
    public void SubscribingAndDisposing_NeitherThrowsNorLeaksIntoTheDeadListener()
    {
        var network = new AppleNetworkStatus();
        var heard = 0;

        var subscription = network.Subscribe(_ => heard++);
        subscription.Dispose();
        subscription.Dispose();   // twice is allowed, per the contract

        var after = heard;
        Thread.Sleep(100);
        heard.Should().Be(after, "a disposed subscription hears nothing more");
    }
}
