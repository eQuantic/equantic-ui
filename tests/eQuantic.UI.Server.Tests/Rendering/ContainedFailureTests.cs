using System.Collections.Concurrent;
using System.Reflection;
using eQuantic.UI.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;

namespace eQuantic.UI.Server.Tests.Rendering;

/// <summary>
/// A component that throws while the server draws a page is contained, and the request's error
/// boundary reports it to the app's log (#1452): for a write-once page, whose tree the request's host
/// builds, and for an escape-hatch page, whose write-once bridges lower through the round's channels.
/// A bridge that built through a boundary of its own would draw the panel and tell nobody.
/// </summary>
public class ContainedFailureTests
{
    private sealed class Throws : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) =>
            throw new InvalidOperationException("the card's data was missing");
    }

    [Page("/contained-write-once")]
    public sealed class WriteOncePage : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: 0);
            column.Add(new Text("before", TypeRole.BodyM));
            column.Add(new Throws());
            return column;
        }
    }

    public sealed class EscapeHatchPage : eQuantic.UI.Web.HtmlElement
    {
        public EscapeHatchPage() => AddChild(new eQuantic.UI.Web.VisualNodeComponent(new Throws()));

        public override eQuantic.UI.Web.HtmlNode Render()
            => new() { Tag = "div", Children = Children.Select(child => child.Render()).ToList() };
    }

    private sealed class Captured : ILoggerProvider
    {
        public ConcurrentQueue<string> Errors { get; } = new();

        public ILogger CreateLogger(string categoryName) => new Logger(this);

        public void Dispose()
        {
        }

        private sealed class Logger(Captured owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (logLevel >= LogLevel.Error) owner.Errors.Enqueue(formatter(state, exception));
            }
        }
    }

    [Theory]
    [InlineData("/contained-write-once")]
    [InlineData("/contained-escape-hatch")]
    public async Task AComponentThatThrows_IsContained_AndReportedToTheAppsLog(string path)
    {
        var captured = new Captured();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.AddProvider(captured);
        builder.Services.AddUI(options => options.ScanAssembly(Assembly.GetExecutingAssembly()));
        await using var app = builder.Build();
        app.MapPage<EscapeHatchPage>("/contained-escape-hatch");
        app.MapUI();
        await app.StartAsync();

        var response = await app.GetTestClient().GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue("a component that throws is contained, never a 500");
        html.Should().Contain("This section could not be displayed.");
        captured.Errors.Should().Contain(message => message.Contains("Throws failed to render and was contained"),
            "the request's boundary reports what it contained to the app's log");
    }
}
