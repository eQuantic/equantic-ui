using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// <c>Navigator.OpenAsync</c> and <c>DownloadAsync</c> (#1787): a handler that awaited an action opens
/// the address it answered with, without a second press on a link. On a Photon shell the address goes to
/// the system through <see cref="IUrlLauncher"/>; where nothing opens addresses, a server's render, the
/// answer is false. The browser's twin is held by <c>navigator-open.spec.ts</c>.
/// </summary>
public class NavigatorOpensAnAddressTests
{
    private sealed class Launcher : IUrlLauncher
    {
        public List<Uri> Opened { get; } = [];

        public ValueTask<bool> OpenUrlAsync(Uri url)
        {
            Opened.Add(url);
            return ValueTask.FromResult(true);
        }

        public bool CanOpen(Uri url) => true;
    }

    private static ComponentContext Context() => new(PhotonTheme.Instance);

    [Fact]
    public async Task AnAbsoluteAddress_GoesToTheTargetsLauncher()
    {
        var launcher = new Launcher();
        using (CapabilityScope.With<IUrlLauncher>(launcher))
        {
            var navigator = Navigator.Of(Context());

            (await navigator.OpenAsync("https://example.com/print/1")).Should().BeTrue();
            (await navigator.DownloadAsync("https://example.com/print/1.pdf", "Catalogue.pdf")).Should().BeTrue();
        }

        launcher.Opened.Select(url => url.AbsoluteUri)
            .Should().Equal("https://example.com/print/1", "https://example.com/print/1.pdf");
    }

    [Fact]
    public async Task WhereNothingOpensAddresses_TheAnswerIsFalse()
    {
        var navigator = Navigator.Of(Context());

        (await navigator.OpenAsync("https://example.com/print/1")).Should().BeFalse(
            "a server's render has no launcher, and opening is a person's, in their browser");
    }

    [Fact]
    public async Task ARelativeAddress_IsRefusedWhereNoPageResolvesIt()
    {
        using var _ = CapabilityScope.With<IUrlLauncher>(new Launcher());
        var navigator = Navigator.Of(Context());

        var open = async () => await navigator.OpenAsync("/print/1");

        (await open.Should().ThrowAsync<ArgumentException>()).Which.ParamName.Should().Be("address");
    }

    [Fact]
    public async Task AnEmptyAddressOrName_IsTheCallersMistake()
    {
        var navigator = Navigator.Of(Context());

        await FluentActions.Awaiting(async () => await navigator.OpenAsync("")).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(async () => await navigator.DownloadAsync("https://example.com/a.pdf", ""))
            .Should().ThrowAsync<ArgumentException>();
    }
}
