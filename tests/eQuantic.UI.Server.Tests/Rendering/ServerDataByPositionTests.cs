using System.Text.Json;
using eQuantic.UI.Primitives;
using eQuantic.UI.Server.Rendering;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace eQuantic.UI.Server.Tests.Rendering;

/// <summary>
/// The server names a composed component's data by WHERE IT STANDS, with the keys the browser reads
/// off the same positions (#1477): the tree, the type, and the slots from the tree's root down
/// (<c>Element.SlotsBelow</c>), measured below the page, which the browser builds by itself.
/// <para>
/// The keys are spelled out, the same strings the browser's hydration cases adopt under
/// (<c>element-tree.spec.ts</c>): one type in the compact and the expanded arm of an adaptive node, one
/// after the node, and one in an overlay's deferred layer. Those are the places where the order the
/// server lowered in and the order the browser built in part, so a drift in either side's numbering
/// fails here by name rather than as a page that hydrates with its defaults.
/// </para>
/// </summary>
public class ServerDataByPositionTests
{
    private sealed class Probe(string name) : StatelessComponent, IServerPrefetch
    {
        private readonly string _name = name;
        private string _loaded = "pending";

        [ServerOnly]
        public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            _loaded = $"loaded {_name}";
            return Task.CompletedTask;
        }

        public override VisualNode Build(ComponentContext context) => new Text(_loaded, TypeRole.BodyM);
    }

    [Page("/server-data-by-position")]
    private sealed class ArmsAndALayer : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column();
            column.Add(new AdaptiveNode(new Probe("compact"), expanded: new Probe("expanded")));
            column.Add(new Probe("after"));
            column.Add(new Overlay(new Probe("layer")));
            return column;
        }
    }

    private static ServerRenderingService CreateService()
    {
        var options = new UIOptions();
        options.ScanAssembly(typeof(ServerDataByPositionTests).Assembly);
        return new ServerRenderingService(
            new ServiceCollection().BuildServiceProvider(),
            options,
            NullLogger<ServerRenderingService>.Instance);
    }

    [Fact]
    public async Task OneTypeInTwoArms_AfterThem_AndInALayer_IsNamedByWhereEachStands()
    {
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };

        var result = await CreateService().RenderPageAsync(typeof(ArmsAndALayer), context);

        result.Success.Should().BeTrue(result.Error);
        using var payload = JsonDocument.Parse(result.SerializedState!);
        var probe = Web.ComponentIdentity.Of(typeof(Probe));
        string Loaded(string slots) =>
            payload.RootElement.GetProperty($"r0/{probe}@{slots}").GetProperty("_loaded").GetString()!;

        // The column the page built is the tree's root: the adaptive node at slot 0, holding its compact
        // arm at 0 and its expanded arm at 2; the probe after it at 1; the overlay at 2, its layer at 0.
        Loaded("0.0").Should().Be("loaded compact");
        Loaded("0.2").Should().Be("loaded expanded");
        Loaded("1").Should().Be("loaded after");
        Loaded("2.0").Should().Be("loaded layer");
        PayloadKeys.All(payload.RootElement, typeof(Probe)).Should().HaveCount(4,
            "each probe is named once, by its own position");
    }
}
