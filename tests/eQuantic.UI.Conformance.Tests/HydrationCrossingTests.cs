using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using eQuantic.UI.Compiler;
using eQuantic.UI.Compiler.Services;
using eQuantic.UI.Conformance.Tests.Hydration;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using eQuantic.UI.Server;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The hydration crossing with both halves executed: the server renders a page and writes its payload,
/// and the page's twin, transpiled from the same source, builds in the embedded Bun on that payload. The
/// browser's first build has to draw what the server drew.
/// <para>
/// Each half had tests of its own, and each agreed with itself. The server named an auto-property after
/// the field the C# compiler synthesized and a captured parameter after its storage, the twin declared
/// both under eqc's names, and the values drew on the server and vanished on hydration (#509, #510).
/// Only running one half on the other's output shows that.
/// </para>
/// <para>
/// The twin builds the way the router builds a page, with no arguments, on the runtime the Server
/// serves. It renders without a DOM: adopting the payload, building and lowering are the steps that
/// decide what the first build draws, and none of them touches the document.
/// </para>
/// </summary>
public class HydrationCrossingTests
{
    private static readonly string[] Fixtures = ["CrossingPage.cs", "CrossingQuote.cs"];

    [Fact]
    public async Task TheTwinDrawsWhatTheServerDrew_OnTheServedPayload_AndOnANavigation()
    {
        var bun = JsExecutor.RequireBun();
        bun.Should().NotBeNullOrEmpty();

        var (html, served, navigation) = await RenderOnTheServerAsync();
        html.Should().Contain("downloads 42").And.Contain("quote ACME",
            "the server draws what the prefetches loaded, which is what the twin has to match");

        var drawn = DrawTheTwin(served, navigation);

        drawn[0].Should().Contain("downloads 42").And.Contain("quote ACME",
            "the first build after hydration draws the values the server wrote into the document");
        drawn[1].Should().Contain("downloads 42").And.Contain("quote ACME",
            "a client navigation hands the router the same values, under the same names");
    }

    /// <summary>The served document, the payload it carries, and the state a navigation receives.</summary>
    private static async Task<(string Html, string Served, string Navigation)> RenderOnTheServerAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddUI(options => options.ScanAssembly(typeof(CrossingPage).Assembly));
        var app = builder.Build();
        app.MapUI();
        await app.StartAsync();
        await using var _ = app;
        var client = app.GetTestClient();

        var html = await client.GetStringAsync("/crossing");
        var payload = Regex.Match(html, @"window\.__INITIAL_STATE__\s*=\s*(\{.*?\});", RegexOptions.Singleline);
        payload.Success.Should().BeTrue("a page with prefetching components carries their state in the document");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/crossing");
        request.Headers.Add("X-EQ-Navigate", "1");
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var navigation = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return (html, payload.Groups[1].Value, navigation.RootElement.GetProperty("state").GetRawText());
    }

    /// <summary>
    /// The texts the page's twin draws on each payload, in order: transpiled by eqc from the fixture
    /// sources and the hydration manifest the generator wrote for this assembly, as an app's build
    /// transpiles them, and run on the runtime the Server serves.
    /// </summary>
    private static string[] DrawTheTwin(params string[] payloads)
    {
        var root = RepoRoot.Find() ?? throw new InvalidOperationException("No repository root.");
        var runtime = ConformanceRunner.RuntimeJsUrl() ?? throw new InvalidOperationException("No served runtime.js.");
        var project = Path.Combine(root, "tests", "eQuantic.UI.Conformance.Tests");
        var sources = Fixtures.Select(name => Path.Combine(project, "Hydration", name)).ToList();

        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(ProjectCompilationHelper.CreateCompilationFromSources(
            sources.Append(Manifest(project)),
            [typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location],
            "Crossing"));

        var modules = Path.Combine(Path.GetTempPath(), $"eq-crossing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(modules);
        try
        {
            foreach (var source in sources)
            {
                foreach (var twin in compiler.CompileSource(File.ReadAllText(source), source))
                {
                    twin.Success.Should().BeTrue(string.Join("\n", twin.Errors.Select(e => e.Message)));
                    // The bare module the page's import map names, which a script has no map for.
                    File.WriteAllText(Path.Combine(modules, twin.ComponentName + ".ts"),
                        twin.TypeScript.Replace("\"@equantic/runtime\"", $"\"{runtime}\""));
                }
            }

            var program = $$"""
                import { CrossingPage } from '{{new Uri(Path.Combine(modules, "CrossingPage.ts")).AbsoluteUri}}';
                globalThis.window = globalThis;
                const texts = (node, into) => {
                  if (!node) return into;
                  if (node.tag === '#text') into.push(node.textContent);
                  for (const child of node.children ?? []) texts(child, into);
                  return into;
                };
                for (const payload of {{JsonSerializer.Serialize(payloads.Select(p => JsonDocument.Parse(p).RootElement))}}) {
                  // What the boot hands a page on a load, and the router on a navigation: the payload in
                  // the hydration door, and a page built with no arguments.
                  globalThis.__INITIAL_STATE__ = payload;
                  console.log('=' + texts(new CrossingPage().render(), []).join(' | '));
                }
                """;
            var output = JsExecutor.Run(program, timeoutMs: 120_000).Split('\n');
            return output.Where(line => line.StartsWith('=')).Select(line => line.TrimEnd('\r')[1..]).ToArray()
                is { Length: 2 } drawn
                ? drawn
                : throw new InvalidOperationException($"The twin drew no answer for each payload:\n{string.Join('\n', output)}");
        }
        finally
        {
            Directory.Delete(modules, recursive: true);
        }
    }

    /// <summary>
    /// The manifest the C# compiler compiled into this assembly, read from where the generator wrote it
    /// on disk for the configuration that built this test: the file eqc reads in an app's build.
    /// </summary>
    private static string Manifest(string project)
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var framework = new DirectoryInfo(AppContext.BaseDirectory).Name;
        var generated = Path.Combine(project, "obj", configuration, framework, "generated");
        return ProjectCompilationHelper.GetCompilerGeneratedFiles(project, generated)
            .SingleOrDefault(file => Path.GetFileName(file) == "HydrationManifest.g.cs")
            ?? throw new InvalidOperationException($"The generator wrote no hydration manifest under {generated}.");
    }
}
