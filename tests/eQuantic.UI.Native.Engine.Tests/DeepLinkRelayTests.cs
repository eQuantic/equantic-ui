using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// What a deep link DOES once a platform has produced one. The plumbing differs on every target —
/// an AppleEvent on macOS, <c>application:openURL:</c> on iOS, an Intent on Android — and none of
/// them differs in what happens next, which is why the rules live here where they can be tested.
/// </summary>
public class DeepLinkRelayTests
{
    [Fact]
    public void TheFirstUrlIsTheLaunchUrl_AndIsAnsweredRatherThanDelivered()
    {
        var relay = new DeepLinkRelay();
        relay.Launch.Should().BeNull("an app started normally was launched with nothing");

        relay.Offer("acme://activate?key=abc").Should().BeTrue();

        relay.Launch.Should().Be(new Uri("acme://activate?key=abc"));
    }

    /// <summary>
    /// The trap this exists to close: on a cold start the URL arrives before any component does, so
    /// a relay that only DELIVERED would hand it to nobody. Reading it later must still work, and
    /// must keep working — a component is torn down and rebuilt, and each rebuild reads again.
    /// </summary>
    [Fact]
    public void TheLaunchUrlSurvivesBeingRead()
    {
        var relay = new DeepLinkRelay();
        relay.Offer("acme://one");

        relay.Launch.Should().Be(new Uri("acme://one"));
        relay.Launch.Should().Be(new Uri("acme://one"));
    }

    /// <summary>And it is the FIRST one, not the latest: a link clicked while the app runs did not
    /// launch it.</summary>
    [Fact]
    public void ALaterUrlDoesNotBecomeTheLaunchUrl()
    {
        var relay = new DeepLinkRelay();
        relay.Offer("acme://first");
        relay.Offer("acme://second");

        relay.Launch.Should().Be(new Uri("acme://first"));
    }

    [Fact]
    public void ASubscriberHearsEveryUrlFromWhenItSubscribed()
    {
        var relay = new DeepLinkRelay();
        var heard = new List<Uri>();
        using var subscription = relay.Subscribe(heard.Add);

        relay.Offer("acme://one");
        relay.Offer("acme://two");

        heard.Should().Equal(new Uri("acme://one"), new Uri("acme://two"));
    }

    /// <summary>
    /// The launch URL is NOT replayed on subscribe. Two subscribers would otherwise disagree about
    /// whether they had seen it — the first one would, the second would not — and the app would act
    /// on the same link twice.
    /// </summary>
    [Fact]
    public void SubscribingDoesNotReplayTheLaunchUrl()
    {
        var relay = new DeepLinkRelay();
        relay.Offer("acme://launch");

        var heard = new List<Uri>();
        using var subscription = relay.Subscribe(heard.Add);

        heard.Should().BeEmpty();
        relay.Launch.Should().Be(new Uri("acme://launch"), "reading it is how you get it");
    }

    [Fact]
    public void DisposingStopsDelivery_AndDisposingTwiceIsFine()
    {
        var relay = new DeepLinkRelay();
        var heard = new List<Uri>();
        var subscription = relay.Subscribe(heard.Add);

        relay.Offer("acme://before");
        subscription.Dispose();
        subscription.Dispose();
        relay.Offer("acme://after");

        heard.Should().ContainSingle().Which.Should().Be(new Uri("acme://before"));
    }

    /// <summary>
    /// A component that unsubscribes from inside its own callback is an ordinary thing — it is what
    /// "handle the first link and stop listening" looks like. Delivering under the lock would
    /// deadlock on it.
    /// </summary>
    [Fact]
    public void AListenerMayUnsubscribeFromInsideItsOwnCallback()
    {
        var relay = new DeepLinkRelay();
        var heard = new List<Uri>();
        IDisposable? subscription = null;
        subscription = relay.Subscribe(url =>
        {
            heard.Add(url);
            subscription!.Dispose();
        });

        relay.Offer("acme://one");
        relay.Offer("acme://two");

        heard.Should().ContainSingle();
    }

    /// <summary>
    /// What is not a URL is DROPPED rather than passed on — the contract promises a Uri, and
    /// inventing one is worse than saying no. The answer says which happened, so a shell can log it.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    public void WhatIsNotAUrlIsDropped(string? text)
    {
        var relay = new DeepLinkRelay();
        var heard = new List<Uri>();
        using var subscription = relay.Subscribe(heard.Add);

        relay.Offer(text).Should().BeFalse();

        heard.Should().BeEmpty();
        relay.Launch.Should().BeNull("a string that is not a URL did not launch anything either");
    }

    /// <summary>
    /// A URL the app will not RECOGNISE is still delivered. Deciding what an unknown host means is
    /// the app's, and a relay that swallowed what it did not know would be deciding on the app's
    /// behalf and leaving no evidence.
    /// </summary>
    [Fact]
    public void AUrlTheAppMayNotRecogniseIsStillDelivered()
    {
        var relay = new DeepLinkRelay();
        var heard = new List<Uri>();
        using var subscription = relay.Subscribe(heard.Add);

        relay.Offer("acme://something-nobody-implemented/yet?with=params").Should().BeTrue();

        heard.Should().ContainSingle();
    }

    /// <summary>
    /// A rooted PATH parses — .NET reads it as an implicit file URI — so it is delivered as one
    /// rather than dropped. Documented rather than forbidden: a `file:` link is a real deep link on
    /// a desktop, and a rule that rejected it to keep this test tidy would reject that too.
    /// </summary>
    [Fact]
    public void ARootedPathArrivesAsAFileUrl()
    {
        var relay = new DeepLinkRelay();

        relay.Offer("/Users/someone/file.txt").Should().BeTrue();

        relay.Launch!.Scheme.Should().Be("file");
        relay.Launch!.AbsolutePath.Should().Be("/Users/someone/file.txt");
    }

    [Fact]
    public void SurroundingWhitespaceIsNotPartOfTheUrl()
    {
        var relay = new DeepLinkRelay();

        relay.Offer("  acme://trimmed  ").Should().BeTrue();

        relay.Launch.Should().Be(new Uri("acme://trimmed"));
    }
}
