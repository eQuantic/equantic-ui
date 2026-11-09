using System.Globalization;
using eQuantic.UI.Material;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static eQuantic.UI.Components.UI;

namespace eQuantic.UI.Testing.Tests;

/// <summary>
/// The scenarios of the <c>component-testing</c> spec beyond the three documented shapes, each through
/// the public surface an app's tests reach.
/// </summary>
public class ComponentTesterTests
{
    // ---- a theme and a culture ------------------------------------------------------------------

    private sealed class Total : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => Text(1234.5.ToString("N1"));
    }

    [Fact]
    public void AComponentThatReadsTheTheme_DrawsWithNoneRegistered()
    {
        // The roadmap's case: a Select read a theme nobody had registered, and threw.
        using var tester = new ComponentTester();

        tester.Pump(Select(["One", "Two"], selectedIndex: 0));

        tester.Semantics.Should().ContainSingle(stop => stop.Role == SemanticRole.ComboBox)
            .Which.Value.Should().Be("One");
    }

    [Fact]
    public void TheCultureATestNames_IsTheOneAComponentFormatsWith_AndItIsGivenBack()
    {
        var before = CultureInfo.CurrentCulture;
        using var tester = new ComponentTester(culture: new CultureInfo("pt-BR"));

        tester.Pump(new Total());

        tester.Find("1.234,5").Role.Should().Be(SemanticRole.StaticText);
        CultureInfo.CurrentCulture.Should().BeSameAs(before);
    }

    /// <summary>Says which theme it was built with, so the test can see the one it named reached the
    /// build rather than the default.</summary>
    private sealed class ThemeName : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => Text(context.Theme.GetType().Name);
    }

    [Fact]
    public void TheThemeATestNames_IsTheOneEveryComponentBuildsWith()
    {
        // The wiki's example, which a build compiles here and nowhere else.
        using var tester = new ComponentTester(theme: MaterialTheme.FromSeed(Color.FromRgb(0x67, 0x50, 0xA4)));

        tester.Pump(new ThemeName());

        tester.Find(nameof(MaterialTheme)).Role.Should().Be(SemanticRole.StaticText);
    }

    [Fact]
    public void WithNoCultureNamed_ATestReadsTheSameOnEveryMachine()
    {
        using var tester = new ComponentTester();

        tester.Pump(new Total());

        tester.Find("1,234.5").Role.Should().Be(SemanticRole.StaticText);
    }

    // ---- frames through a live owner ------------------------------------------------------------

    private sealed class Counter : StatefulComponent
    {
        private int _count;

        public int Mounts { get; private set; }

        public int Unmounts { get; private set; }

        protected override void OnMount() => Mounts++;

        protected override void OnUnmount() => Unmounts++;

        public override VisualNode Build(ComponentContext context) => Column(children:
        [
            Text($"Count: {_count}"),
            Button("Add", onPressed: () => SetState(() => _count++)),
        ]);
    }

    /// <summary>A page that builds its body FRESH every time, as an app writes <c>new Stats()</c> inside a
    /// <c>Build</c>: the parent hands the position a new instance on every build, which is the whole
    /// difference between keeping an instance by its position and not. Handing it one instance made
    /// once would keep it by accident, and prove nothing.</summary>
    private sealed class Page(Func<VisualNode> body) : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => Column(children: [Text("Title"), body()]);
    }

    [Fact]
    public void AStatefulComponentAPageBuilds_KeepsItsState()
    {
        using var tester = new ComponentTester();
        tester.Pump(new Page(() => new Counter()));

        tester.Tap("Add");
        tester.HasScheduledFrame.Should().BeTrue("the press set state and asked for a frame");
        tester.Pump();

        tester.HasScheduledFrame.Should().BeFalse();
        tester.Find("Count: 1").Role.Should().Be(SemanticRole.StaticText);
    }

    [Fact]
    public void AComponentMountsOnce_AndUnmountsWhenTheTestEnds()
    {
        var counter = new Counter();
        var tester = new ComponentTester();

        tester.Pump(counter);
        tester.Pump();
        tester.Pump();
        counter.Mounts.Should().Be(1);
        counter.Unmounts.Should().Be(0);

        tester.Dispose();
        counter.Unmounts.Should().Be(1);
    }

    // ---- acting by the name a screen reader announces --------------------------------------------

    [Fact]
    public void ANameNothingAnnounces_FailsNamingWhatIs()
    {
        using var tester = new ComponentTester();
        tester.Pump(Row(children: [Button("Save", onPressed: () => { }), Button("Cancel", onPressed: () => { })]));

        var tap = () => tester.Tap("Delete");

        tap.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("\"Save\"").And.Contain("\"Cancel\"");
    }

    [Fact]
    public void ADisabledControl_CannotBeTapped()
    {
        using var tester = new ComponentTester();
        tester.Pump(new Components.Button("Save", onPressed: () => { }) { Disabled = true });

        var tap = () => tester.Tap("Save");

        tap.Should().Throw<InvalidOperationException>().WithMessage("*disabled*cannot be tapped*");
    }

    [Fact]
    public void ALink_IsFollowedByTheBrowser_NotByTheTester()
    {
        // As the wiki says: the tester does not navigate, so a link with no handler of its own is
        // not something a tap can run.
        using var tester = new ComponentTester();
        tester.Pump(new Link("/docs", Text("Docs")));

        tester.Find(SemanticRole.Link, "Docs").Should().NotBeNull();
        var tap = () => tester.Tap("Docs");

        tap.Should().Throw<InvalidOperationException>().WithMessage("Nothing handles a tap on Link \"Docs\"*");
    }

    [Fact]
    public void TwoStopsOfOneName_AreToldApartByTheirRole()
    {
        // A text input shows its label as text and announces its field by the same name.
        using var tester = new ComponentTester();
        tester.Pump(TextInput("ana", label: "Email"));

        var find = () => tester.Find("Email");

        find.Should().Throw<InvalidOperationException>().WithMessage("*2 of the frame's stops*name the role*");
        tester.Find(SemanticRole.TextField, "Email").Value.Should().Be("ana");
    }

    // ---- what a screen reader announces ---------------------------------------------------------

    [Fact]
    public void ACheckbox_AnnouncesItsRoleNameAndCheck()
    {
        using var tester = new ComponentTester();

        tester.Pump(Checkbox(true, null, "Accept"));

        tester.Semantics.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Role = SemanticRole.Checkbox,
            Name = "Accept",
            Checked = (SemanticCheck?)SemanticCheck.On,
        });
    }

    [Theory]
    [InlineData(1280f, "Desk", "Phone")]
    [InlineData(400f, "Phone", "Desk")]
    public void TheArmAWidthHides_IsNotAnnounced_ThoughTheMarkupHoldsIt(float width, string shown, string hidden)
    {
        using var tester = new ComponentTester(width: width);

        tester.Pump(AdaptiveNode(Text("Phone"), expanded: Text("Desk")));

        tester.Semantics.Select(stop => stop.Name).Should().Equal(shown);
        tester.Html.Should().Contain(hidden, "the browser gets every arm, and its media rules pick one");
    }

    // ---- a page's server data -------------------------------------------------------------------

    private interface ICatalog
    {
        Task<IReadOnlyList<string>> IdsAsync();

        Task<string> TitleAsync(string id);
    }

    private sealed class Catalog : ICatalog
    {
        public Task<IReadOnlyList<string>> IdsAsync() => Task.FromResult<IReadOnlyList<string>>(["a", "b"]);

        public Task<string> TitleAsync(string id) => Task.FromResult($"Title {id}");
    }

    private static ServiceProvider Services() =>
        new ServiceCollection().AddSingleton<ICatalog, Catalog>().BuildServiceProvider();

    /// <summary>A STATELESS component that loads its own data, nested in a page: the shape a rebuild of
    /// everything after a prefetch loses, because its parent hands the position a fresh instance.</summary>
    private sealed class Stats : StatelessComponent, IServerPrefetch
    {
        private string _title = "loading";

        [ServerOnly]
        public async Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken) =>
            _title = await services.GetRequiredService<ICatalog>().TitleAsync("stats");

        public override VisualNode Build(ComponentContext context) => Text(_title);
    }

    [Fact]
    public async Task AStatelessComponentThatLoads_InsideAPage_IsDrawnWithWhatItLoaded()
    {
        using var tester = new ComponentTester(services: Services());

        await tester.PumpPageAsync(new Page(() => new Stats()));

        tester.Find("Title stats").Role.Should().Be(SemanticRole.StaticText);
    }

    private sealed class Item(string id) : StatelessComponent, IServerPrefetch
    {
        private string _title = "";

        [ServerOnly]
        public async Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken) =>
            _title = await services.GetRequiredService<ICatalog>().TitleAsync(id);

        public override VisualNode Build(ComponentContext context) => Text(_title);
    }

    private sealed class CatalogPage : StatelessComponent, IServerPrefetch
    {
        private IReadOnlyList<string> _ids = [];

        [ServerOnly]
        public async Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken) =>
            _ids = await services.GetRequiredService<ICatalog>().IdsAsync();

        public override VisualNode Build(ComponentContext context) =>
            Column(children: [.. _ids.Select(VisualNode (id) => new Item(id))]);
    }

    [Fact]
    public async Task AComponentTheDataBrings_LoadsToo()
    {
        using var tester = new ComponentTester(services: Services());

        await tester.PumpPageAsync(new CatalogPage());

        tester.Semantics.Select(stop => stop.Name).Should().Equal("Title a", "Title b");
    }

    private sealed class Chain(int depth) : StatelessComponent, IServerPrefetch
    {
        private bool _loaded;

        [ServerOnly]
        public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            _loaded = true;
            return Task.CompletedTask;
        }

        public override VisualNode Build(ComponentContext context) =>
            _loaded ? Column(children: [Text($"Link {depth}"), new Chain(depth + 1)]) : Text("waiting");
    }

    [Fact]
    public async Task APageThatNeverSettles_FailsRatherThanDrawingDefaults()
    {
        using var tester = new ComponentTester();

        var pump = () => tester.PumpPageAsync(new Chain(0));

        (await pump.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("still asks for data after 5 rounds").And.Contain(nameof(Chain));
    }
}
