using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// Two contracts read off the DOCUMENT THAT GOES OVER THE WIRE, rather than off a lowered node.
/// <para>
/// Both were regressions this month, both were found by a person opening a page, and both cost an
/// afternoon across three sessions. Each already has a realizer-level pin — and those pins passed
/// the whole time, because they assert what the realizer produced. This asserts what the page
/// SERVED, which is the thing a browser receives. The gap between the two is where both lived.
/// </para>
/// <para>
/// Deliberately a SYNTHETIC page, and deliberately cheap: no browser, no Chrome in CI, one request.
/// It cannot catch what only appears when properties cross in a real design — a wrapper's width
/// contract, a hover that stops firing — and that half needs geometry and hit-testing against real
/// pages. This is the half that comes free.
/// </para>
/// </summary>
public class ServedDocumentContractTests
{
    /// <summary>Floating chrome, pinned chrome, and a `long` that has to cross the typed boundary.</summary>
    [eQuantic.UI.Core.Page("/served-contract")]
    public sealed class ContractPage : StatelessComponent, IServerPrefetch
    {
        public long Downloads;

        public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            // Past 2^53, so a number that crossed as JSON would not even round-trip exactly.
            Downloads = 9007199254740993L;
            return Task.CompletedTask;
        }

        public override VisualNode Build(ComponentContext context)
        {
            var page = new Column();
            page.Add(new Sticky(new Box(new BoxStyle { Height = 64 })) { Float = true });
            page.Add(new Sticky(new Box(new BoxStyle { Height = 32 }), offset: 96));
            page.Add(new Text($"{Downloads}", TypeRole.BodyM));
            return page;
        }
    }

    private static async Task<string> ServeAsync(string path)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddUI(options => options.ScanAssembly(Assembly.GetExecutingAssembly()));
        var app = builder.Build();
        app.MapUI();
        await app.StartAsync();
        await using var _ = app;
        return await app.GetTestClient().GetStringAsync(path);
    }

    /// <summary>
    /// Every atomic class in the document's stylesheet, to the declarations it carries. Usually one
    /// — that is the point of an atomic class — but not always: a VENDOR PAIR writes both spellings
    /// into a single rule, because `-webkit-backdrop-filter` alone is dropped whole by engines that
    /// only take the standard name. So the value is a SET, and a caller asks whether a declaration
    /// is among them rather than whether it is the whole text.
    /// </summary>
    private static Dictionary<string, string[]> AtomicRules(string html) =>
        Regex.Matches(html, @"\.(eq-[\w-]+)\{([^}]+)\}")
            .ToDictionary(
                m => m.Groups[1].Value,
                m => m.Groups[2].Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                StringComparer.Ordinal);

    /// <summary>
    /// The z-index actually applied to the element that carries <paramref name="declaration"/>.
    /// Atomic CSS means the position and the z-index are DIFFERENT classes on the same element, so
    /// the answer is only visible by pairing the stylesheet back to the element — which is exactly
    /// what a realizer-level test never does.
    /// </summary>
    private static int LayerOfElementDeclaring(string html, string declaration)
    {
        var rules = AtomicRules(html);
        var marker = rules.FirstOrDefault(r => r.Value.Contains(declaration, StringComparer.Ordinal)).Key;
        marker.Should().NotBeNull($"the document has no class declaring `{declaration}`");

        foreach (System.Text.RegularExpressions.Match element in Regex.Matches(html, "class=\"([^\"]+)\""))
        {
            var classes = element.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (!classes.Contains(marker, StringComparer.Ordinal)) continue;

            foreach (var name in classes)
                if (rules.TryGetValue(name, out var declarations))
                    foreach (var value in declarations)
                        if (value.StartsWith("z-index:", StringComparison.Ordinal))
                            return int.Parse(value["z-index:".Length..]);
        }

        throw new InvalidOperationException($"no element declaring `{declaration}` carries a z-index");
    }

    /// <summary>
    /// The tie that let a pinned rail paint over an open mega menu: both were chrome, both were
    /// z-index 100, so the winner was whichever came later in the document.
    /// </summary>
    [Fact]
    public async Task FloatingChrome_OutranksPinnedChrome_InTheServedDocument()
    {
        var html = await ServeAsync("/served-contract");

        var floating = LayerOfElementDeclaring(html, "position:fixed");
        var pinned = LayerOfElementDeclaring(html, "position:sticky");

        floating.Should().BeGreaterThan(pinned,
            "a header and a rail on one page must not tie and let document order decide which paints on top");
    }

    /// <summary>
    /// A `long` is a BigInt on the other side and JSON has no such thing. Crossing as a NUMBER is
    /// how a production page died: the value arrived where a bigint was declared, and past 2^53 it
    /// would not even be the same number.
    /// </summary>
    [Fact]
    public async Task ALongSlot_CrossesAsAString_InTheServedDocument()
    {
        var html = await ServeAsync("/served-contract");

        var payload = Regex.Match(html, @"window\.__INITIAL_STATE__\s*=\s*(\{.*?\});", RegexOptions.Singleline);
        payload.Success.Should().BeTrue("the page prefetches, so the document must carry its state");

        var state = JsonDocument.Parse(payload.Groups[1].Value).RootElement;
        var downloads = state.GetProperty("Downloads");

        downloads.ValueKind.Should().Be(JsonValueKind.String,
            "a long crosses as text — as a JSON number it lands in a bigint slot, and past 2^53 it "
            + "is not even the same number");
        downloads.GetString().Should().Be("9007199254740993");
    }
}
