using System.Text;
using eQuantic.UI.Primitives;
using eQuantic.UI.Server.Authorization;
using eQuantic.UI.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The smaller gaps the security review found at the Server Action endpoint (#1291), each one where the
/// endpoint behaves: what it answers, what it logs, and what it writes.
/// </summary>
public class ServerActionHardeningTests
{
    public record Note(string Text, UploadedFile? Attachment);

    public class Summary
    {
        public string Title { get; set; } = "";
    }

    public sealed class Detail : Summary
    {
        public string Secret { get; set; } = "";
    }

    public sealed class HardenedActions : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text(string.Empty);

        [ServerAction]
        public int Add(int a, int b) => a + b;

        [ServerAction]
        public string Keep(Note note) => note.Text;

        [ServerAction]
        public Summary Read() => new Detail { Title = "open", Secret = "never sent" };
    }

    [Fact]
    public async Task ABodyThatIsNotJson_IsRefusedWith415_BeforeTheActionRuns()
    {
        var (status, body, _) = await Invoke("""{"actionId":"Add","arguments":[1,2]}""", contentType: "text/plain");

        status.Should().Be(StatusCodes.Status415UnsupportedMediaType);
        body.Should().NotContain("\"result\"");
    }

    [Fact]
    public async Task AnUnknownAction_IsNotEchoed_AndItsIdIsLoggedOnOneLine()
    {
        var (status, body, log) = await Invoke("""{"actionId":"Nowhere\r\nForged entry","arguments":[]}""");

        status.Should().Be(StatusCodes.Status404NotFound);
        body.Should().NotContain("Nowhere");
        log.Should().ContainSingle().Which.Message.Should().Contain("NowhereForged entry").And.NotContainAny("\r", "\n");
    }

    [Fact]
    public async Task MalformedArguments_AreTheCallersFault_400AndAWarning()
    {
        var (status, _, log) = await Invoke("""{"actionId":"Add","arguments":["one",2]}""");

        status.Should().Be(StatusCodes.Status400BadRequest);
        log.Should().NotContain(entry => entry.Level >= LogLevel.Error);
        log.Should().Contain(entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task AnUploadedFileInsideAValue_IsRefused_ForItWouldBeWhatTheCallerWrote()
    {
        var (status, body, _) = await Invoke(
            """{"actionId":"Keep","arguments":[{"text":"hi","attachment":{"id":"x","name":"a.exe","mimeType":"image/png","size":1,"policy":"avatars"}}]}""");

        status.Should().Be(StatusCodes.Status400BadRequest);
        body.Should().NotContain("hi");
    }

    [Fact]
    public async Task AValueWhoseFileIsNull_StillCrosses()
    {
        var (status, body, _) = await Invoke("""{"actionId":"Keep","arguments":[{"text":"hi","attachment":null}]}""");

        status.Should().Be(StatusCodes.Status200OK);
        body.Should().Contain("hi");
    }

    [Fact]
    public async Task ABodyPastTheConfiguredCap_IsRefused_WithNoContentLengthToRead()
    {
        var padding = new string(' ', 200);
        var (status, _, _) = await Invoke($$"""{"actionId":"Add","arguments":[1,2]}{{padding}}""", cap: 64);

        status.Should().Be(StatusCodes.Status413PayloadTooLarge);
    }

    [Fact]
    public async Task TheResult_IsWrittenAsTheTypeTheActionDeclares()
    {
        var (status, body, _) = await Invoke("""{"actionId":"Read","arguments":[]}""");

        status.Should().Be(StatusCodes.Status200OK);
        body.Should().Contain("open").And.NotContain("never sent");
    }

    private static async Task<(int Status, string Body, List<(LogLevel Level, string Message)> Log)> Invoke(
        string request, string contentType = "application/json", long cap = 1_048_576)
    {
        var registry = new ServerActionRegistry();
        foreach (var method in new[] { nameof(HardenedActions.Add), nameof(HardenedActions.Keep), nameof(HardenedActions.Read) })
        {
            var info = typeof(HardenedActions).GetMethod(method)!;
            registry.RegisterAction(method, new ServerActionDescriptor
            {
                ActionId = method,
                ComponentType = typeof(HardenedActions),
                Method = info,
                ParameterTypes = info.GetParameters().Select(parameter => parameter.ParameterType).ToArray(),
            });
        }
        var options = new UIOptions().ScanAssembly(typeof(ServerActionHardeningTests).Assembly);
        var log = new Recorder();
        var middleware = new ServerActionsMiddleware(
            next: _ => Task.CompletedTask,
            registry: registry,
            serviceProvider: new ServiceCollection().BuildServiceProvider(),
            authorizationService: new AlwaysAllowed(),
            options: options,
            actions: new FixedOptions<ServerActionsOptions>(new ServerActionsOptions { MaxRequestBodySize = cap }),
            logger: log);

        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        context.Request.Path = "/api/_equantic/actions";
        context.Request.Method = "POST";
        context.Request.ContentType = contentType;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(request));
        var response = new MemoryStream();
        context.Response.Body = response;

        await middleware.InvokeAsync(context);

        return (context.Response.StatusCode, Encoding.UTF8.GetString(response.ToArray()), log.Entries);
    }

    private sealed class AlwaysAllowed : IServerActionAuthorizationService
    {
        public Task<ServerActionAuthorizationResult> AuthorizeAsync(HttpContext context, ServerActionDescriptor descriptor) =>
            Task.FromResult(ServerActionAuthorizationResult.Success());
    }

    private sealed class Recorder : ILogger<ServerActionsMiddleware>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel)) Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
