using eQuantic.UI.Design;
using FluentAssertions;

namespace eQuantic.UI.Design.Tests;

public sealed class ZzRefuteProbeTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "eq-refute-" + Guid.NewGuid().ToString("N"));
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
                return new Box(new BoxStyle());
            }
        }
        """;

    public ZzRefuteProbeTests()
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

    private static string Apply(string text, EditResult edit)
    {
        var source = Microsoft.CodeAnalysis.Text.SourceText.From(text);
        var start = source.Lines[edit.StartLine].Start + edit.StartColumn;
        var end = source.Lines[edit.EndLine].Start + edit.EndColumn;
        return source.Replace(Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(start, end), edit.NewText).ToString();
    }

    private string OriginOf(string body, string fragment)
    {
        var lines = body.Replace("\r\n", "\n").Split('\n');
        var line = Array.FindIndex(lines, l => l.Contains(fragment, StringComparison.Ordinal));
        line.Should().BeGreaterThanOrEqualTo(0);
        var column = lines[line].IndexOf(fragment, StringComparison.Ordinal);
        return $"{_probe}|{line}:{column}|{line}:{column + fragment.Length}";
    }

    [Fact]
    public void SecondPropertyOnSameNewNode()
    {
        var origin = OriginOf(Source, "new Box(new BoxStyle");

        var first = _session.SetProperty(_probe, Source, origin, "AlignSelf", "CrossAlign.Center");
        Console.WriteLine($"STEP1 applied={first.Applied} reason={first.Reason}");
        first.Applied.Should().BeTrue(first.Reason);
        var after = Apply(Source, first);
        Console.WriteLine("AFTER1:\n" + after);

        var origin2 = OriginOf(after, "new Box(new BoxStyle");
        var inspect = _session.Inspect(_probe, after, origin2);
        foreach (var p in inspect!.Properties)
            Console.WriteLine($"PROP {p.Name} kind={p.Kind} value={p.Value} editable={p.Editable} reason={p.Reason}");

        var second = _session.SetProperty(_probe, after, origin2, "GridSpan", "2");
        Console.WriteLine($"STEP2 applied={second.Applied} reason={second.Reason}");
        if (second.Applied) Console.WriteLine("AFTER2:\n" + Apply(after, second));

        // also Key
        var third = _session.SetProperty(_probe, after, origin2, "Key", "\"k\"");
        Console.WriteLine($"STEP3(Key) applied={third.Applied} reason={third.Reason}");
        if (third.Applied) Console.WriteLine("AFTER3:\n" + Apply(after, third));
    }
}
