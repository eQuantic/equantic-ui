using eQuantic.UI.Design;
using FluentAssertions;

namespace eQuantic.UI.Design.Tests;

public sealed class ZzProbeTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "eq-probe-" + Guid.NewGuid().ToString("N"));
    private readonly string _probe;
    private readonly DesignSession _session = new();

    private const string Source = """
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
                var empty = new Box(new BoxStyle());   // EMPTYARGS
                var braces = new Box(new BoxStyle { });
                var grid1 = Grid(columns: [GridTrack.Flex()], gap: 12, children: []);
                var gridT = Grid(columns: [GridTrack.Flex(), GridTrack.Fixed(120),], gap: 12, children: []);
                return new Box(new BoxStyle
                {
                    Padding = EdgeInsets.All(Space.S4),
                });
            }
        }
        """;

    public ZzProbeTests()
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

    private string OriginOf(string text, string fragment)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var line = Array.FindIndex(lines, l => l.Contains(fragment, StringComparison.Ordinal));
        line.Should().BeGreaterThanOrEqualTo(0, $"the probe should contain '{fragment}'");
        var column = lines[line].IndexOf(fragment, StringComparison.Ordinal);
        return $"{_probe}|{line}:{column}|{line}:{column + fragment.Length}";
    }

    /// <summary>Origin of `new Box(new BoxStyle` on the line that starts with `return`.</summary>
    private string MultiLineBox(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var line = Array.FindIndex(lines, l => l.Contains("return new Box(new BoxStyle", StringComparison.Ordinal));
        line.Should().BeGreaterThanOrEqualTo(0);
        var column = lines[line].IndexOf("new Box(new BoxStyle", StringComparison.Ordinal);
        return $"{_probe}|{line}:{column}|{line}:{column + 20}";
    }

    private static string Apply(string text, EditResult edit)
    {
        var source = Microsoft.CodeAnalysis.Text.SourceText.From(text);
        var start = source.Lines[edit.StartLine].Start + edit.StartColumn;
        var end = source.Lines[edit.EndLine].Start + edit.EndColumn;
        return source.Replace(Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(start, end), edit.NewText).ToString();
    }

    /// <summary>H1: branch 4 works exactly once per node.</summary>
    [Fact]
    public void SecondPropertyAfterBranch4()
    {
        var first = _session.SetProperty(_probe, Source, MultiLineBox(Source), "AlignSelf", "CrossAlign.Center");
        first.Applied.Should().BeTrue(first.Reason);
        var after = Apply(Source, first);
        Console.WriteLine("=== AFTER FIRST ===\n" + after);

        var second = _session.SetProperty(_probe, after, MultiLineBox(after), "GridSpan", "2");
        Console.WriteLine("=== SECOND applied=" + second.Applied + " reason=" + second.Reason);
        second.Applied.Should().BeTrue(second.Reason);
    }

    /// <summary>H1b: inspect says the second one is reachable.</summary>
    [Fact]
    public void InspectSaysReachableAfterBranch4()
    {
        var first = _session.SetProperty(_probe, Source, MultiLineBox(Source), "AlignSelf", "CrossAlign.Center");
        var after = Apply(Source, first);
        var node = _session.Inspect(_probe, after, MultiLineBox(after))!;
        var span = node.Properties.Single(p => p.Name == "GridSpan");
        Console.WriteLine("GridSpan editable=" + span.Editable + " reason=" + span.Reason);
        span.Editable.Should().BeTrue();
    }

    /// <summary>SetNested into `new BoxStyle()` with no initializer.</summary>
    [Fact]
    public void NestedIntoEmptyArgList()
    {
        var edit = _session.SetProperty(_probe, Source, OriginOf(Source, "new Box(new BoxStyle())"), "style.BorderWidth", "2");
        Console.WriteLine("applied=" + edit.Applied + " reason=" + edit.Reason);
        edit.Applied.Should().BeTrue(edit.Reason);
        Console.WriteLine(Apply(Source, edit));
    }

    /// <summary>SetNested into `new BoxStyle { }` — empty braces.</summary>
    [Fact]
    public void NestedIntoEmptyBraces()
    {
        var edit = _session.SetProperty(_probe, Source, OriginOf(Source, "new Box(new BoxStyle { })"), "style.BorderWidth", "2");
        Console.WriteLine("applied=" + edit.Applied + " reason=" + edit.Reason);
        edit.Applied.Should().BeTrue(edit.Reason);
        Console.WriteLine(Apply(Source, edit));
    }

    /// <summary>RemoveAt the only element of a single-line list.</summary>
    [Fact]
    public void RemoveOnlyColumn()
    {
        var edit = _session.RemoveAt(_probe, Source, OriginOf(Source, "Grid(columns: [GridTrack.Flex()]"), "columns", 0);
        Console.WriteLine("applied=" + edit.Applied + " reason=" + edit.Reason);
        if (edit.Applied) Console.WriteLine(Apply(Source, edit));
    }

    /// <summary>Insert into a single-line list that carries a TRAILING comma.</summary>
    [Fact]
    public void InsertIntoTrailingCommaSingleLine()
    {
        var edit = _session.InsertChild(_probe, Source, OriginOf(Source, "Grid(columns: [GridTrack.Flex(), GridTrack.Fixed(120),]"), 2, "GridTrack.Auto", "columns");
        Console.WriteLine("applied=" + edit.Applied + " reason=" + edit.Reason);
        if (edit.Applied) Console.WriteLine(Apply(Source, edit));
        edit.Applied.Should().BeTrue(edit.Reason);
    }

    /// <summary>Unset on a path with more than one dot.</summary>
    [Fact]
    public void UnsetDeepPath()
    {
        var edit = _session.UnsetProperty(_probe, Source, MultiLineBox(Source), "style.Hover.Background");
        Console.WriteLine("applied=" + edit.Applied + " reason=" + edit.Reason);
    }

    /// <summary>Unset the ONLY member, written with a trailing comma.</summary>
    [Fact]
    public void UnsetOnlyMemberTrailingComma()
    {
        var edit = _session.UnsetProperty(_probe, Source, MultiLineBox(Source), "style.Padding");
        Console.WriteLine("applied=" + edit.Applied + " reason=" + edit.Reason);
        if (edit.Applied) Console.WriteLine(Apply(Source, edit));
        edit.Applied.Should().BeTrue(edit.Reason);
    }
}
