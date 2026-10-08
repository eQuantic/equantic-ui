using System.Text;
using eQuantic.UI.Server.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// A dictionary crosses a Server Action both ways in the order it enumerates (#437). The request is the
/// one the browser's runtime writes for these arguments, pinned in a file that the runtime's own spec
/// (<c>dictionary.spec.ts</c>) writes the same bytes against, so the two halves are held to one text.
/// </summary>
public class ServerActionDictionaryTests
{
    private const string Fixture = "src/eQuantic.UI.Runtime/src/utils/__fixtures__/action-arguments.json";

    public sealed class OrderActions
    {
        public Task<string> Echo(Dictionary<int, string> scores, IDictionary<string, int> codes) =>
            Task.FromResult($"{string.Join(",", scores.Keys)} | {string.Join(",", codes.Keys)}");

        public Task<Dictionary<int, string>> Scores() =>
            Task.FromResult(new Dictionary<int, string> { [3] = "c", [1] = "a" });
    }

    private sealed class AlwaysAllowed : IServerActionAuthorizationService
    {
        public Task<ServerActionAuthorizationResult> AuthorizeAsync(
            HttpContext context, ServerActionDescriptor descriptor) =>
            Task.FromResult(ServerActionAuthorizationResult.Success());
    }

    [Fact]
    public async Task TheArgumentsTheBrowserWrites_ArriveInTheOrderTheBrowserHeldThem()
    {
        var (status, body) = await Invoke("Echo", File.ReadAllText(Path.Combine(RepoRoot(), Fixture)));

        status.Should().Be(StatusCodes.Status200OK, body);
        body.Should().Be("""{"success":true,"result":"3,1 | b,10,9"}""");
    }

    [Fact]
    public async Task ADictionaryAnswered_IsWrittenAsItsPairs_InTheOrderItEnumerates()
    {
        var (status, body) = await Invoke("Scores", """{"actionId":"OrderActions/Scores","arguments":[]}""");

        status.Should().Be(StatusCodes.Status200OK, body);
        body.Should().Be("""{"success":true,"result":[[3,"c"],[1,"a"]]}""");
    }

    private static async Task<(int Status, string Body)> Invoke(string method, string request)
    {
        var actionId = $"{nameof(OrderActions)}/{method}";
        var registry = new ServerActionRegistry();
        registry.RegisterAction(actionId, new ServerActionDescriptor
        {
            ActionId = actionId,
            ComponentType = typeof(OrderActions),
            Method = typeof(OrderActions).GetMethod(method)!,
        });

        var root = new ServiceCollection().BuildServiceProvider(validateScopes: true);
        var middleware = new ServerActionsMiddleware(
            next: _ => Task.CompletedTask,
            registry: registry,
            serviceProvider: root,
            authorizationService: new AlwaysAllowed(),
            options: new UIOptions(),
            logger: NullLogger<ServerActionsMiddleware>.Instance);

        using var scope = root.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Path = "/api/_equantic/actions";
        context.Request.Method = "POST";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(request));
        var response = new MemoryStream();
        context.Response.Body = response;

        await middleware.InvokeAsync(context);

        return (context.Response.StatusCode, Encoding.UTF8.GetString(response.ToArray()));
    }

    private static string RepoRoot()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "src", "eQuantic.UI.Runtime")))
            here = here.Parent;
        here.Should().NotBeNull("the suite runs inside the repository");
        return here!.FullName;
    }
}
