using eQuantic.UI.Design;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Design.Tests;

/// <summary>
/// Writing a property back into the source.
/// <para>
/// The edit is computed here and applied by the EDITOR, through a WorkspaceEdit — so it lands in the
/// document's own undo stack, one Ctrl+Z reverses the whole gesture, and an unsaved buffer stays
/// unsaved. A host that wrote the file itself would be fighting the editor for the same document.
/// </para>
/// </summary>
public sealed class SetPropertyTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "eq-set-" + Guid.NewGuid().ToString("N"));
    private readonly string _probe;
    private readonly DesignSession _session = new();

    private const string Source = """
        using eQuantic.UI.Primitives;
        using static eQuantic.UI.Components.UI;
        using StatefulComponent = eQuantic.UI.Primitives.StatefulComponent;

        [Page("/probe")]
        public sealed class Probe : StatefulComponent
        {
            public override VisualNode Build(ComponentContext context)
            {
                var box = new Box(new BoxStyle { Padding = EdgeInsets.All(Space.S2) });
                var empty = new Row();
                return Row(gap: Space.S2, children: [
                    Text("hello", TypeRole.BodyM, context.Theme.TextPrimary),
                    box,
                ]);
            }
        }
        """;

    public SetPropertyTests()
    {
        Directory.CreateDirectory(Path.Combine(_directory, "Screens"));
        _probe = Path.Combine(_directory, "Screens", "Probe.cs");
        File.WriteAllText(_probe, Source);
        File.WriteAllText(Path.Combine(_directory, "Probe.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var references = new[]
            {
                typeof(eQuantic.UI.Primitives.VisualNode).Assembly,
                typeof(eQuantic.UI.Components.UI).Assembly,
                typeof(eQuantic.UI.Primitives.PageAttribute).Assembly,
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

    /// <summary>Applies an edit to the text the way the editor would, so the assertions are about
    /// the resulting DOCUMENT rather than about a span in the abstract.</summary>
    private static string Apply(string text, EditResult edit)
    {
        var source = Microsoft.CodeAnalysis.Text.SourceText.From(text);
        var start = source.Lines[edit.StartLine].Start + edit.StartColumn;
        var end = source.Lines[edit.EndLine].Start + edit.EndColumn;
        return source.Replace(Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(start, end), edit.NewText).ToString();
    }

    [Fact]
    public void SettingAnArgumentThatIsAlreadyWritten_ReplacesJustItsValue()
    {
        var edit = _session.SetProperty(_probe, Source, OriginOf("Row(gap:"), "gap", "Space.S6");

        edit.Applied.Should().BeTrue(edit.Reason);
        Apply(Source, edit).Should().Contain("Row(gap: Space.S6, children:")
            .And.NotContain("Space.S2, children:");
    }

    /// <summary>The least invasive edit there is: a parameter the call omitted becomes a NAMED
    /// argument, so nothing about the existing arguments moves.</summary>
    [Fact]
    public void SettingAnOmittedParameter_AddsItByName_BeforeTheChildren()
    {
        var edit = _session.SetProperty(_probe, Source, OriginOf("Row(gap:"), "cross", "CrossAlign.Center");

        edit.Applied.Should().BeTrue(edit.Reason);
        // Before `children:`, so the tree stays last — which is how every screen in the repo reads.
        Apply(Source, edit).Should().Contain("Row(gap: Space.S2, cross: CrossAlign.Center, children:");
    }

    /// <summary>
    /// The things an author actually wants to change — padding, background, corner radius — are not
    /// on the node. They are on a <c>BoxStyle</c>, which is DATA rather than tree and so carries no
    /// origin of its own: a click on a Box resolves to the Box. So the panel names them through the
    /// argument that carries them, and the edit goes one level down.
    /// </summary>
    [Fact]
    public void SettingAMemberOfAValueAnArgumentCarries_EditsInsideThatValue()
    {
        var edit = _session.SetProperty(_probe, Source, OriginOf("new Box("), "style.Padding", "EdgeInsets.All(Space.S6)");

        edit.Applied.Should().BeTrue(edit.Reason);
        Apply(Source, edit).Should().Contain("Padding = EdgeInsets.All(Space.S6)");
    }

    /// <summary>A member the style does not yet mention is appended to the initializer that is
    /// already there — never to one invented for the purpose.</summary>
    [Fact]
    public void SettingAStyleMemberTheInitializerOmits_AppendsToTheExistingBraces()
    {
        var edit = _session.SetProperty(_probe, Source, OriginOf("new Box("), "style.Background", "context.Theme.Surface");

        edit.Applied.Should().BeTrue(edit.Reason);
        Apply(Source, edit).Should()
            .Contain("Padding = EdgeInsets.All(Space.S2), Background = context.Theme.Surface");
    }

    /// <summary>
    /// The fence. `Width` is init-only on a factory call, so setting it needs an object initializer
    /// the call does not have — and adding one rewrites `Row(…)` into `new Row(…) { … }`, which is a
    /// decision about how the file is authored. The panel says so instead of doing it.
    /// </summary>
    [Fact]
    public void SettingAnInitOnlyMemberOnAFactoryCall_IsRefusedWithTheReason()
    {
        var edit = _session.SetProperty(_probe, Source, OriginOf("Row(gap:"), "Width", "SizeValue.Fill");

        edit.Applied.Should().BeFalse();
        edit.Reason.Should().Contain("factory call").And.Contain("authored");
    }

    /// <summary>
    /// A constructor parameter goes into the ARGUMENT LIST, wherever that is.
    /// <para>
    /// Anchoring it on the end of the construction assumed the construction ends with `)`. One that
    /// carries an object initializer ends with `}`, so the argument landed inside the braces — and
    /// this tool CREATES that shape itself the moment it sets one property, so the very next edit on
    /// the same node hit it.
    /// </para>
    /// </summary>
    [Fact]
    public void AParameterGoesInTheParens_EvenAfterTheNodeHasGainedBraces()
    {
        // The empty-parens shape, which is the one the tool creates for itself: setting a property
        // first gives it braces, and the parameter must still go in the parens that come before them.
        var braces = _session.SetProperty(_probe, Source, OriginOf("new Row()"), "AlignSelf", "CrossAlign.Center");
        var after = Apply(Source, braces);

        var edit = _session.SetProperty(_probe, after, OriginOf("new Row()"), "gap", "Space.S4");

        edit.Applied.Should().BeTrue(edit.Reason);
        Apply(after, edit).Should().Contain("new Row(gap: Space.S4) { AlignSelf = CrossAlign.Center }");
    }

    /// <summary>
    /// The other half of that fence, and the half that was drawn in the wrong place.
    /// <para>
    /// A construction ALREADY written with `new` has chosen that form: the braces are how it is
    /// spelled, so adding them rewrites nothing. Refusing there meant every property of every
    /// `new Box(…)` on a screen answered "not from here" — which is most of the properties on most of
    /// the screens in this repo.
    /// </para>
    /// </summary>
    [Fact]
    public void TheSameMemberOnANewConstruction_IsWrittenIntoBracesThatDidNotExist()
    {
        // `Width` lives on the STYLE, not on the Box — the node's own init-only members are things
        // like AlignSelf, and this is one of them.
        var edit = _session.SetProperty(_probe, Source, OriginOf("new Box(new BoxStyle"), "AlignSelf", "CrossAlign.Center");

        edit.Applied.Should().BeTrue(edit.Reason);
        Apply(Source, edit).Should().Contain("{ AlignSelf = CrossAlign.Center }");
    }

    /// <summary>
    /// And the SECOND one goes in beside the first.
    /// <para>
    /// Adding the braces is one branch and filling them is another, and with only the first there,
    /// setting two things on the same node — the ordinary case, not the edge one — refused everything
    /// after the first, with a message that called a `new` construction a factory call.
    /// </para>
    /// </summary>
    [Fact]
    public void ASecondMemberJoinsTheBracesRatherThanBeingRefused()
    {
        var first = _session.SetProperty(_probe, Source, OriginOf("new Box(new BoxStyle"), "AlignSelf", "CrossAlign.Center");
        var after = Apply(Source, first);

        var second = _session.SetProperty(_probe, after, OriginOf("new Box(new BoxStyle"), "GridSpan", "2");

        second.Applied.Should().BeTrue(second.Reason);
        var written = Apply(after, second);
        written.Should().Contain("AlignSelf = CrossAlign.Center").And.Contain("GridSpan = 2");
    }

    /// <summary>A value that is not C# would be written verbatim into a C# file.</summary>
    [Fact]
    public void AValueThatIsNotAnExpression_IsRefusedBeforeAnythingIsComputed()
    {
        var edit = _session.SetProperty(_probe, Source, OriginOf("Row(gap:"), "gap", "Space.S2 ; DROP");

        edit.Applied.Should().BeFalse();
        edit.Reason.Should().Contain("not a C# expression");
    }

    /// <summary>
    /// A value that parses but does not belong is caught by re-parsing the whole file: an edit that
    /// introduces an error the file did not already have is refused rather than returned. A panel
    /// that writes a broken file and lets the preview report it has still broken the file.
    /// </summary>
    [Fact]
    public void AValueThatWouldNotCompile_IsRefusedRatherThanWritten()
    {
        var edit = _session.SetProperty(_probe, Source, OriginOf("Row(gap:"), "gap", "NoSuchThing.Nope");

        edit.Applied.Should().BeFalse();
        edit.Reason.Should().Contain("would not compile");
    }

    /// <summary>
    /// THE safety net for the whole phase: setting every written argument to the value it already
    /// has must give back a byte-identical file. A span off by one character passes every test above
    /// and silently eats a bracket here.
    /// </summary>
    [Fact]
    public void SettingEveryArgumentToWhatItAlreadySays_LeavesTheFileByteIdentical()
    {
        var root = CSharpSyntaxTree.ParseText(Source, path: _probe).GetRoot();
        var checkedEdits = 0;

        foreach (var construction in root.DescendantNodes()
                     .Where(n => n is ObjectCreationExpressionSyntax or InvocationExpressionSyntax))
        {
            var arguments = construction switch
            {
                ObjectCreationExpressionSyntax creation => creation.ArgumentList?.Arguments,
                InvocationExpressionSyntax invocation => invocation.ArgumentList.Arguments,
                _ => null,
            };
            if (arguments is not { } list) continue;

            var span = construction.Span;
            var source = Microsoft.CodeAnalysis.Text.SourceText.From(Source);
            var start = source.Lines.GetLinePosition(span.Start);
            var end = source.Lines.GetLinePosition(span.End);
            var origin = $"{_probe}|{start.Line}:{start.Character}|{end.Line}:{end.Character}";

            foreach (var argument in list.Where(a => a.NameColon is not null))
            {
                var name = argument.NameColon!.Name.Identifier.Text;
                var edit = _session.SetProperty(_probe, Source, origin, name, argument.Expression.ToString());
                if (!edit.Applied) continue;

                Apply(Source, edit).Should().Be(Source,
                    $"setting {name} to what it already says must change nothing (in `{construction.Kind()}`)");
                checkedEdits++;
            }
        }

        checkedEdits.Should().BeGreaterThanOrEqualTo(2, "the probe should exercise several named arguments");
    }
}
