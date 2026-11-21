using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using eQuantic.UI.Primitives;
using eQuantic.UI.Server.Authorization;
using eQuantic.UI.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// An accessor is never a Server Action (#1512). C# takes <c>[ServerAction]</c>, a method's attribute, on an
/// event's <c>add</c> and on a property's <c>get</c>, and the registry read every public method carrying it,
/// so <c>add_Fired</c> and <c>get_Label</c> answered as actions no client stub calls, and a getter's body ran
/// for any caller who named it.
/// </summary>
public class ServerActionAccessorTests
{
    public sealed class Marked : StatelessComponent
    {
        public static int AddsRun;
        public static int GetsRun;
        private Action? _fired;

        public event Action Fired { [ServerAction] add { AddsRun++; _fired += value; } remove => _fired -= value; }

        public string Label { [ServerAction] get { GetsRun++; return "read on the server"; } }

        [ServerAction]
        public string Load() => "loaded";

        public override VisualNode Build(ComponentContext context) => new Text(string.Empty);
    }

    private static readonly string Id = typeof(Marked).FullName!;

    [Theory]
    [InlineData("add_Fired")]
    [InlineData("get_Label")]
    public void AnAccessorMarkedAsAnAction_IsNoAction(string accessor)
    {
        var registry = new ServerActionRegistry();
        registry.ScanAssembly(typeof(ServerActionAccessorTests).Assembly);

        registry.GetAction($"{Id}/{accessor}").Should().BeNull("an accessor is no method a client stub calls");
        registry.GetAction($"{Id}/Load").Should().NotBeNull("the component's real action is still one");
    }

    [Theory]
    [InlineData("add_Fired", """[null]""")]
    [InlineData("add_Fired", """[]""")]
    [InlineData("get_Label", """[]""")]
    public async Task ACallNamingAnAccessor_IsNotFound_AndItsBodyNeverRuns(string accessor, string arguments)
    {
        var (addsBefore, getsBefore) = (Marked.AddsRun, Marked.GetsRun);

        var status = await Invoke($$"""{"actionId":"{{Id}}/{{accessor}}","arguments":{{arguments}}}""");

        (Marked.AddsRun - addsBefore, Marked.GetsRun - getsBefore).Should().Be((0, 0), "the accessor's body is no action's");
        status.Should().Be(StatusCodes.Status404NotFound);
    }

    /// <summary>The page boots the actions bridge when any scanned type declares one, and an accessor does not.</summary>
    [Fact]
    public void AnAccessorMarkedAsAnAction_MakesNoAppHaveActions()
    {
        var options = new UIOptions().ScanAssembly(OnlyAnAccessorMarked());

        Client.AppSurface.Of(options, pattern => [pattern]).HasServerActions.Should().BeFalse();
    }

    /// <summary>An assembly whose one mark is on a property's getter, as C# compiles <c>{ [ServerAction] get … }</c>:
    /// a special-name method carrying the attribute.</summary>
    private static Assembly OnlyAnAccessorMarked()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("OnlyAnAccessorMarked"), AssemblyBuilderAccess.Run);
        var type = assembly.DefineDynamicModule("OnlyAnAccessorMarked").DefineType("Panel", TypeAttributes.Public | TypeAttributes.Class);
        var getter = type.DefineMethod("get_Label",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, typeof(string), Type.EmptyTypes);
        var il = getter.GetILGenerator();
        il.Emit(OpCodes.Ldstr, "x");
        il.Emit(OpCodes.Ret);
        getter.SetCustomAttribute(new CustomAttributeBuilder(typeof(ServerActionAttribute).GetConstructor(Type.EmptyTypes)!, []));
        type.DefineProperty("Label", PropertyAttributes.None, typeof(string), null).SetGetMethod(getter);
        type.CreateType();
        return assembly;
    }

    private static async Task<int> Invoke(string request)
    {
        var registry = new ServerActionRegistry();
        registry.ScanAssembly(typeof(ServerActionAccessorTests).Assembly);
        var middleware = new ServerActionsMiddleware(
            next: _ => Task.CompletedTask,
            registry: registry,
            serviceProvider: new ServiceCollection().BuildServiceProvider(),
            authorizationService: new AlwaysAllowed(),
            options: new UIOptions().ScanAssembly(typeof(ServerActionAccessorTests).Assembly),
            actions: new FixedOptions<ServerActionsOptions>(new ServerActionsOptions()),
            logger: NullLogger<ServerActionsMiddleware>.Instance);

        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        context.Request.Path = "/api/_equantic/actions";
        context.Request.Method = "POST";
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(request));
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);
        return context.Response.StatusCode;
    }

    private sealed class AlwaysAllowed : IServerActionAuthorizationService
    {
        public Task<ServerActionAuthorizationResult> AuthorizeAsync(HttpContext context, ServerActionDescriptor descriptor) =>
            Task.FromResult(ServerActionAuthorizationResult.Success());
    }
}
