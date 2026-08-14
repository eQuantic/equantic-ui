using eQuantic.UI.Design;
using FluentAssertions;

namespace eQuantic.UI.Design.Tests;

/// <summary>
/// What the inspector shows for a selected node.
/// <para>
/// Read from the semantic model rather than from a generated catalogue, because the question is
/// about THIS call: which parameters it supplies, what it wrote for them, and which of the type's
/// members the form it is written in can even reach. A catalogue answers "what does Row have"; the
/// panel needs "what does this Row say, and what may I change".
/// </para>
/// </summary>
public sealed class InspectTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "eq-inspect-" + Guid.NewGuid().ToString("N"));
    private readonly string _probe;
    private readonly DesignSession _session = new();

    private const string Source = """
        using eQuantic.UI.Core;
        using eQuantic.UI.Primitives;
        using static eQuantic.UI.Components.UI;

        [Page("/probe")]
        public sealed class Probe : StatefulComponent
        {
            public override VisualNode Build(ComponentContext context) =>
                Row(gap: Space.S2, children: [
                    Text("hello", TypeRole.BodyM, context.Theme.TextPrimary),
                ]);
        }
        """;

    public InspectTests()
    {
        Directory.CreateDirectory(Path.Combine(_directory, "Screens"));
        _probe = Path.Combine(_directory, "Screens", "Probe.cs");
        File.WriteAllText(_probe, Source);
        File.WriteAllText(Path.Combine(_directory, "Probe.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        // The reference set the SDK would have written. Seeded by TYPE, not by whatever the host has
        // touched: the CLR loads lazily, so GetAssemblies() alone is complete only by luck.
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

    /// <summary>Zero-based, like the stamp and like the editor.</summary>
    private string OriginOf(string fragment)
    {
        var lines = Source.Replace("\r\n", "\n").Split('\n');
        var line = Array.FindIndex(lines, l => l.Contains(fragment, StringComparison.Ordinal));
        line.Should().BeGreaterThanOrEqualTo(0, $"the probe should contain '{fragment}'");
        var column = lines[line].IndexOf(fragment, StringComparison.Ordinal);
        return $"{_probe}|{line}:{column}|{line}:{column + fragment.Length}";
    }

    private InspectResult Inspect(string fragment)
    {
        var result = _session.Inspect(_probe, Source, OriginOf(fragment));
        result.Should().NotBeNull();
        return result!;
    }

    [Fact]
    public void AFactoryCall_ReportsItsComponentAndTheArgumentsAsWritten()
    {
        var node = Inspect("Row(gap:");

        node.Component.Should().Be("Row");
        node.Form.Should().Be("factory");

        var gap = node.Properties.Single(p => p.Name == "gap");
        gap.Kind.Should().Be("argument");
        gap.Value.Should().Be("Space.S2");
        gap.Editable.Should().BeTrue();
    }

    /// <summary>A parameter the call did not supply is still an offer — the panel can add a named
    /// argument for it, which is the least invasive edit there is.</summary>
    [Fact]
    public void AParameterTheCallOmitted_IsReportedUnsetButReachable()
    {
        var main = Inspect("Row(gap:").Properties.Single(p => p.Name == "main");

        main.Kind.Should().Be("unset");
        main.Value.Should().BeNull();
        main.Editable.Should().BeTrue();
    }

    /// <summary>An enum is a closed set, so the panel offers a list rather than a text box — the
    /// difference between choosing a value and having to know one.</summary>
    [Fact]
    public void AnEnumParameter_CarriesItsMembers()
    {
        var cross = Inspect("Row(gap:").Properties.Single(p => p.Name == "cross");

        cross.Options.Should().NotBeNull();
        cross.Options.Should().Contain(["Start", "Center", "End", "Stretch"]);
    }

    /// <summary>
    /// The honest half. `Width` is init-only and reachable only through an object initializer, which
    /// a factory call does not have — and the factory surface exists precisely so nobody writes one.
    /// Saying so beats silently rewriting `Row(…)` into `new Row(…) { … }` behind the author's back.
    /// </summary>
    [Fact]
    public void AnInitOnlyMember_OnAFactoryCall_IsReportedUnreachableWithTheReason()
    {
        var width = Inspect("Row(gap:").Properties.Single(p => p.Name == "Width");

        width.Editable.Should().BeFalse();
        width.Reason.Should().Contain("object initializer");
    }

    /// <summary>Inherited members count: a Row's Width lives on FlexNode and its Key on VisualNode,
    /// and asking only the type itself listed none of the properties an author reaches for most.</summary>
    [Fact]
    public void InheritedMembers_AreListedToo()
    {
        var names = Inspect("Row(gap:").Properties.Select(p => p.Name);

        names.Should().Contain("Key").And.Contain("AlignSelf");
    }

    /// <summary>
    /// The prose comes off the SYMBOL, and the framework's symbols arrive as metadata — so this also
    /// pins that the XML documentation is being found beside the reference. It is written a directory
    /// above the ref assembly MSBuild hands over, so nothing finds it by accident.
    /// </summary>
    [Fact]
    public void AFrameworkComponent_CarriesItsDocumentation()
    {
        Inspect("Row(gap:").Summary.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void AnOriginFromAnotherFile_IsNotInspected()
    {
        _session.Inspect(_probe, Source, $"{Path.Combine(_directory, "Other.cs")}|1:0|1:5").Should().BeNull();
    }
}
