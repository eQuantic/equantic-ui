using eQuantic.UI.Design;
using FluentAssertions;

namespace eQuantic.UI.Design.Tests;

/// <summary>
/// The members of a value an argument carries — a <c>BoxStyle</c>'s padding, background, radius.
/// <para>
/// They arrive at the panel as one long line of C# otherwise, which is the thing an author most wants
/// to change and the one thing they cannot change there. Reported as ROWS, each is an ordinary
/// property with the same editor and the same refusals as any other.
/// </para>
/// </summary>
public sealed class ValueMemberTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "eq-members-" + Guid.NewGuid().ToString("N"));
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
                var scaled = new Row(gap: Space.S3);
                var plain = new Row(gap: 7);
                var qualified = new Row(gap: eQuantic.UI.Primitives.Space.S3);
                return new Box(new BoxStyle
                {
                    Padding = EdgeInsets.All(Space.S4),
                    Background = context.Theme.Surface,
                });
            }
        }
        """;

    public ValueMemberTests()
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

    private static string Apply(string text, EditResult edit)
    {
        var source = Microsoft.CodeAnalysis.Text.SourceText.From(text);
        var start = source.Lines[edit.StartLine].Start + edit.StartColumn;
        var end = source.Lines[edit.EndLine].Start + edit.EndColumn;
        return source.Replace(Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(start, end), edit.NewText).ToString();
    }

    private string TheBox => OriginOf("new Box(new BoxStyle");

    private NodeProperty Style =>
        _session.Inspect(_probe, Source, TheBox)!.Properties.Single(property => property.Name == "style");

    [Fact]
    public void AValueWrittenAsAConstruction_ArrivesAsRowsRatherThanAsOneLineOfCSharp()
    {
        Style.Members.Should().NotBeNull();
        Style.Members.Should().Contain(member => member.Name == "Padding" && member.Kind == "initializer");
        Style.Members.Should().Contain(member => member.Name == "Background" && member.Kind == "initializer");
        // Everything the type can hold, not only what is written: the unset ones are the offer.
        Style.Members.Should().Contain(member => member.Kind == "unset");
    }

    [Fact]
    public void EachMemberCarriesWhatIsWrittenThereAndWhatItMayBe()
    {
        var padding = Style.Members!.Single(member => member.Name == "Padding");

        padding.Value.Should().Be("EdgeInsets.All(Space.S4)");
        padding.Editable.Should().BeTrue();
        padding.Type.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>An enum member is a closed set, so the panel offers a list rather than a text box —
    /// the same rule as any other property, applied one level down.</summary>
    [Fact]
    public void AMemberOfAClosedType_CarriesItsOptions()
    {
        Style.Members.Should().Contain(member => member.Options != null && member.Options.Length > 0);
    }

    [Fact]
    public void SettingAMember_WritesItIntoTheInitializer()
    {
        var edit = _session.SetProperty(_probe, Source, TheBox, "style.BorderWidth", "2");

        edit.Applied.Should().BeTrue(edit.Reason);
        Apply(Source, edit).Should().Contain("BorderWidth = 2");
    }

    [Fact]
    public void ReplacingAMember_KeepsTheRest()
    {
        var edit = _session.SetProperty(_probe, Source, TheBox, "style.Padding", "EdgeInsets.All(Space.S2)");

        edit.Applied.Should().BeTrue(edit.Reason);
        var after = Apply(Source, edit);
        after.Should().Contain("Padding = EdgeInsets.All(Space.S2)");
        after.Should().Contain("Background = context.Theme.Surface");
    }

    /// <summary>A spreadsheet you can only add rows to is a spreadsheet with a leak.</summary>
    [Fact]
    public void AMemberCanBeTakenBackOut()
    {
        var edit = _session.UnsetProperty(_probe, Source, TheBox, "style.Padding");

        edit.Applied.Should().BeTrue(edit.Reason);
        var after = Apply(Source, edit);
        after.Should().NotContain("Padding =");
        after.Should().Contain("Background = context.Theme.Surface");
        after.Should().NotContain(",,");
    }

    [Fact]
    public void UnsettingTheLastOne_LeavesNoOrphanedComma()
    {
        var once = _session.UnsetProperty(_probe, Source, TheBox, "style.Padding");
        var after = Apply(Source, once);

        var twice = _session.UnsetProperty(_probe, after, OriginOf("new Box(new BoxStyle"), "style.Background");

        twice.Applied.Should().BeTrue(twice.Reason);
        Apply(after, twice).Should().NotContain(",,").And.NotContain("{,");
    }

    /// <summary>
    /// Half the framework's tokens are not enums. `Space` is a static class of `const float`, so a
    /// `float gap` reads as a number and a panel that looked only at the type would offer a number box
    /// for a value that must come off a 4dp scale. What is WRITTEN there says otherwise.
    /// </summary>
    [Fact]
    public void AValueOffAScaleOffersTheWholeScale_EvenThoughItsTypeIsFloat()
    {
        var node = _session.Inspect(_probe, Source, OriginOf("new Row(gap: Space.S3"));
        var gap = node!.Properties.Single(property => property.Name == "gap");

        gap.Type.Should().Be("float");
        gap.Options.Should().NotBeNull();
        gap.Options.Should().Contain("Space.S4").And.Contain("Space.S1");
    }

    /// <summary>A number that is just a number stays a number: the scale is inferred from what is
    /// written, so there is nothing to infer when nothing names a scale.</summary>
    [Fact]
    public void APlainNumber_IsStillJustANumber()
    {
        var node = _session.Inspect(_probe, Source, OriginOf("new Row(gap: 7"));

        node!.Properties.Single(property => property.Name == "gap").Options.Should().BeNull();
    }

    /// <summary>
    /// A construction written with `new` and no braces can be GIVEN them: they are how that form is
    /// spelled, so adding them rewrites nothing the author chose. The fence belongs around a factory
    /// call, where `new Button("Save") { … }` really would change the form.
    /// </summary>
    [Fact]
    public void ANewConstructionWithoutBraces_CanStillBeSet()
    {
        var edit = _session.SetProperty(_probe, Source, TheBox, "AlignSelf", "CrossAlign.Center");

        edit.Applied.Should().BeTrue(edit.Reason);
        Apply(Source, edit).Should().Contain("{ AlignSelf = CrossAlign.Center }");
    }

    [Fact]
    public void ThoseRowsAreOffered_RatherThanExplainedAway()
    {
        var align = _session.Inspect(_probe, Source, TheBox)!
            .Properties.Single(property => property.Name == "AlignSelf");

        align.Editable.Should().BeTrue();
        align.Options.Should().NotBeNull("CrossAlign is an enum, so it is a list and not a text box");
    }

    /// <summary>
    /// The options are qualified the way the AUTHOR qualified them. The panel writes what it offers
    /// straight into the file, so the scale's simple name is only certainly in scope where that is how
    /// it is already spelled there.
    /// </summary>
    [Fact]
    public void TheOptionsKeepTheSpellingTheFileUses()
    {
        var node = _session.Inspect(_probe, Source, OriginOf("new Row(gap: eQuantic.UI.Primitives.Space.S3"));
        var gap = node!.Properties.Single(property => property.Name == "gap");

        gap.Options.Should().NotBeNull();
        gap.Options.Should().OnlyContain(option => option.StartsWith("eQuantic.UI.Primitives.Space."));
    }

    /// <summary>A property with a private setter is not an offer: every edit of that row would be
    /// refused by the compiler rather than by the tool.</summary>
    [Fact]
    public void AMemberWithNoPublicSetter_IsNotOffered()
    {
        Style.Members.Should().OnlyContain(member => member.Name != "GetHashCode");
        Style.Members.Should().NotContain(member => member.Name == "EqualityContract");
    }

    /// <summary>
    /// An edit INSIDE a node changes its length, so the span the panel is holding no longer matches
    /// it: unsetting a member shortens the node, the old span overshoots, and a span that overshoots
    /// resolves to the smallest node CONTAINING it — the parent. Every in-place edit therefore says
    /// where the node now ends.
    /// </summary>
    [Fact]
    public void AnEditInsideANode_SaysWhereThatNodeNowEnds()
    {
        var edit = _session.UnsetProperty(_probe, Source, TheBox, "style.Padding");

        edit.Applied.Should().BeTrue(edit.Reason);
        edit.Origin.Should().NotBeNull();

        var after = Apply(Source, edit);
        // The same node it was asked about — a Box with a style — and not the method around it.
        _session.Inspect(_probe, after, edit.Origin!)!.Component.Should().Be("Box");

        // A DIFFERENT span from the one that was handed in: the node ends earlier than it did, which
        // is the whole reason the caller cannot go on using what it was holding. (The origins in this
        // probe are short fragments, so they do not overshoot; the compiler stamps the FULL
        // construction, and there the old end lands past the node and resolves to its parent.)
        edit.Origin.Should().NotBe(TheBox);
    }

    [Fact]
    public void UnsettingSomethingThatIsNotSet_IsRefusedRatherThanWritingNothing()
    {
        var edit = _session.UnsetProperty(_probe, Source, TheBox, "style.BorderWidth");

        edit.Applied.Should().BeFalse();
        edit.Reason.Should().Contain("not set");
    }
}
