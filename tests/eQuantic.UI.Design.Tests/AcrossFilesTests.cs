using eQuantic.UI.Design;
using FluentAssertions;

namespace eQuantic.UI.Design.Tests;

/// <summary>
/// A screen is rarely one file, and almost never one method.
/// <para>
/// This is the shape of real code in this repo: a page composed of helper methods, a shell in another
/// file, data in a third. So the editor has to READ all of it — the fence is on what it will rewrite,
/// never on what it will understand. A tool that went blank the moment a node came from somewhere
/// else would be blank on most of every screen anyone actually has.
/// </para>
/// </summary>
public sealed class AcrossFilesTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "eq-across-" + Guid.NewGuid().ToString("N"));
    private readonly string _page;
    private readonly string _shell;
    private readonly DesignSession _session = new();

    private const string PageSource = """
        using eQuantic.UI.Primitives;
        using static eQuantic.UI.Components.UI;
        using StatefulComponent = eQuantic.UI.Primitives.StatefulComponent;

        [Page("/probe")]
        public sealed class Probe : StatefulComponent
        {
            public override VisualNode Build(ComponentContext context) =>
                Shell.Wrap(context, Row(gap: Space.S2, children: []));
        }
        """;

    private const string ShellSource = """
        using eQuantic.UI.Primitives;
        using static eQuantic.UI.Components.UI;

        public static class Shell
        {
            public static VisualNode Wrap(ComponentContext context, VisualNode body) =>
                Column(gap: Space.S4, children: [
                    Text("Console", TypeRole.Heading, context.Theme.TextPrimary),
                    body,
                ]);
        }
        """;

    public AcrossFilesTests()
    {
        Directory.CreateDirectory(Path.Combine(_directory, "Screens"));
        _page = Path.Combine(_directory, "Screens", "Probe.cs");
        _shell = Path.Combine(_directory, "Screens", "Shell.cs");
        File.WriteAllText(_page, PageSource);
        File.WriteAllText(_shell, ShellSource);
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

    private static string OriginIn(string path, string source, string fragment)
    {
        var lines = source.Replace("\r\n", "\n").Split('\n');
        var line = Array.FindIndex(lines, l => l.Contains(fragment, StringComparison.Ordinal));
        line.Should().BeGreaterThanOrEqualTo(0, $"the probe should contain '{fragment}'");
        var column = lines[line].IndexOf(fragment, StringComparison.Ordinal);
        return $"{path}|{line}:{column}|{line}:{column + fragment.Length}";
    }

    /// <summary>
    /// The node is in the SHELL, and the shell is a different file from the page being previewed.
    /// Asking about it works because the origin decides the file, not the panel.
    /// </summary>
    [Fact]
    public void ANodeFromAnotherFile_IsInspectedInThatFile()
    {
        var node = _session.Inspect(_shell, ShellSource, OriginIn(_shell, ShellSource, "Column(gap:"));

        node.Should().NotBeNull();
        node!.Component.Should().Be("Column");
        node.Properties.Single(p => p.Name == "gap").Value.Should().Be("Space.S4");
    }

    [Fact]
    public void ANodeFromAnotherFile_CanBeEditedInThatFile()
    {
        var edit = _session.SetProperty(
            _shell, ShellSource, OriginIn(_shell, ShellSource, "Column(gap:"), "gap", "Space.S6");

        edit.Applied.Should().BeTrue(edit.Reason);
        edit.NewText.Should().Be("Space.S6");
    }

    /// <summary>
    /// A helper method inside the SAME file is the commonest shape of all — a page is twenty-odd of
    /// them — and the node's origin lands inside the helper, which is where the author has to go.
    /// </summary>
    [Fact]
    public void ANodeBuiltByAHelperInAnotherFile_IsClassifiedByItsOwnMember()
    {
        var tier = _session.Classify(_shell, ShellSource, OriginIn(_shell, ShellSource, "Text(\"Console\""));

        tier.Tier.Should().Be("foreign");
        tier.Member.Should().Be("Wrap");
    }

    /// <summary>
    /// The unsaved half. A neighbour open and edited but not saved must be what every answer is built
    /// on — otherwise the preview shows the last saved version of every part the author is not
    /// currently typing in, which is the confusing kind of stale.
    /// </summary>
    [Fact]
    public void AnUnsavedNeighbour_IsWhatTheAnswersAreBuiltOn()
    {
        var edited = ShellSource.Replace("Space.S4", "Space.S8");
        _session.SyncBuffers([new OpenBuffer(_shell, edited)]);

        var node = _session.Inspect(_shell, edited, OriginIn(_shell, edited, "Column(gap:"));

        node.Should().NotBeNull();
        node!.Properties.Single(p => p.Name == "gap").Value
            .Should().Be("Space.S8", "the buffer says S8 even though the file on disk still says S4");
        File.ReadAllText(_shell).Should().Contain("Space.S4", "and nothing wrote to disk");
    }

    /// <summary>A file closed without saving goes back to what is on disk — the overlay is the
    /// editor's state, not a second store that drifts from it.</summary>
    [Fact]
    public void ClosingWithoutSaving_ForgetsTheBuffer()
    {
        _session.SyncBuffers([new OpenBuffer(_shell, ShellSource.Replace("Space.S4", "Space.S8"))]);
        _session.SyncBuffers([]);

        var node = _session.Inspect(_shell, ShellSource, OriginIn(_shell, ShellSource, "Column(gap:"));

        node!.Properties.Single(p => p.Name == "gap").Value.Should().Be("Space.S4");
    }
}
