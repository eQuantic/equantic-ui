using eQuantic.UI.Code;
using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// An app's key bindings on <see cref="CodeEditor"/> (#1010), driven as a person drives it: a click into
/// the code, then the keys, through <see cref="PhotonHost"/>.
/// </summary>
[Collection(ProcessDispatcherCollection.Name)]
public class CodeEditorKeyBindingsTests : IDisposable
{
    private readonly IUiDispatcher? _outer = UiDispatcher.Current;

    public CodeEditorKeyBindingsTests() => UiDispatcher.Current = null;

    public void Dispose() => UiDispatcher.Current = _outer;

    private sealed class FixedWidthRasterizer : ITextRasterizer
    {
        public TextRaster? Rasterize(string content, TypeStyle style, float typeScale,
            float maxWidth, int maxLines, float scale, TextAlignment align)
        {
            if (string.IsNullOrEmpty(content)) return null;
            var width = Math.Max(1, (int)Math.Min(content.Length * 8 * scale, maxWidth * scale));
            var height = Math.Max(1, (int)(style.LineHeight * scale));
            return new TextRaster(width, height, new byte[width * height]);
        }
    }

    private sealed class Parent(CodeEditor first) : StatefulComponent
    {
        private CodeEditor _current = first;

        public void Hand(CodeEditor next) => SetState(() => _current = next);

        public override VisualNode Build(ComponentContext context) => _current;
    }

    private const string Code = "alpha beta\ngamma delta\nepsilon";

    private static readonly CodeKeyBinding SelectAllOnD = new(KeyChord.Command("d"), CodeCommand.SelectAll);

    private static PhotonHost Host(VisualNode root) =>
        new(root, PhotonTheme.Instance, ThemeMode.Light, 600, 400)
        {
            TextRasterizer = new FixedWidthRasterizer(),
            Density = Density.Compact,
            SmoothScroll = false,
        };

    private static RealizeResult Settle(PhotonHost host)
    {
        host.RenderFrame(new DisplayListBuilder());
        return host.RenderFrame(new DisplayListBuilder());
    }

    /// <summary>Puts the caret in the code with a press, as a person does.</summary>
    private static void ClickInto(PhotonHost host, CodeEditor editor)
    {
        var frame = Settle(host);
        var region = frame.CodeRegions.Single();
        var at = editor.Editor.CaretRect(new CodePosition(1, 2));
        host.PressDown(region.Bounds.X + at.X + 1, region.Bounds.Y + at.Y + at.Height / 2);
        host.PressUp(region.Bounds.X + at.X + 1, region.Bounds.Y + at.Y + at.Height / 2);
        Settle(host);
    }

    private static CodeRange Everything(CodeEditor editor) => new(CodePosition.Start, editor.Editor.Document.End);

    [Fact]
    public void AKeyOfTheAppsOwn_RunsItsCommand()
    {
        var editor = new CodeEditor(Code, "csharp") { Height = SizeValue.Fill, KeyBindings = [SelectAllOnD] };
        var host = Host(editor);
        ClickInto(host, editor);

        host.KeyDown("d", KeyModifiers.Command).Should().BeTrue();

        editor.Editor.Selection.Should().Be(Everything(editor));
    }

    /// <summary>The bindings of a parent's next build reach the editor its position keeps, and the ones it
    /// handed before go: a property missing from <c>AdoptConfig</c> never reached it.</summary>
    [Fact]
    public void BindingsHandedByAParent_ReplaceTheOnesBefore()
    {
        var first = new CodeEditor(Code, "csharp") { Height = SizeValue.Fill, KeyBindings = [SelectAllOnD] };
        var parent = new Parent(first);
        var host = Host(parent);
        ClickInto(host, first);

        parent.Hand(new CodeEditor(Code, "csharp")
        {
            Height = SizeValue.Fill,
            KeyBindings = [new CodeKeyBinding(KeyChord.Command("e"), CodeCommand.SelectAll)],
        });
        Settle(host);

        first.Editor.KeyBindings.Should().ContainSingle().Which.Chord.Key.Should().Be("e");
        host.KeyDown("e", KeyModifiers.Command).Should().BeTrue();
        first.Editor.Selection.Should().Be(Everything(first));
    }
}
