using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The reconciler retains a stateful component across a parent rebuild and hands it the fresh node
/// through <see cref="UiComponent.AdoptConfig"/>. The base implementation does nothing — right for a
/// component whose props never change, and silently wrong for one whose props do.
/// <para>
/// The CodeEditor was the second kind and had no override. A page that recomputed its decorations
/// (diagnostics as you type) built a new node with them; the reconciler kept the retained instance,
/// dropped that node, and the editor drew the first frame's configuration forever. Nothing failed,
/// nothing logged — the underlines simply never appeared.
/// </para>
/// </summary>
public class CodeEditorAdoptConfigTests
{
    private static CodeEditor Editor(IReadOnlyList<CodeDecoration>? marks = null, string? search = null) =>
        new("var x = 1;", "csharp") { Decorations = marks ?? [], Search = search };

    private static CodeDecoration Squiggle(int line) =>
        new(new CodeRange(new CodePosition(line, 0), new CodePosition(line, 4)), CodeDecorationKind.Squiggle);

    [Fact]
    public void FreshDecorations_ReachTheRetainedInstance()
    {
        var retained = Editor();
        retained.Decorations.Should().BeEmpty();

        retained.AdoptConfig(Editor([Squiggle(3)]));

        retained.Decorations.Should().ContainSingle()
            .Which.Kind.Should().Be(CodeDecorationKind.Squiggle);
    }

    /// <summary>Decorations that went away have to go away — an underline for a mistake the author
    /// already fixed is worse than none.</summary>
    [Fact]
    public void DecorationsThatCleared_AreCleared()
    {
        var retained = Editor([Squiggle(3), Squiggle(7)]);

        retained.AdoptConfig(Editor());

        retained.Decorations.Should().BeEmpty();
    }

    [Fact]
    public void OtherConfigTravelsToo()
    {
        var retained = Editor();

        retained.AdoptConfig(Editor(search: "needle"));

        retained.Search.Should().Be("needle");
    }

    /// <summary>
    /// The one that must NOT travel. The editor takes its text when it opens and the document is
    /// its own from then on — that is what `Initial` means. Adopting it would overwrite whatever
    /// someone is typing on every parent rebuild, which for a page that rebuilds on each keystroke
    /// means it would overwrite them constantly.
    /// </summary>
    [Fact]
    public void TheInitialCode_IsNeverAdopted()
    {
        var retained = new CodeEditor("what the author has been typing", "csharp");

        retained.AdoptConfig(new CodeEditor("the page's stale first value", "csharp"));

        retained.InitialCode.Should().Be("what the author has been typing");
    }

    /// <summary>A different component at the same position is not this one — adopting from it would
    /// scramble the configuration rather than leave it alone.</summary>
    [Fact]
    public void AForeignComponent_ChangesNothing()
    {
        var retained = Editor([Squiggle(1)], search: "keep");

        retained.AdoptConfig(new TextInput("unrelated"));

        retained.Decorations.Should().ContainSingle();
        retained.Search.Should().Be("keep");
    }
}
