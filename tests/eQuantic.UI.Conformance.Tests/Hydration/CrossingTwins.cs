using System.Text.Json;
using eQuantic.UI.Compiler;
using eQuantic.UI.Compiler.Services;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using FluentAssertions;

namespace eQuantic.UI.Conformance.Tests.Hydration;

/// <summary>
/// The twins of the crossing's fixtures, transpiled once for every case: eqc over every source in this
/// folder and every source the C# compiler generated for this assembly (the hydration manifest among
/// them), as an app's build transpiles them, and run by the embedded Bun on the runtime the Server
/// serves.
/// </summary>
public sealed class CrossingTwins : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"eq-crossing-{Guid.NewGuid():N}");

    public CrossingTwins()
    {
        var root = RepoRoot.Find() ?? throw new InvalidOperationException("No repository root.");
        var runtime = ConformanceRunner.RuntimeJsUrl() ?? throw new InvalidOperationException("No served runtime.js.");
        var project = Path.Combine(root, "tests", "eQuantic.UI.Conformance.Tests");
        // Read off the folder, never listed: a fixture added here is a fixture transpiled.
        var sources = Directory.GetFiles(Path.Combine(project, "Hydration"), "Crossing*.cs")
            .Where(file => Path.GetFileName(file) != "CrossingTwins.cs")
            .ToList();
        var generated = Generated(project);

        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(ProjectCompilationHelper.CreateCompilationFromSources(
            sources.Concat(generated),
            [typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location],
            "Crossing"));

        Directory.CreateDirectory(_folder);
        foreach (var source in sources)
        foreach (var twin in compiler.CompileSource(File.ReadAllText(source), source))
        {
            twin.Success.Should().BeTrue(string.Join("\n", twin.Errors.Select(e => e.Message)));
            // The bare module the page's import map names, which a script has no map for.
            File.WriteAllText(Path.Combine(_folder, twin.ComponentName + ".ts"),
                twin.TypeScript.Replace("\"@equantic/runtime\"", $"\"{runtime}\""));
        }
    }

    /// <summary>
    /// What a page's twin draws on each payload, in order: every text, and the destination of every
    /// link, as its first build lowers them. The twin is built the way the boot builds a page on a load
    /// and the router on a navigation: the payload in the hydration door, and no arguments.
    /// </summary>
    public string[] Draw(string page, params string[] payloads)
    {
        var program = $$"""
            import { {{page}} } from '{{new Uri(Path.Combine(_folder, page + ".ts")).AbsoluteUri}}';
            globalThis.window = globalThis;
            const draw = (node, into) => {
              if (!node) return into;
              if (node.tag === '#text') into.push(node.textContent);
              if (node.tag === 'a') into.push('href ' + node.attributes?.href);
              for (const child of node.children ?? []) draw(child, into);
              return into;
            };
            for (const payload of {{JsonSerializer.Serialize(payloads.Select(p => JsonDocument.Parse(p).RootElement))}}) {
              globalThis.__INITIAL_STATE__ = payload;
              console.log('=' + draw(new {{page}}().render(), []).join(' | '));
            }
            """;
        var output = JsExecutor.Run(program, timeoutMs: 120_000).Split('\n');
        var drawn = output.Where(line => line.StartsWith('=')).Select(line => line.TrimEnd('\r')[1..]).ToArray();
        return drawn.Length == payloads.Length
            ? drawn
            : throw new InvalidOperationException($"The twin drew no answer for each payload:\n{string.Join('\n', output)}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    /// <summary>
    /// The sources the generators wrote for this assembly, read from where they wrote them for the
    /// configuration that built this test: the files eqc reads in an app's build.
    /// </summary>
    private static IReadOnlyList<string> Generated(string project)
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var framework = new DirectoryInfo(AppContext.BaseDirectory).Name;
        var directory = Path.Combine(project, "obj", configuration, framework, "generated");
        var files = ProjectCompilationHelper.GetCompilerGeneratedFiles(project, directory).ToList();
        files.Should().Contain(file => Path.GetFileName(file) == "HydrationManifest.g.cs",
            $"the generator writes the hydration manifest under {directory}");
        return files;
    }
}
