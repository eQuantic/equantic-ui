using eQuantic.UI.Components;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// ONE grid. An editor places its caret at <c>contentTop + line × lineHeight</c> over lines the
/// block draws at its own line height — and the two used to be measured separately, from whatever
/// context each half happened to be built with. The moment those disagreed (a page's context said
/// comfortable while the lowering handed its subtree the ambient compact) the caret sat between
/// lines and drifted further with every line down the file.
/// </summary>
public class CodeEditorGridTests
{
    private static CodeBlock BlockIn(CodeEditor editor, ComponentContext context) =>
        Find<CodeBlock>(editor.Build(context))!;

    private static T? Find<T>(VisualNode node) where T : VisualNode
    {
        if (node is T match) return match;
        foreach (var child in Children(node))
        {
            if (Find<T>(child) is { } found) return found;
        }
        return null;
    }

    private static IEnumerable<VisualNode> Children(VisualNode node) => node switch
    {
        FlexNode flex => flex.Children,
        Box box when box.Child is { } child => [child],
        Stack stack => stack.Children,
        Flexible flexible => [flexible.Child],
        ScrollView scroll => [scroll.Child],
        Shortcut shortcut => [shortcut.Child],
        CodeSurface surface => [surface.Child],
        UiComponent component => [],
        _ => [],
    };

    [Theory]
    [InlineData(Density.Comfortable)]
    [InlineData(Density.Compact)]
    public void The_block_draws_on_the_grid_the_surface_measures(Density density)
    {
        var context = new ComponentContext(PhotonTheme.Instance, density: density);
        var editor = new CodeEditor("let x = 1;\nlet y = 2;", "csharp");

        var tree = editor.Build(context);
        var surface = Find<CodeSurface>(tree)!;
        var block = Find<CodeBlock>(tree)!;

        block.Metrics.Should().NotBeNull("the editor measured once and hands the grid down");
        block.Metrics!.Value.LineHeight.Should().Be(surface.LineHeight);
        block.Metrics!.Value.ColumnWidth.Should().Be(surface.ColumnWidth);
        block.Metrics!.Value.ContentLeft.Should().Be(surface.ContentLeft);
        block.Metrics!.Value.ContentTop.Should().Be(surface.ContentTop);
    }

    [Fact]
    public void Density_changes_the_grid_but_never_splits_it()
    {
        var comfortable = new CodeEditor("x", "csharp")
            .Build(new ComponentContext(PhotonTheme.Instance, density: Density.Comfortable));
        var compact = new CodeEditor("x", "csharp")
            .Build(new ComponentContext(PhotonTheme.Instance, density: Density.Compact));

        // 13dp label → a 16dp line box (MathF.Round is to-even: 32.5 → 32) → 18; 11.5dp → 14.5 → 17.
        // Two grids, each internally whole — which is the point, not the numbers.
        Find<CodeSurface>(comfortable)!.LineHeight.Should().Be(18);
        Find<CodeSurface>(compact)!.LineHeight.Should().Be(17);
        Find<CodeBlock>(compact)!.Metrics!.Value.LineHeight.Should().Be(17);
    }

    [Fact]
    public void The_marks_write_with_the_block_s_ink_not_the_page_s()
    {
        var context = new ComponentContext(PhotonTheme.Instance);
        var inverse = Find<CodeSurface>(new CodeEditor("x", "csharp") { Inverse = true }.Build(context))!;
        var plain = Find<CodeSurface>(new CodeEditor("x", "csharp").Build(context))!;

        // On the dark slab the page's TextPrimary IS the slab — a caret painted with it cannot be
        // seen on exactly the surface people type into.
        inverse.CaretColor.Should().NotBeNull().And.NotBe(context.Theme.TextPrimary);
        plain.CaretColor.Should().Be(context.Theme.TextPrimary);
    }
}
