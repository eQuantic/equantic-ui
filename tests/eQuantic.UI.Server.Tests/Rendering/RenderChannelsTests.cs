using System.Reflection;
using eQuantic.UI.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;

namespace eQuantic.UI.Server.Tests.Rendering;

/// <summary>
/// Two requests drawn AT ONCE each write their own page (#1404). A render's style sink, gradients, faces,
/// expansion scope, density and link policy are one object the render owns, handed to the lowering, and
/// the escape hatch's bridge finds it in the one ambient left. Each request here is held inside its page's
/// build until the other one is drawing too, so both renders are live together, and each answer must
/// carry its own density and its own atomic rules, and none of the other's.
/// </summary>
public class RenderChannelsTests
{
    /// <summary>Where the two renders meet. Armed only by these tests, so a page that only scans the
    /// assembly never waits.</summary>
    private static class Meeting
    {
        public static volatile bool Armed;
        public static volatile bool Missed;
        public static readonly ManualResetEventSlim Compact = new();
        public static readonly ManualResetEventSlim Comfortable = new();
        public static readonly CountdownEvent Bridges = new(2);

        public static void Reset()
        {
            Compact.Reset();
            Comfortable.Reset();
            Bridges.Reset(2);
            Missed = false;
        }

        /// <summary>Says an escape-hatch page is drawing, its render's channels armed and not read yet,
        /// and waits for the other request's to be there too: whichever armed last, each bridge then reads
        /// its own.</summary>
        public static void ArriveAtTheBridge()
        {
            if (!Armed) return;
            try
            {
                Bridges.Signal();
            }
            catch (InvalidOperationException)
            {
                return; // a later render of a request that already met
            }

            if (!Bridges.Wait(TimeSpan.FromSeconds(10))) Missed = true;
        }

        /// <summary>Says this density is drawing, and waits for the other one to be drawing too.</summary>
        public static void Arrive(Density density)
        {
            if (!Armed) return;
            var compact = density == Density.Compact;
            (compact ? Compact : Comfortable).Set();
            if (!(compact ? Comfortable : Compact).Wait(TimeSpan.FromSeconds(10))) Missed = true;
        }
    }

    /// <summary>A page that draws the density it is built at as a width, 20 compact and 22 comfortable,
    /// once the other request is drawing too.</summary>
    [Page("/channels-meet")]
    public sealed class MeetingPage : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            Meeting.Arrive(context.Density);
            return new Box(new BoxStyle { Width = context.Density == Density.Compact ? 20 : 22, Height = 10 });
        }
    }

    /// <summary>The same page inside an escape-hatch page, whose bridge finds its render's channels in the
    /// ambient the server arms around it. The two requests meet before the bridge reads it, so both have
    /// armed theirs by then.</summary>
    public sealed class MeetingBridgePage : eQuantic.UI.Web.HtmlElement
    {
        public MeetingBridgePage() => AddChild(new eQuantic.UI.Web.VisualNodeComponent(new MeetingPage()));

        public override eQuantic.UI.Web.HtmlNode Render()
        {
            Meeting.ArriveAtTheBridge();
            return new() { Tag = "div", Children = Children.Select(child => child.Render()).ToList() };
        }
    }

    [Theory]
    [InlineData("/channels-meet")]
    [InlineData("/channels-meet-bridge")]
    public async Task TwoRequestsDrawnAtOnce_EachWriteTheirOwnPage(string path)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddUI(options => options.ScanAssembly(Assembly.GetExecutingAssembly()));
        await using var app = builder.Build();
        app.MapPage<MeetingBridgePage>("/channels-meet-bridge");
        app.MapUI();
        await app.StartAsync();
        var client = app.GetTestClient();

        async Task<string> Get(string density)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("Cookie", $"{DensityCookie.Name}={density}");
            var response = await client.SendAsync(request);
            return await response.Content.ReadAsStringAsync();
        }

        Meeting.Reset();
        Meeting.Armed = true;
        string[] pages;
        try
        {
            pages = await Task.WhenAll(Task.Run(() => Get("compact")), Task.Run(() => Get("comfortable")));
        }
        finally
        {
            Meeting.Armed = false;
        }

        // What each page carries first: a render that read the other's channels is built at the other's
        // density, and then it waits at the meeting for a density that never comes.
        pages[0].Should().Contain("width:20px").And.NotContain("width:22px",
            "the compact request's page carries its own rules and none of the other request's");
        pages[1].Should().Contain("width:22px").And.NotContain("width:20px",
            "the comfortable request's page carries its own rules and none of the other request's");
        ShellConfig.In(pages[0]).GetProperty("density").GetString().Should().Be("compact");
        ShellConfig.In(pages[1]).GetProperty("density").GetString().Should().Be("comfortable");
        Meeting.Missed.Should().BeFalse("the two renders were live together, or this proves nothing");
    }
}
