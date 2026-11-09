using System.Globalization;
using eQuantic.UI.Primitives;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static eQuantic.UI.Components.UI;

namespace eQuantic.UI.Testing.Tests;

/// <summary>
/// THE THREE SHAPES the wiki's Testing page shows, written here exactly as it shows them: a
/// stateless card, a stateful form, and a page that loads server data. The page is copied from these
/// tests, never the other way round, so what an app author copies is code a build has run. They
/// assert with xUnit's own <c>Assert</c>, the lowest common denominator, because the package
/// depends on no test framework and the page is read by people using any of them.
/// </summary>
public class DocumentedShapesTests
{
    // ---- 1. A stateless card -------------------------------------------------------------------

    public sealed class ProfileCard(string name, string role) : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => Card(Column(gap: Space.S2, children:
        [
            Text(name, TypeRole.Title, headingLevel: 2),
            Text(role, TypeRole.BodyM),
        ]));
    }

    [Fact]
    public void AStatelessCard()
    {
        using var tester = new ComponentTester();

        tester.Pump(new ProfileCard("Ana Lima", "Engineer"));

        var name = tester.Find("Ana Lima");
        Assert.Equal(SemanticRole.StaticText, name.Role);
        Assert.Equal(2, name.HeadingLevel);
        Assert.Equal(["Ana Lima", "Engineer"], tester.Semantics.Select(stop => stop.Name));
    }

    // ---- 2. A stateful form --------------------------------------------------------------------

    public sealed class SignupForm : StatefulComponent
    {
        private string _email = "";
        private string? _welcome;

        public override VisualNode Build(ComponentContext context) => Column(gap: Space.S3, children:
        [
            TextInput(_email, email => SetState(() => _email = email), label: "Email"),
            Button("Sign up", onPressed: () => SetState(() => _welcome = $"Welcome, {_email}")),
            Text(_welcome ?? ""),
        ]);
    }

    [Fact]
    public void AStatefulForm()
    {
        using var tester = new ComponentTester();
        tester.Pump(new SignupForm());

        tester.EnterText("Email", "ana@example.com");
        tester.Tap("Sign up");
        tester.Pump();

        Assert.Equal("ana@example.com", tester.Find(SemanticRole.TextField, "Email").Value);
        Assert.Equal(SemanticRole.StaticText, tester.Find("Welcome, ana@example.com").Role);
    }

    // ---- 3. A page that loads server data ------------------------------------------------------

    public interface IDownloads
    {
        Task<long> CountAsync(CancellationToken cancellationToken);
    }

    [Page("/downloads")]
    public sealed class DownloadsPage : StatelessComponent, IServerPrefetch
    {
        private long _downloads;

        [ServerOnly]
        public async Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken) =>
            _downloads = await services.GetRequiredService<IDownloads>().CountAsync(cancellationToken);

        public override VisualNode Build(ComponentContext context) => Text($"Downloads: {_downloads:N0}");
    }

    private sealed class FakeDownloads(long count) : IDownloads
    {
        public Task<long> CountAsync(CancellationToken cancellationToken) => Task.FromResult(count);
    }

    [Fact]
    public async Task APageThatLoadsServerData()
    {
        var services = new ServiceCollection()
            .AddSingleton<IDownloads>(new FakeDownloads(675_617))
            .BuildServiceProvider();
        using var tester = new ComponentTester(culture: new CultureInfo("pt-BR"), services: services);

        await tester.PumpPageAsync(new DownloadsPage());

        Assert.Equal(SemanticRole.StaticText, tester.Find("Downloads: 675.617").Role);
    }
}
