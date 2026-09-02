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
        using StatefulComponent = eQuantic.UI.Primitives.StatefulComponent;

        [Page("/probe")]
        public sealed class Probe : StatefulComponent
        {
            public override VisualNode Build(ComponentContext context)
            {
                // Imperative, and the construction is the call's ONLY argument — the shape that used
                // to resolve to Add() instead of to the node.
                var column = new Column(gap: Space.S2);
                column.Add(new Text("only", TypeRole.BodyM, context.Theme.TextPrimary));

                return Row(gap: Space.S2, children: [
                    Text("hello", TypeRole.BodyM, context.Theme.TextPrimary),
                    column,
                ]);
            }
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
    /// difference between choosing a value and having to know one. QUALIFIED, because what the panel
    /// offers is what gets written into the file, and a bare member name would not compile.</summary>
    [Fact]
    public void AnEnumParameter_CarriesItsMembers()
    {
        var cross = Inspect("Row(gap:").Properties.Single(p => p.Name == "cross");

        cross.Options.Should().NotBeNull();
        cross.Options.Should().Contain(["CrossAlign.Start", "CrossAlign.Center", "CrossAlign.Stretch"]);
    }

    /// <summary>
    /// The honest half. `Width` is init-only and reachable only through an object initializer, which
    /// a factory call does not have — and the factory surface exists precisely so nobody writes one.
    /// Saying so beats silently rewriting `Row(…)` into `new Row(…) { … }` behind the author's back.
    /// </summary>
    [Fact]
    public void AnInitOnlyMember_OnAFactoryCall_IsReportedUnreachableWithTheReason()
    {
        // AlignSelf is init-only on VisualNode and no factory carries it — the shape Width had until
        // the layout tail (width/height on the containers) turned Width into a parameter; see below.
        var alignSelf = Inspect("Row(gap:").Properties.Single(p => p.Name == "AlignSelf");

        alignSelf.Editable.Should().BeFalse();
        alignSelf.Reason.Should().Contain("object initializer");
    }

    /// <summary>The inspector reads the FACTORY's parameters, not a list: when Row gained `width` and
    /// `height`, both showed up here as editable arguments — and the init-only property they cover
    /// left the unreachable list — without anyone telling the inspector.</summary>
    [Fact]
    public void AMemberTheFactoryCarries_IsAnEditableArgument_EvenThoughThePropertyIsInitOnly()
    {
        var properties = Inspect("Row(gap:").Properties;

        properties.Single(p => p.Name == "width").Editable.Should().BeTrue();
        properties.Single(p => p.Name == "height").Editable.Should().BeTrue();
        properties.Should().NotContain(p => p.Name == "Width", "covered by the parameter, so not listed twice");
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

    /// <summary>
    /// A node that is a call's ONLY argument resolves to ITSELF.
    /// <para>
    /// `column.Add(new Text(…))` gives the ArgumentSyntax exactly the same span as the construction
    /// inside it, and Roslyn's FindNode returns the OUTERMOST of a tie unless asked otherwise — so
    /// the walk up landed on Add(), whose symbol returns void. The panel introduced a FormInput as
    /// "Void" and offered to edit `child`, which is Add's parameter. Every node added imperatively
    /// had this, which is most of every real screen.
    /// </para>
    /// </summary>
    [Fact]
    public void ANodeThatIsACallsOnlyArgument_ResolvesToItself_NotToTheCall()
    {
        var node = Inspect("new Text(\"only\"");

        node.Component.Should().Be("Text");
        node.Properties.Select(p => p.Name).Should().Contain("content");
    }

    /// <summary>
    /// Children are STRUCTURE, not a value. A text box over a subtree is an invitation to replace a
    /// screen with a typo, and the canvas already has the gesture that belongs here — click the child.
    /// </summary>
    [Fact]
    public void ChildrenAreNotOfferedAsAProperty()
    {
        var names = Inspect("Row(gap:").Properties.Select(p => p.Name);

        names.Should().NotContain("children");
        names.Should().Contain("gap", "the value-shaped parameters are still there");
    }

    /// <summary>The design stamp is the TOOL's own scaffolding; offering it as an editable property
    /// would be the inspector inviting someone to edit the thing that makes it work.</summary>
    [Fact]
    public void TheDesignStampIsNotOfferedAsAProperty()
    {
        var names = Inspect("Row(gap:").Properties.Select(p => p.Name);

        names.Should().NotContain("Origin").And.NotContain("OriginLabel");
    }

    [Fact]
    public void AnOriginFromAnotherFile_IsNotInspected()
    {
        _session.Inspect(_probe, Source, $"{Path.Combine(_directory, "Other.cs")}|1:0|1:5").Should().BeNull();
    }
}
