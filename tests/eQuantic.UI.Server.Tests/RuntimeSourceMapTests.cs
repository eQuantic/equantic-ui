using System.Net;
using System.Reflection;
using System.Text.Json;
using eQuantic.UI.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The served runtime carries a map that leads a frame inside the SDK to the SDK's own C# (#1108).
/// The runtime holds modules eqc transpiled from the SDK's C# beside the hand-written TypeScript, and
/// it shipped no map, so the development overlay could only show a developer the runtime's
/// JavaScript. The Server's build writes the map with the bundle and has eqc compose it with the map
/// committed beside each transpiled module; the Server embeds it and answers it in Development.
/// </summary>
public class RuntimeSourceMapTests
{
    [Page("/mapped")]
    public sealed class MappedPage : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text("mapped", TypeRole.BodyM);
    }

    /// <summary>
    /// End to end, against the bytes the Server serves: a statement of <c>SheetController</c>'s
    /// constructor, found in the served runtime, leads through the embedded map to the C# line the
    /// module's own committed map gives that statement.
    /// </summary>
    [Fact]
    public void TheServedRuntimesMap_LeadsATranspiledModulesCode_ToItsCSharp()
    {
        var root = Repository.Root();
        var components = Path.Combine(root, "src", "eQuantic.UI.Runtime", "src", "shared", "components");
        var module = File.ReadAllLines(Path.Combine(components, "SheetController.ts"));
        var tsLine = Array.FindIndex(module, line => line.Trim() == "this.revealVersion = 0;");
        tsLine.Should().BeGreaterThan(0, "the anchor is a statement SheetController's constructor holds");
        using var stageOne = JsonDocument.Parse(File.ReadAllText(Path.Combine(components, "SheetController.ts.map")));
        var expected = Mappings(stageOne.RootElement)[tsLine].Should().NotBeEmpty("eqc maps the statement").And.Subject.First();

        var runtime = Embedded("eQuantic.UI.Server.runtime.js");
        const string anchor = "this.revealVersion=0";
        var at = runtime.IndexOf(anchor, StringComparison.Ordinal);
        at.Should().BeGreaterThan(0, "the bundle holds the statement");
        runtime.IndexOf(anchor, at + 1, StringComparison.Ordinal).Should().Be(-1, "the anchor names one place");
        var jsLine = runtime[..at].Count(c => c == '\n');
        var jsColumn = at - (runtime.LastIndexOf('\n', at - 1) + 1);

        using var map = JsonDocument.Parse(Embedded("eQuantic.UI.Server.runtime.js.map"));
        var sources = map.RootElement.GetProperty("sources").EnumerateArray().Select(s => s.GetString()!).ToArray();
        var segment = Mappings(map.RootElement)[jsLine].Last(s => s[0] <= jsColumn);

        segment.Should().HaveCount(4, "the position has a source");
        sources[segment[1]].Should().Be("eQuantic.UI.Sheet/Editing/SheetController.cs",
            "a frame inside a transpiled module is named by the SDK's C#, from src/");
        segment[2].Should().Be(expected[2], "the line is the one the module's own map gives the statement");
        map.RootElement.TryGetProperty("sourcesContent", out _).Should().BeFalse(
            "the map names the SDK's sources, it does not carry them");
        sources.Should().Contain("eQuantic.UI.Runtime/src/shared/lowering.ts",
            "the hand-written runtime is named by its module, from src/");
        sources.Should().OnlyContain(source => !source.StartsWith("..") && !Path.IsPathRooted(source),
            "no source names the build machine's layout");
    }

    [Theory]
    [InlineData("Development", HttpStatusCode.OK)]
    [InlineData("Production", HttpStatusCode.NotFound)]
    public async Task TheMap_IsAnsweredInDevelopmentAlone(string environment, HttpStatusCode status)
    {
        var (app, client) = await StartAsync(environment);
        await using var _ = app;

        using var answer = await client.GetAsync("/_equantic/runtime.js.map");

        answer.StatusCode.Should().Be(status);
        if (status == HttpStatusCode.OK)
        {
            answer.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
            using var map = JsonDocument.Parse(await answer.Content.ReadAsStringAsync());
            map.RootElement.GetProperty("version").GetInt32().Should().Be(3);
        }
    }

    /// <summary>The overlay's report names the SDK an error was raised in, read from the Server's own
    /// assembly, and a page outside Development says nothing of it.</summary>
    [Theory]
    [InlineData("Development", true)]
    [InlineData("Production", false)]
    public async Task TheSdksVersion_ReachesTheClientInDevelopmentAlone(string environment, bool carried)
    {
        var (app, client) = await StartAsync(environment);
        await using var _ = app;

        var config = ShellConfig.In(await client.GetStringAsync("/mapped"));

        var version = config.GetProperty("sdkVersion");
        if (carried)
            version.GetString().Should().Be(typeof(UIExtensions).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion);
        else
            version.ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>An app as it runs, its content root holding a web root: outside Development the map's
    /// route is the modules' maps' route, which looks there.</summary>
    private static async Task<(WebApplication App, HttpClient Client)> StartAsync(string environment)
    {
        var contentRoot = Path.Combine(AppContext.BaseDirectory, "runtime-map-app");
        Directory.CreateDirectory(Path.Combine(contentRoot, "wwwroot"));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environment,
            ContentRootPath = contentRoot,
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddUI(options => options.ScanAssembly(Assembly.GetExecutingAssembly()));
        var app = builder.Build();
        app.MapUI();
        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    private static string Embedded(string name)
    {
        using var stream = typeof(UIExtensions).Assembly.GetManifestResourceStream(name);
        stream.Should().NotBeNull($"the Server embeds {name}");
        using var reader = new StreamReader(stream!);
        return reader.ReadToEnd();
    }

    private const string Digits = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

    /// <summary>A map's segments per generated line, each [column, source, line, column] with its values
    /// summed, read by a decoder of this file's own rather than the writer's.</summary>
    private static List<List<int[]>> Mappings(JsonElement map)
    {
        var lines = new List<List<int[]>> { new() };
        int column = 0, source = 0, sourceLine = 0, sourceColumn = 0;
        var fields = new List<int>();
        void End()
        {
            if (fields.Count == 0) return;
            column += fields[0];
            if (fields.Count >= 4)
            {
                (source, sourceLine, sourceColumn) = (source + fields[1], sourceLine + fields[2], sourceColumn + fields[3]);
                lines[^1].Add([column, source, sourceLine, sourceColumn]);
            }
            else
            {
                lines[^1].Add([column]);
            }
            fields.Clear();
        }

        var mappings = map.GetProperty("mappings").GetString()!;
        for (var i = 0; i < mappings.Length;)
        {
            if (mappings[i] is ';' or ',')
            {
                End();
                if (mappings[i] == ';')
                {
                    lines.Add([]);
                    column = 0;
                }
                i++;
                continue;
            }
            int value = 0, shift = 0, digit;
            do
            {
                digit = Digits.IndexOf(mappings[i++]);
                value += (digit & 31) << shift;
                shift += 5;
            } while ((digit & 32) != 0);
            fields.Add((value & 1) == 1 ? -(value >> 1) : value >> 1);
        }
        End();
        return lines;
    }
}
