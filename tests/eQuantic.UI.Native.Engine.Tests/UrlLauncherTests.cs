using eQuantic.UI.Native.Hosting;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The rule every shell's <see cref="IUrlLauncher"/> applies before its platform is asked, written
/// once in <see cref="UrlLauncher"/>: who was wrong, then what this app opens, then the system. The
/// platform half differs per shell (NSWorkspace, ShellExecuteEx, UIApplication, a VIEW intent) and is
/// proved on each system; the order is the same on all four, and it is pinned here against a shell
/// that records what reached it.
/// </summary>
public class UrlLauncherTests
{
    private static readonly OpenUrlPolicy MailToo = OpenUrlPolicy.Web.Allowing("mailto");

    [Fact]
    public async Task AUrlThisAppOpens_ReachesTheSystem_AsItsAbsoluteForm()
    {
        var shell = new Probe(MailToo) { Takes = true };

        (await shell.OpenUrlAsync(new Uri("https://acme.example/a b"))).Should().BeTrue();
        (await shell.OpenUrlAsync(new Uri("mailto:support@acme.example"))).Should().BeTrue();

        // What a system parses: the space encoded, never the text the app typed.
        shell.Handed.Should().Equal("https://acme.example/a%20b", "mailto:support@acme.example");
    }

    [Fact]
    public async Task TheSystemsNo_IsTheAnswer()
    {
        var shell = new Probe(MailToo) { Takes = false };

        (await shell.OpenUrlAsync(new Uri("https://acme.example"))).Should().BeFalse(
            "nothing claims the scheme on this machine, which is ordinary");
        shell.Handed.Should().ContainSingle();
    }

    /// <summary>
    /// A scheme this app does not open never becomes a call to the system, and the developer is told
    /// the one line that opens it: false alone is the answer nobody can diagnose.
    /// </summary>
    [Fact]
    public async Task ASchemeThisAppDoesNotOpen_NeverReachesTheSystem_AndIsLogged()
    {
        var logger = new RecordingLogger();
        var shell = new Probe(OpenUrlPolicy.Web, logger) { Takes = true };

        (await shell.OpenUrlAsync(new Uri("tel:+351000000000"))).Should().BeFalse();

        shell.Handed.Should().BeEmpty();
        logger.Warnings.Should().ContainSingle().Which.Should().Contain("builder.Workspace.Opens(\"tel\")");
    }

    /// <summary>
    /// The QUESTION applies the same policy, or a button gated by it would show for a link the click
    /// then refuses. It asks the system only about what passed, and logs nothing: a screen may ask it
    /// on every build.
    /// </summary>
    [Fact]
    public void TheQuestionAppliesTheSamePolicy_AndAsksOnlyAboutWhatPassed()
    {
        var logger = new RecordingLogger();
        var shell = new Probe(MailToo, logger) { Claims = true };

        shell.CanOpen(new Uri("mailto:support@acme.example")).Should().BeTrue();
        shell.CanOpen(new Uri("tel:+351000000000")).Should().BeFalse("the app does not open tel:");

        shell.Asked.Should().Equal("mailto:support@acme.example");
        logger.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void TheQuestionIsTheSystemsAnswer_ForWhatTheAppOpens()
    {
        var shell = new Probe(MailToo) { Claims = false };

        shell.CanOpen(new Uri("mailto:support@acme.example")).Should().BeFalse("no mail app is installed");
    }

    /// <summary>
    /// A relative URL is not something the system declined: no system could have been asked. It is
    /// the caller's mistake, on both doors, and it is said at the call.
    /// </summary>
    [Fact]
    public async Task ARelativeUrlOrNoUrl_IsTheCallersMistake_OnBothDoors()
    {
        var shell = new Probe(MailToo);
        var relative = new Uri("about", UriKind.Relative);

        await shell.Invoking(s => s.OpenUrlAsync(relative).AsTask())
            .Should().ThrowAsync<ArgumentException>().WithMessage("*must be absolute*");
        shell.Invoking(s => s.CanOpen(relative))
            .Should().Throw<ArgumentException>().WithMessage("*must be absolute*");
        await shell.Invoking(s => s.OpenUrlAsync(null!).AsTask()).Should().ThrowAsync<ArgumentNullException>();
        shell.Invoking(s => s.CanOpen(null!)).Should().Throw<ArgumentNullException>();

        shell.Handed.Should().BeEmpty();
        shell.Asked.Should().BeEmpty();
    }

    /// <summary>A shell that records what reached it, answering what the test says.</summary>
    private sealed class Probe(OpenUrlPolicy policy, ILogger? logger = null)
        : UrlLauncher(policy, logger ?? new RecordingLogger())
    {
        public bool Takes { get; init; }

        public bool Claims { get; init; }

        public List<string> Handed { get; } = [];

        public List<string> Asked { get; } = [];

        protected override ValueTask<bool> Hand(Uri url)
        {
            Handed.Add(url.AbsoluteUri);
            return new(Takes);
        }

        protected override bool Claimed(Uri url)
        {
            Asked.Add(url.AbsoluteUri);
            return Claims;
        }
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Warnings { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning) Warnings.Add(formatter(state, exception));
        }
    }
}
