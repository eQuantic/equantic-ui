using eQuantic.UI.Design;
using FluentAssertions;

namespace eQuantic.UI.Design.Tests;

public sealed class ZzEamStalePaletteTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "eq-eam-" + Guid.NewGuid().ToString("N"));
    private readonly string _probe;
    private readonly DesignSession _session = new();

    private const string Source = """
        using System;
        using eQuantic.UI.Core;
        using eQuantic.UI.Primitives;
        using eQuantic.UI.Components;
        using static eQuantic.UI.Components.UI;
        using StatefulComponent = eQuantic.UI.Primitives.StatefulComponent;

        [Page("/probe")]
        public sealed class Probe : StatefulComponent
        {
            public override VisualNode Build(ComponentContext context)
            {
                return Column(gap: 8, children: [
                    Tabs(labels: ["One", "Two"], selected: 0),
                    Text("a", TypeRole.BodyM, context.Theme.TextPrimary),
                ]);
            }
        }
        """;

    public ZzEamStalePaletteTests()
    {
        Directory.CreateDirectory(Path.Combine(_directory, "Screens"));
        _probe = Path.Combine(_directory, "Screens", "Probe.cs");
        File.WriteAllText(_probe, Source);
        File.WriteAllText(Path.Combine(_directory, "Probe.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var references = new[]
            {
                typeof(eQuantic.UI.Primitives.VisualNode).Assembly,
                typeof(eQuantic.UI.Components.UI).Assembly,
                typeof(eQuantic.UI.Core.PageAttribute).Assembly,
                typeof(object).Assembly,
            }
            .Concat(AppDomain.CurrentDomain.GetAssemblies())
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            .Select(assembly => assembly.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var refsFile = Path.Combine(_directory, "equantic.refs.txt");
        File.WriteAllLines(refsFile, references);
        _session.Initialize(_directory, refsFile, generatedDir: null);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private string OriginOf(string fragment)
    {
        var lines = Source.Replace("\r\n", "\n").Split('\n');
        var line = Array.FindIndex(lines, l => l.Contains(fragment, StringComparison.Ordinal));
        line.Should().BeGreaterThanOrEqualTo(0, $"the probe should contain '{fragment}'");
        var column = lines[line].IndexOf(fragment, StringComparison.Ordinal);
        return $"{_probe}|{line}:{column}|{line}:{column + fragment.Length}";
    }

    private string TheTabs => OriginOf("Tabs(labels:");

    [Fact]
    public void StalePaletteProbe()
    {
        var report = new System.Text.StringBuilder();

        // 1. What the panel is told about the list, on FRESH text.
        var node = _session.Inspect(_probe, Source, TheTabs);
        report.AppendLine("FRESH LISTS: " + (node?.Lists is null
            ? "none"
            : string.Join(" ;; ", node.Lists.Select(l => $"{l.Name} visual={l.Visual} element={l.ElementType} count={l.Count}"))));

        // 2. The palette on FRESH text: this is what the cache SHOULD hold.
        var fresh = _session.Palette(_probe, Source, TheTabs, "labels");
        report.AppendLine($"FRESH PALETTE ({fresh.Length}): "
            + string.Join(" | ", fresh.Take(8).Select(e => $"{e.Name}=>{e.Snippet}[{e.Source}]")));

        // 3. Enter at the top of the file: every line shifts down one, the origin does not.
        var shifted = "\n" + Source;
        var stale = _session.Palette(_probe, shifted, TheTabs, "labels");
        report.AppendLine($"STALE PALETTE ({stale.Length}): "
            + string.Join(" | ", stale.Take(8).Select(e => $"{e.Name}=>{e.Snippet}[{e.Source}]")));

        // 4. Does an entry from the STALE palette get refused when applied to a FRESH origin?
        var first = stale.FirstOrDefault();
        if (first is not null)
        {
            var edit = _session.InsertChild(_probe, Source, TheTabs, 2, first.Snippet, "labels");
            report.AppendLine($"INSERT '{first.Snippet}' applied={edit.Applied} reason={edit.Reason}");
        }

        // 5. And the fresh palette's own first entry, for comparison.
        var freshFirst = fresh.FirstOrDefault();
        if (freshFirst is not null)
        {
            var edit = _session.InsertChild(_probe, Source, TheTabs, 2, freshFirst.Snippet, "labels");
            report.AppendLine($"INSERT-FRESH '{freshFirst.Snippet}' applied={edit.Applied} reason={edit.Reason}");
        }

        throw new Exception(report.ToString());
    }
}
