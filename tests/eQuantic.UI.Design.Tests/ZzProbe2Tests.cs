using eQuantic.UI.Design;
using FluentAssertions;

namespace eQuantic.UI.Design.Tests;

public sealed class ZzProbe2Tests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "eq-probe2-" + Guid.NewGuid().ToString("N"));
    private readonly string _probe;
    private readonly DesignSession _session = new();

    private const string Source = """
        using System.Collections.Generic;
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
                var bare = new Row();
                var withInit = new Row() { AlignSelf = CrossAlign.Center };
                var emptyInit = new Row() { };
                var braceOnly = new Row { AlignSelf = CrossAlign.Center };
                var argsAndInit = new Row(gap: 4) { AlignSelf = CrossAlign.Center };
                var listy = Grid(columns: new List<GridTrack> { GridTrack.Flex() }, gap: 12, children: []);
                return new Column(children: [bare, withInit, emptyInit, braceOnly, argsAndInit, listy]);
            }
        }
        """;

    public ZzProbe2Tests()
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

    private string OriginOf(string text, string fragment, string token)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var line = Array.FindIndex(lines, l => l.Contains(fragment, StringComparison.Ordinal));
        line.Should().BeGreaterThanOrEqualTo(0, $"the probe should contain '{fragment}'");
        var column = lines[line].IndexOf(token, StringComparison.Ordinal);
        column.Should().BeGreaterThanOrEqualTo(0);
        return $"{_probe}|{line}:{column}|{line}:{column + token.Length}";
    }

    private static string Apply(string text, EditResult edit)
    {
        var source = Microsoft.CodeAnalysis.Text.SourceText.From(text);
        var start = source.Lines[edit.StartLine].Start + edit.StartColumn;
        var end = source.Lines[edit.EndLine].Start + edit.EndColumn;
        return source.Replace(Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(start, end), edit.NewText).ToString();
    }

    private void Report(string label, EditResult edit, string text)
    {
        Console.WriteLine($"## {label}: applied={edit.Applied} reason={edit.Reason}");
        if (edit.Applied)
        {
            var after = Apply(text, edit);
            var line = after.Replace("\r\n", "\n").Split('\n')
                .FirstOrDefault(l => l.Contains(label.Split(' ')[0], StringComparison.Ordinal));
            Console.WriteLine("   -> " + line);
        }
    }

    [Fact]
    public void EveryNewShape()
    {
        Report("withInit ctor-param gap", _session.SetProperty(
            _probe, Source, OriginOf(Source, "var withInit", "new Row()"), "gap", "4"), Source);

        Report("withInit second property GridSpan", _session.SetProperty(
            _probe, Source, OriginOf(Source, "var withInit", "new Row()"), "GridSpan", "2"), Source);

        Report("emptyInit AlignSelf", _session.SetProperty(
            _probe, Source, OriginOf(Source, "var emptyInit", "new Row()"), "AlignSelf", "CrossAlign.Center"), Source);

        Report("braceOnly gap", _session.SetProperty(
            _probe, Source, OriginOf(Source, "var braceOnly", "new Row"), "gap", "4"), Source);

        Report("argsAndInit main", _session.SetProperty(
            _probe, Source, OriginOf(Source, "var argsAndInit", "new Row(gap: 4)"), "main", "MainAlign.Center"), Source);

        Report("bare AlignSelf", _session.SetProperty(
            _probe, Source, OriginOf(Source, "var bare", "new Row()"), "AlignSelf", "CrossAlign.Center"), Source);

        // What the panel SAYS about each of these.
        foreach (var (label, token, frag) in new[]
                 {
                     ("withInit", "new Row()", "var withInit"),
                     ("emptyInit", "new Row()", "var emptyInit"),
                     ("braceOnly", "new Row", "var braceOnly"),
                 })
        {
            var node = _session.Inspect(_probe, Source, OriginOf(Source, frag, token))!;
            var gridSpan = node.Properties.Single(p => p.Name == "GridSpan");
            var gap = node.Properties.Single(p => p.Name == "gap");
            Console.WriteLine($"== {label}: GridSpan editable={gridSpan.Editable} reason={gridSpan.Reason} | gap editable={gap.Editable}");
        }
    }

    [Fact]
    public void CollectionInitializerValue()
    {
        var origin = OriginOf(Source, "var listy", "Grid(");
        var node = _session.Inspect(_probe, Source, origin)!;
        var columns = node.Properties.SingleOrDefault(p => p.Name == "columns");
        Console.WriteLine("columns members = " + (columns?.Members is null
            ? "null"
            : string.Join(", ", columns.Members.Select(m => m.Name + ":" + m.Kind))));
        Console.WriteLine("lists = " + string.Join(", ", (node.Lists ?? []).Select(l => l.Name)));

        var edit = _session.SetProperty(_probe, Source, origin, "columns.Capacity", "5");
        Report("listy capacity", edit, Source);
    }
}
