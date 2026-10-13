using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// What a deep link DOES once a platform has produced one. The plumbing differs on every target —
/// an AppleEvent on macOS, a scene's URL contexts on iOS, an Intent on Android — and none of them
/// differs in what happens next, which is why the rules live here where they can be tested.
/// </summary>
public class DeepLinkRelayTests
{
    /// <summary>
    /// A phone's shell KNOWS what the app was started with, and says so. An app opened from its
    /// icon was launched with nothing, and a link tapped an hour later did not launch it: read as
    /// the first URL ever seen, <c>Launch</c> answered that link, against its own contract.
    /// </summary>
    [Fact]
    public void StartedNormally_ALinkThatArrivesLaterIsHeardAndIsNotTheLaunchUrl()
    {
        var relay = new DeepLinkRelay();
        var heard = new List<Uri>();
        using var _ = relay.Subscribe(heard.Add);

        relay.StartedWith(null).Should().BeFalse("there was no URL");
        relay.Offer("acme://later").Should().BeTrue();

        relay.Launch.Should().BeNull("the app was started from its icon");
        heard.Should().Equal(new Uri("acme://later"));
    }

    [Fact]
    public void StartedWithALink_ItIsTheLaunchUrl_AndALaterOneDoesNotReplaceIt()
    {
        var relay = new DeepLinkRelay();

        relay.StartedWith(" acme://activate?key=abc ").Should().BeTrue();
        relay.Offer("acme://later");

        relay.Launch.Should().Be(new Uri("acme://activate?key=abc"));
    }

    /// <summary>A phone keeps the process and starts the app in it again. The second start is its
    /// own: from the icon, it was launched with nothing, whatever the first one was launched with.</summary>
    [Fact]
    public void StartedAgain_TheAnswerIsTheNewStarts()
    {
        var relay = new DeepLinkRelay();
        relay.StartedWith("acme://first");

        relay.StartedWith(null);
        relay.Launch.Should().BeNull();

        relay.StartedWith("acme://third");
        relay.Launch.Should().Be(new Uri("acme://third"));
    }

    /// <summary>Whoever is already listening hears the link a start came with: on a phone that is a
    /// tree kept alive across the start.</summary>
    [Fact]
    public void AStartWithALink_IsDeliveredToWhoeverIsListening()
    {
        var relay = new DeepLinkRelay();
        var heard = new List<Uri>();
        using var _ = relay.Subscribe(heard.Add);

        relay.StartedWith("acme://open/7");
        relay.StartedWith("not a url").Should().BeFalse();

        heard.Should().Equal(new Uri("acme://open/7"));
        relay.Launch.Should().BeNull("the second start came with nothing that is a URL");
    }

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
        // Rooted as THIS platform roots a path: a Unix path is not absolute to .NET on Windows,
        // where a drive letter is what makes one — the rule under test is the same on both.
        var path = OperatingSystem.IsWindows() ? @"C:\Users\someone\file.txt" : "/Users/someone/file.txt";

        relay.Offer(path).Should().BeTrue();

        relay.Launch!.Scheme.Should().Be("file");
        relay.Launch!.LocalPath.Should().Be(path);
    }

    [Fact]
    public void SurroundingWhitespaceIsNotPartOfTheUrl()
    {
        var relay = new DeepLinkRelay();

        relay.Offer("  acme://trimmed  ").Should().BeTrue();

        relay.Launch.Should().Be(new Uri("acme://trimmed"));
    }
}
