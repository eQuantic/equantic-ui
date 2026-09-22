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

    private enum Mode { Idle, Ready }

    /// <summary>
    /// A prefetch that sets an ENUM, an AUTO-PROPERTY and CLEARS a non-null default — the three
    /// shapes the payload's wire form mangles.
    /// </summary>
    /// <summary>
    /// COMPOSED, not the page, and that distinction is the whole test. The page instance survives
    /// every round — the pipeline re-renders the same wrapper — so its fields still hold what its
    /// own prefetch set and no restore is involved. A component the page BUILDS is constructed
    /// afresh on every expansion, so it is the only one whose loaded values have to be put back.
    /// A first version of this case used the page and passed with the restore broken.
    /// </summary>
    private sealed class AwkwardShapes : Primitives.StatelessComponent, IServerPrefetch
    {
        private Mode _mode = Mode.Idle;
        private string? _cleared = "still here";
        // A NULLABLE, carried here as regression cover rather than as a fix: reflection really does
        // box a non-null long? as a System.Int64, but IsInstanceOfType special-cases Nullable and
        // accepts it (measured). This case never failed — it exists so a stricter type check added
        // later cannot quietly start dropping every nullable a prefetch loads.
        private long? _count;
        public string Loaded { get; set; } = "(not loaded)";

        [ServerOnly]
        public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            _mode = Mode.Ready;
            _cleared = null;
            _count = 42;
            Loaded = "loaded";
            return Task.CompletedTask;
        }

        public override VisualNode Build(ComponentContext context) =>
            new Text($"{_mode}|{_cleared ?? "cleared"}|{Loaded}|n={_count}", TypeRole.Heading);
    }

    [Page("/tree-prefetch-shapes")]
    private sealed class PageWithAwkwardShapes : Primitives.StatelessComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: Space.S2);
            column.Add(new AwkwardShapes());
            return column;
        }
    }

    /// <summary>
    /// What a prefetch loaded survives into the DRAWING, whatever shape it is in.
    ///
    /// <para>
    /// Every prefetch goes through a restore, because the drawing is the discovery round: the first
    /// pass finds the component, the load happens, and the SECOND pass draws with the values put
    /// back onto a fresh instance. So the restore is not a multi-round edge case — it is on the path
    /// of every page that prefetches at all.
    /// </para>
    ///
    /// <para>
    /// The restore used to read the PAYLOAD's snapshot, which is a wire form: an enum written as its
    /// camelCase string is not assignable to the enum field, an auto-property's backing field is
    /// renamed to the property so it matches no field at all, and a null is dropped so CLEARING a
    /// default silently kept it. Three ways for a page to draw the value it had before it loaded
    /// anything. The restore reads a CLR capture now; only the response is normalized.
    /// </para>
    /// </summary>
    [Fact]
    public async Task WhatThePrefetchLoaded_SurvivesIntoTheDrawing_WhateverShapeItIs()
    {
        var context = RequestWith(out _);

        var result = await CreateService().RenderPageAsync(nameof(PageWithAwkwardShapes), context);

        result.Success.Should().BeTrue(result.Error);
        result.Html.Should().Contain("Ready", "an enum survives the restore");
        result.Html.Should().Contain("cleared", "a prefetch that CLEARS a default must not get it back");
        result.Html.Should().Contain("loaded", "an auto-property survives the restore");
        result.Html.Should().Contain("n=42", "a nullable survives the restore — regression cover for a "
            + "stricter type check, not a defect that was ever measured here");
    }

    /// <summary>
    /// A CLEARED DEFAULT IS A LOADED VALUE, and the payload has to say so.
    ///
    /// <para>
    /// The wire snapshot dropped null fields, so a prefetch that CLEARS a non-null default wrote
    /// nothing the client could read. The server drew the cleared value, the payload omitted the
    /// field, and hydration left the component holding the default its own constructor set — the
    /// page reverting a moment after it appeared, which is the whole failure this change removes,
    /// arriving as the one value that is indistinguishable from absence.
    /// </para>
    /// </summary>
    [Fact]
    public async Task APrefetchThatClearsADefault_SaysSoInThePayload()
    {
        var context = RequestWith(out _);

        var result = await CreateService().RenderPageAsync(nameof(PageWithAwkwardShapes), context);

        result.Success.Should().BeTrue(result.Error);
        result.SerializedState.Should().NotBeNull();

        using var payload = JsonDocument.Parse(result.SerializedState!);
        payload.RootElement.TryGetProperty($"{nameof(AwkwardShapes)}#0", out var fields)
            .Should().BeTrue($"payload was: {result.SerializedState}");
        fields.TryGetProperty("_cleared", out var cleared).Should().BeTrue(
            "the field the prefetch CLEARED has to travel; payload was: " + result.SerializedState);
        cleared.ValueKind.Should().Be(JsonValueKind.Null,
            "a cleared value is null on the wire, not a missing key the client reads as 'keep yours'");
    }

    /// <summary>A page that loads its OWN data — the ordinary shape, and the root of every round.</summary>
    [Page("/tree-prefetch-root")]
    private sealed class PageThatLoadsItsOwnData : Primitives.StatelessComponent, IServerPrefetch
    {
        private long _total = -1;

        [ServerOnly]
        public async Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
            => _total = await services.GetRequiredService<ICounts>().GetAsync(cancellationToken);

        public override VisualNode Build(ComponentContext context) =>
            new Text($"total:{_total}", TypeRole.Heading);
    }

    /// <summary>
    /// A PAGE ROOT IS ASKED ONCE, and the reason it needs saying is that the root is the one
    /// component the rounds do not rebuild.
    ///
    /// <para>
    /// Every other component is constructed afresh each round, so a key whose component no longer
    /// matches how the loader was built means the data replaced it. The root does not match either
    /// — its OWN prefetch is what changed its fields — and reading that as a replacement asked it
    /// again on every round: one extra query per page, for the most ordinary page there is. It is
    /// recognised by reference instead, which is exact.
    /// </para>
    /// </summary>
    [Fact]
    public async Task APageThatLoadsItsOwnData_IsAskedOnce()
    {
        var context = RequestWith(out var counts);

        var result = await CreateService().RenderPageAsync(nameof(PageThatLoadsItsOwnData), context);

        result.Success.Should().BeTrue(result.Error);
        result.Html.Should().Contain("total:675617");
        counts.Calls.Should().Be(1,
            "the root is the same instance every round, so it has nothing to be handed back and "
            + "nothing to re-load");
    }

    /// <summary>A row that knows WHICH row it is, and loads for that one.</summary>
    private sealed class Row : Primitives.StatelessComponent, IServerPrefetch
    {
        private readonly string _name;
        private string _value = "(not loaded)";

        public Row(string name) => _name = name;

        [ServerOnly]
        public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            _value = $"{_name}-loaded";
            return Task.CompletedTask;
        }

        public override VisualNode Build(ComponentContext context) =>
            new Text($"row:{_name}:{_value}", TypeRole.BodyM);
    }

    /// <summary>Its own prefetch decides WHICH row it composes, so the row changes between rounds.</summary>
    [Page("/tree-prefetch-swap")]
    private sealed class PageThatSwapsItsRow : Primitives.StatelessComponent, IServerPrefetch
    {
        private string _which = "a";

        [ServerOnly]
        public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            _which = "b";
            return Task.CompletedTask;
        }

        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: Space.S2);
            column.Add(new Row(_which));
            return column;
        }
    }

    /// <summary>
    /// A COMPONENT THE DATA REPLACED IS ITS OWN COMPONENT — it keeps what its constructor was given
    /// and loads what belongs to it, rather than inheriting the state of the one it replaced.
    ///
    /// <para>
    /// The restore used to write EVERY captured field onto the next round's instance, so this page
    /// drew <c>row:a:a-loaded</c> on a request whose own prefetch had already chosen row <c>b</c>:
    /// the constructor argument was overwritten by the round before it, and the value shown belonged
    /// to a row that was no longer on the page. A wrong value, not a missing one — the failure the
    /// key carries a type name to prevent, arriving from inside one type.
    /// </para>
    ///
    /// <para>
    /// Two rules together produce the right answer. Only what a prefetch WROTE is carried, so a
    /// constructor argument is never overwritten; and a component whose comparable fields no longer
    /// match how the loader was built is recognised as a different component, so nothing is restored
    /// onto it and it is asked for its own data.
    /// </para>
    /// </summary>
    [Fact]
    public async Task ARowTheDataReplaced_IsItsOwnRow()
    {
        var context = RequestWith(out _);

        var result = await CreateService().RenderPageAsync(nameof(PageThatSwapsItsRow), context);

        result.Success.Should().BeTrue(result.Error);
        result.Html.Should().Contain("row:b:b-loaded",
            "the page's own prefetch chose row b, so the page draws row b with row b's data");
        result.Html.Should().NotContain("a-loaded",
            "what the replaced row loaded belongs to a row that is no longer on the page");

        result.SerializedState.Should().NotBeNull();
        using var payload = JsonDocument.Parse(result.SerializedState!);
        payload.RootElement.GetProperty($"{nameof(Row)}#0").GetProperty("_value").GetString()
            .Should().Be("b-loaded", "the payload is read off the instance that drew, so it says "
                + "what the markup beside it says");
    }

    private static readonly List<string> Order = [];

    private sealed class SlowLoader : Primitives.StatelessComponent, IServerPrefetch
    {
        private readonly string _name;
        private string _value = "(not loaded)";

        public SlowLoader(string name) => _name = name;

        [ServerOnly]
        public async Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            lock (Order) Order.Add($"enter:{_name}");
            await Task.Delay(30, cancellationToken);
            lock (Order) Order.Add($"leave:{_name}");
            _value = _name;
        }

        public override VisualNode Build(ComponentContext context) => new Text(_value, TypeRole.BodyM);
    }

    [Page("/tree-prefetch-order")]
    private sealed class PageWithTwoLoaders : Primitives.StatelessComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: Space.S2);
            column.Add(new SlowLoader("a"));
            column.Add(new SlowLoader("b"));
            return column;
        }
    }

    /// <summary>
    /// Two prefetches in one round run ONE AT A TIME.
    ///
    /// <para>
    /// They were started together at first, which is faster and wrong: every prefetch is handed the
    /// REQUEST's service provider, so two components resolving the same scoped dependency — an EF
    /// DbContext being the ordinary case — would use one instance concurrently, which EF refuses by
    /// design. A page that worked would fail for a reason its author could not see, and the fix
    /// would be to stop composing two loaders rather than anything about their code.
    /// </para>
    ///
    /// <para>
    /// The order proves it rather than a timer: interleaved entries mean they overlapped.
    /// </para>
    /// </summary>
    [Fact]
    public async Task TwoPrefetchesInOneRound_DoNotShareTheRequestScopeConcurrently()
    {
        lock (Order) Order.Clear();
        var context = RequestWith(out _);

        var result = await CreateService().RenderPageAsync(nameof(PageWithTwoLoaders), context);

        result.Success.Should().BeTrue(result.Error);
        string[] sequential = ["enter:a", "leave:a", "enter:b", "leave:b"];
        Order.Should().Equal(sequential,
            "an interleaved order means both held the request's scoped services at once");
    }

    /// <summary>A link that loads, then composes the next one — a chain deeper than the cap.</summary>
    private sealed class ChainLink(int depth) : Primitives.StatelessComponent, IServerPrefetch
    {
        private readonly int _depth = depth;
        private string _value = "pending";

        [ServerOnly]
        public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            _value = "loaded";
            return Task.CompletedTask;
        }

        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: Space.S1);
            column.Add(new Text($"link{_depth}:{_value}", TypeRole.BodyM));
            // The NEXT link exists only once this one has loaded, so each level costs a round.
            if (_value == "loaded" && _depth < 12) column.Add(new ChainLink(_depth + 1));
            return column;
        }
    }

    [Page("/tree-prefetch-chain")]
    private sealed class PageWithADeepChain : Primitives.StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new ChainLink(0);
    }

    /// <summary>
    /// A chain deeper than the cap stops, and the MARKUP AND THE PAYLOAD STILL AGREE.
    ///
    /// <para>
    /// The loop draws at the top, so awaiting on the last allowed pass would load values nothing
    /// ever draws: the page would serve a link reading "pending" while the payload told the client
    /// it was "loaded", and hydration would swap the text a moment after the page appeared — the
    /// very symptom this whole change exists to remove, reintroduced at the boundary. It stops
    /// before that await, so both say "pending" and the page is visibly incomplete instead of
    /// self-contradictory.
    /// </para>
    /// </summary>
    [Fact]
    public async Task AChainDeeperThanTheCap_ServesMarkupAndStateThatAgree()
    {
        var context = RequestWith(out _);

        var result = await CreateService().RenderPageAsync(nameof(PageWithADeepChain), context);

        result.Success.Should().BeTrue(result.Error);
        result.Html.Should().Contain("link0:loaded", "the cap stops a chain, it does not break the page");
        result.SerializedState.Should().NotBeNull();

        using var payload = JsonDocument.Parse(result.SerializedState!);
        foreach (var entry in payload.RootElement.EnumerateObject())
        {
            if (!entry.Value.TryGetProperty("_value", out var value)) continue;
            var depth = entry.Name.Split('#')[1];
            var says = value.GetString();
            result.Html.Should().Contain($"link{depth}:{says}",
                $"the payload says {entry.Name} is '{says}', so the markup beside it must say the "
                + "same — a page whose words and state disagree is the defect this change removes");
        }
    }

    /// <summary>A Stack is an ordinary container, and a prefetching component sits in one.</summary>
    [Page("/tree-prefetch-stack")]
    private sealed class PageWithAStack : Primitives.StatelessComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            var stack = new Stack();
            stack.Add(new StatsHeader());
            return stack;
        }
    }

    /// <summary>
    /// A component inside a STACK is discovered like any other.
    ///
    /// <para>
    /// The stack resolves a component child itself — <c>ResolveForPositioning</c> calls
    /// <c>ExpandContained</c> directly, because it has to know whether what the component builds is
    /// a <c>Positioned</c> before it can place it — so the realizer's ordinary component visit never
    /// runs for it. The traversal therefore did not see it at all: no prefetch, no payload entry, no
    /// diagnosis, on a container nobody would think twice about using.
    /// </para>
    ///
    /// <para>
    /// The two sides skipped it identically, so the keys stayed aligned and nothing drifted — which
    /// is exactly why it would never have been noticed. It is a coverage hole, not a mismatch.
    /// </para>
    /// </summary>
    [Fact]
    public async Task AComponentInsideAStack_IsDiscoveredLikeAnyOther()
    {
        var context = RequestWith(out var counts);

        var result = await CreateService().RenderPageAsync(nameof(PageWithAStack), context);

        result.Success.Should().BeTrue(result.Error);
        counts.Calls.Should().BeGreaterThan(0, "a Stack is a container, not a place data stops arriving");
        result.Html.Should().Contain("Downloads: 675617");
    }

    /// <summary>
    /// A page written as an ESCAPE-HATCH element rather than a write-once component. It is a
    /// <c>Web.IComponent</c>, so it never passes through the realizer's component visit and the
    /// traversal would find nothing to ask — the pipeline asks its root directly instead.
    /// <para>
    /// It reaches the SSR index through <c>MapPage&lt;T&gt;</c>: the attribute scan admits only
    /// <c>UiComponent</c>s, while <c>MapPage</c> accepts either. That is the one door this shape
    /// comes through, so the test comes through it too.
    /// </para>
    /// </summary>
    private sealed class CoreRootPage : eQuantic.UI.Web.HtmlElement, IServerPrefetch
    {
        private long _downloads = -1;

        [ServerOnly]
        public async Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
            => _downloads = await services.GetRequiredService<ICounts>().GetAsync(cancellationToken);

        public override eQuantic.UI.Web.HtmlNode Render()
            => eQuantic.UI.Web.HtmlNode.Text($"Downloads: {_downloads}");
    }

    /// <summary>
    /// AN ESCAPE-HATCH PAGE SHIPS WHAT IT LOADED, like every other page that loads something.
    ///
    /// <para>
    /// Its root is asked directly and its markup was always right — but the key it loaded under
    /// never joined the asked set, and the asked set is what decides whether the response carries a
    /// payload at all. So the one page shape that never needed the traversal was the one shape that
    /// hydrated from its defaults: the number drew and then reverted, which is the exact failure
    /// this change exists to remove, arriving by the other door.
    /// </para>
    /// </summary>
    [Fact]
    public async Task AnEscapeHatchPage_ShipsWhatItLoaded()
    {
        var counts = new Counts();
        var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder();
        builder.Services.AddSingleton<ICounts>(counts);
        builder.Services.AddUI(o => o.ScanAssembly(typeof(TreePrefetchTests).Assembly));
        await using var app = builder.Build();
        app.MapPage<CoreRootPage>("/tree-prefetch-core");

        var service = app.Services.GetRequiredService<IServerRenderingService>();
        var context = new DefaultHttpContext { RequestServices = app.Services };

        var result = await service.RenderPageAsync(nameof(CoreRootPage), context);

        result.Success.Should().BeTrue(result.Error);
        result.Html.Should().Contain("Downloads: 675617");
        counts.Calls.Should().Be(1, "the root is asked once, directly");

        result.SerializedState.Should().NotBeNull(
            "a root that prefetched has state the client cannot rebuild for itself");
        using var payload = JsonDocument.Parse(result.SerializedState!);
        payload.RootElement.TryGetProperty($"{nameof(CoreRootPage)}#0", out var fields)
            .Should().BeTrue($"the root's own key carries its fields; payload was: {result.SerializedState}");
        // A STRING on the wire: EqJson writes Int64 that way so values past 2^53 survive into the
        // client's BigInt-backed `long`, which is the Server Action protocol's spelling too.
        fields.GetProperty("_downloads").GetString().Should().Be("675617");
    }
}
