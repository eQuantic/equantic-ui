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
/// SERVER DATA IS LOADED FOR EVERY COMPONENT THAT ASKS, not only the root of the route.
///
/// <para>
/// The contract has always said "a page — or any component the page composes — declares the data it
/// needs". The pipeline only ever asked the root: <c>metadataSource is IServerPrefetch</c>, and a
/// payload read off that same object. A component the page merely composed had
/// <c>PrefetchAsync</c> called never and its fields carried never, and nothing said so — the build
/// passed, the suite passed, and `curl` returned correct HTML. Only a browser showed it, a moment
/// after the page appeared, when hydration rebuilt the component from a payload that never carried
/// its field.
/// </para>
///
/// <para>
/// A header shown on every route needing one server-loaded number is the shape that found it, and
/// it is the ordinary shape — which is why "load it at the root and pass it down" was not an
/// acceptable answer for a contract that invites the other thing in its first sentence.
/// </para>
/// </summary>
public class TreePrefetchTests
{
    private interface ICounts
    {
        Task<long> GetAsync(CancellationToken cancellationToken);
    }

    private sealed class Counts : ICounts
    {
        public int Calls { get; private set; }

        public Task<long> GetAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(675_617L);
        }
    }

    /// <summary>The header the defect was found on: composed by the page, loading its own number.</summary>
    private sealed class StatsHeader : Primitives.StatelessComponent, IServerPrefetch
    {
        // The field default is what a reader sees if the prefetch never runs — which is exactly
        // what the old pipeline showed after hydration, having drawn the real number first.
        private long _downloads = -1;

        [ServerOnly]
        public async Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
            => _downloads = await services.GetRequiredService<ICounts>().GetAsync(cancellationToken);

        public override VisualNode Build(ComponentContext context) =>
            new Text($"Downloads: {_downloads}", TypeRole.Heading);
    }

    /// <summary>A page that declares NO server data of its own and composes a component that does.</summary>
    [Page("/tree-prefetch")]
    private sealed class PageComposingAPrefetcher : Primitives.StatelessComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: Space.S2);
            column.Add(new StatsHeader());
            column.Add(new Text("body", TypeRole.BodyM));
            return column;
        }
    }

    private static ServerRenderingService CreateService()
    {
        var options = new UIOptions();
        options.ScanAssembly(typeof(TreePrefetchTests).Assembly);
        return new ServerRenderingService(
            new ServiceCollection().BuildServiceProvider(),
            options,
            NullLogger<ServerRenderingService>.Instance);
    }

    private static DefaultHttpContext RequestWith(out Counts counts)
    {
        counts = new Counts();
        var provider = new ServiceCollection()
            .AddSingleton<ICounts>(counts)
            .BuildServiceProvider();
        return new DefaultHttpContext { RequestServices = provider };
    }

    [Fact]
    public async Task AComponentThePageComposes_HasItsServerDataLoaded()
    {
        var context = RequestWith(out var counts);

        var result = await CreateService()
            .RenderPageAsync(nameof(PageComposingAPrefetcher), context);

        result.Success.Should().BeTrue(result.Error);
        result.Html.Should().Contain("Downloads: 675617",
            "the header declares IServerPrefetch, so the markup must carry what it loaded — not the "
            + "field default it states while nothing has loaded");
        counts.Calls.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ItsFieldsReachTheHydrationPayload_UnderItsOwnKey()
    {
        var context = RequestWith(out _);

        var result = await CreateService()
            .RenderPageAsync(nameof(PageComposingAPrefetcher), context);

        result.SerializedState.Should().NotBeNull(
            "a component that prefetched has state the client cannot rebuild for itself");

        using var payload = JsonDocument.Parse(result.SerializedState!);

        // KEYED BY COMPONENT. A flat map could not say which component a field belongs to, and two
        // components holding a field of the same name would overwrite each other in whichever order
        // reflection returned them.
        payload.RootElement.TryGetProperty($"{nameof(StatsHeader)}#0", out var header)
            .Should().BeTrue($"the payload names each component; it carried: {result.SerializedState}");
        // AS A STRING, and that is the wire contract rather than a quirk: EqJson writes Int64 as
        // text so a value beyond 2^53 survives into the client's BigInt-backed `long`. Asserting a
        // number here failed, which is the contract telling the test what it is.
        header.GetProperty("_downloads").GetString().Should().Be("675617");
    }

    /// <summary>A row that loads its own detail — one per item the page found.</summary>
    private sealed class RowDetail(int index) : Primitives.StatelessComponent, IServerPrefetch
    {
        private readonly int _index = index;
        private string _detail = "(not loaded)";

        [ServerOnly]
        public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            _detail = $"detail-{_index}";
            return Task.CompletedTask;
        }

        public override VisualNode Build(ComponentContext context) =>
            new Text(_detail, TypeRole.BodyM);
    }

    /// <summary>
    /// The page whose TREE DEPENDS ON ITS OWN DATA: it loads a list, then composes one prefetching
    /// component per row. Those rows do not exist on the first expansion, so a single discovery
    /// round cannot see them.
    /// </summary>
    [Page("/tree-prefetch-rows")]
    private sealed class PageWhoseRowsPrefetch : Primitives.StatelessComponent, IServerPrefetch
    {
        private IReadOnlyList<int> _ids = [];

        [ServerOnly]
        public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            _ids = [0, 1];
            return Task.CompletedTask;
        }

        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: Space.S2);
            foreach (var id in _ids) column.Add(new RowDetail(id));
            return column;
        }
    }

    /// <summary>
    /// A component can EXIST because of what another one loaded, so discovery repeats until a round
    /// finds nothing new. One round would draw the rows with their field defaults — the page's own
    /// prefetch creates them, and they declare server data of their own.
    ///
    /// <para>
    /// This is the case that decided the loop over a single pass. Without it the rows render
    /// "(not loaded)", which is a plausible-looking page rather than an error.
    /// </para>
    /// </summary>
    [Fact]
    public async Task AComponentThatExistsBecauseOfAPrefetch_IsPrefetchedToo()
    {
        var context = RequestWith(out _);

        var result = await CreateService().RenderPageAsync(nameof(PageWhoseRowsPrefetch), context);

        result.Success.Should().BeTrue(result.Error);
        result.Html.Should().Contain("detail-0").And.Contain("detail-1");
        result.Html.Should().NotContain("(not loaded)",
            "a row composed from what the page loaded declares its own server data, and the loop "
            + "exists so the round that discovers it can still ask");
    }

    private abstract class CarryingBase : Primitives.StatelessComponent
    {
        // PRIVATE on the base, which is the shape reflection quietly dropped: GetFields does not
        // return a base type's private fields, so the value drew and then vanished on hydration.
        private string _carried = "(not loaded)";

        protected string Carried => _carried;

        protected void Load(string value) => _carried = value;
    }

    [Page("/tree-prefetch-base-field")]
    private sealed class PageWithABaseField : CarryingBase, IServerPrefetch
    {
        [ServerOnly]
        public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            Load("loaded");
            return Task.CompletedTask;
        }

        public override VisualNode Build(ComponentContext context) => new Text(Carried, TypeRole.Heading);
    }

    /// <summary>
    /// A PRIVATE field declared on a BASE travels. <c>GetFields</c> alone does not return one, so
    /// the payload silently lacked it and the value vanished on hydration — the same symptom as the
    /// root-only defect, reached from reflection instead of from the pipeline.
    ///
    /// <para>
    /// It was unreachable while a component over an app-owned base was unusable for other reasons,
    /// and became reachable the moment that was fixed.
    /// </para>
    /// </summary>
    [Fact]
    public async Task APrivateFieldOnABase_ReachesThePayload()
    {
        var context = RequestWith(out _);

        var result = await CreateService().RenderPageAsync(nameof(PageWithABaseField), context);

        result.Success.Should().BeTrue(result.Error);
        result.Html.Should().Contain("loaded");
        result.SerializedState.Should().NotBeNull();

        using var payload = JsonDocument.Parse(result.SerializedState!);
        payload.RootElement.TryGetProperty($"{nameof(PageWithABaseField)}#0", out var page)
            .Should().BeTrue($"the page prefetched; payload was: {result.SerializedState}");
        page.TryGetProperty("_carried", out var carried)
            .Should().BeTrue($"the base's private field has to travel; payload was: {result.SerializedState}");
        carried.GetString().Should().Be("loaded");
    }
}
