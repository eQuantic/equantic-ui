using eQuantic.UI.Design;
using FluentAssertions;

namespace eQuantic.UI.Design.Tests;

public sealed class ZzNestedScaleProbeTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "eq-nested-" + Guid.NewGuid().ToString("N"));
    private readonly string _probe;
    private readonly DesignSession _session = new();

    private const string Source = """
        using eQuantic.UI.Core;
        using eQuantic.UI.Primitives;
        using static eQuantic.UI.Components.UI;
        using StatefulComponent = eQuantic.UI.Primitives.StatefulComponent;

        public static class Tokens
        {
            public static class Gaps
            {
                public const float Tight = 4;
                public const float Loose = 16;
            }
        }

        [Page("/probe")]
        public sealed class Probe : StatefulComponent
        {
            public override VisualNode Build(ComponentContext context)
            {
                return Row(gap: Tokens.Gaps.Tight, children: [
                    Text("hello", TypeRole.BodyM, context.Theme.TextPrimary),
                ]);
            }
        }
        """;

    public ZzNestedScaleProbeTests()
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

    [Fact]
    public void WhatDoesTheNestedScaleOffer()
    {
        var node = _session.Inspect(_probe, Source, OriginOf("Row(gap:"));
        node.Should().NotBeNull();
        var gap = node!.Properties.Single(p => p.Name == "gap");

        Console.WriteLine("VALUE: " + gap.Value);
        Console.WriteLine("OPTIONS: " + (gap.Options is null ? "<null>" : string.Join(" | ", gap.Options)));

        if (gap.Options is { Length: > 0 })
        {
            foreach (var option in gap.Options)
            {
                var edit = _session.SetProperty(_probe, Source, OriginOf("Row(gap:"), "gap", option);
                Console.WriteLine($"SET '{option}' -> Applied={edit.Applied} Reason={edit.Reason}");
            }
        }

        true.Should().BeTrue();
    }
}
