using eQuantic.UI.Design;
using FluentAssertions;
using Xunit.Abstractions;

namespace eQuantic.UI.Design.Tests;

public sealed class ZzEamNoParensProbeTests : IDisposable
{
    private readonly ITestOutputHelper _out;
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "eq-noparens-" + Guid.NewGuid().ToString("N"));
    private readonly string _probe;
    private readonly DesignSession _session = new();

    private const string Source = """
        using eQuantic.UI.Core;
        using eQuantic.UI.Primitives;
        using static eQuantic.UI.Components.UI;
        using StatefulComponent = eQuantic.UI.Primitives.StatefulComponent;

        [Page("/probe")]
        public sealed class Probe : StatefulComponent
        {
            public override VisualNode Build(ComponentContext context)
            {
                var bare = new Row { AlignSelf = CrossAlign.Center };
                var withParens = new Row() { AlignSelf = CrossAlign.Center };
                return Row(gap: Space.S2, children: [
                    bare,
                    withParens,
                ]);
            }
        }
        """;

    public ZzEamNoParensProbeTests(ITestOutputHelper output)
    {
        _out = output;
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

    [Fact]
    public void Probe()
    {
        var origin = OriginOf("new Row { AlignSelf");
        var inspect = _session.Inspect(_probe, Source, origin);
        _out.WriteLine("=== bare `new Row { … }` ===");
        _out.WriteLine("node: " + (inspect?.Component ?? "<null>") + " form=" + (inspect?.Form ?? "<null>"));
        foreach (var p in inspect?.Properties ?? Array.Empty<NodeProperty>())
        {
            _out.WriteLine($"  {p.Name} : {p.Type} kind={p.Kind} editable={p.Editable} reason={p.Reason}");
        }

        var edit = _session.SetProperty(_probe, Source, origin, "gap", "4");
        _out.WriteLine("SetProperty(gap=4) applied=" + edit.Applied + " reason=" + edit.Reason);
        _out.WriteLine("newText=" + edit.NewText);

        var edit2 = _session.SetProperty(_probe, Source, origin, "Gap", "4");
        _out.WriteLine("SetProperty(Gap=4) applied=" + edit2.Applied + " reason=" + edit2.Reason);
        _out.WriteLine("newText=" + edit2.NewText);

        var origin2 = OriginOf("new Row() { AlignSelf");
        var edit3 = _session.SetProperty(_probe, Source, origin2, "gap", "4");
        _out.WriteLine("=== with parens: SetProperty(gap=4) applied=" + edit3.Applied + " reason=" + edit3.Reason);
        _out.WriteLine("newText=" + edit3.NewText);
    }
}
