using System.Diagnostics;
using eQuantic.UI.Code;
using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// What the editor does on EVERY frame or every key, measured against a file of the size an IDE
/// opens. The ceilings follow the harness's rule (see <see cref="PerfHarnessTests"/>): an order of
/// magnitude loose, because CI machines are shared, and still far below what the regression cost.
/// </summary>
[Collection(PerfHarnessCollection.Name)]
public class CodeEditorPerfTests
{
    private readonly ITestOutputHelper _output;

    public CodeEditorPerfTests(ITestOutputHelper output) => _output = output;

    private const double BracketWalkCeilingMs = 25;
    private const double ArrowsCeilingMs = 15;

    /// <summary>
    /// The bracket under the caret is looked for on every frame the caret is on one, and its pair
    /// can be the last line of the file. Stepped the way the caret steps, the walk built every
    /// line's text elements on the way: 110 ms per frame on 3000 lines, where the scan takes
    /// under one.
    /// </summary>
    [Fact]
    public void TheBracketWalkThroughALongFile_StaysInsideTheAlarm()
    {
        var body = string.Join("\n", Enumerable.Range(0, 3000)
            .Select(i => $"    var x{i} = compute(a, b); // e\u0301 \U0001F600 \u4E2D"));
        var editor = new CodeEditorController("{\n" + body + "\n}", CodeLanguages.CSharp)
        {
            Selection = new CodeRange(CodePosition.Start),
        };
        editor.BracketAtCaret()!.Value.There.Line.Should().Be(3001);

        const int calls = 20;
        var clock = Stopwatch.StartNew();
        for (var i = 0; i < calls; i++) editor.BracketAtCaret();
        var each = clock.Elapsed.TotalMilliseconds / calls;

        _output.WriteLine($"bracket walk over 3000 lines: {each:F2} ms (alarm {BracketWalkCeilingMs} ms)");
        each.Should().BeLessThan(BracketWalkCeilingMs, "the walk runs on every frame");
    }

    private const double ScrollStepCeilingMs = 40;

    /// <summary>
    /// A scroll step rebuilds the editor, and a build measured the whole file three times over: every
    /// selected line's band, every match of the search (found again from scratch, and each made a
    /// mark for the block to throw away), and every line's width, to find the widest. A select-all
    /// with a search on over 50,000 lines is what an IDE does on a large file, and it has to scroll.
    /// </summary>
    [Fact]
    public void AScrollStepThroughALongSelectedSearchedFile_StaysInsideTheAlarm()
    {
        var text = string.Join("\n", Enumerable.Range(0, 50_000).Select(i => $"    var needle{i} = compute(a, {i});"));
        var editor = new CodeEditor(text, "csharp") { Height = SizeValue.Fill, Search = "needle" };
        editor.Editor.SelectAll();
        var host = new PhotonHost(editor, PhotonTheme.Instance, ThemeMode.Light, 800, 600);
        host.RenderFrame(new DisplayListBuilder());
        var frame = host.RenderFrame(new DisplayListBuilder());
        var viewport = frame.ScrollRegions.First(r => r.Axis == ScrollAxis.Vertical);
        // Over the gutter, which the vertical viewport holds and the sideways one does not.
        var (x, y) = (viewport.Bounds.X + 4, viewport.Bounds.Y + 20);
        // Warm: the first steps measure the lines they bring into view.
        for (var i = 0; i < 5; i++)
        {
            host.ScrollBy(x, y, 60).Should().BeTrue();
            host.RenderFrame(new DisplayListBuilder());
        }

        const int steps = 20;
        var clock = Stopwatch.StartNew();
        for (var i = 0; i < steps; i++)
        {
            host.ScrollBy(x, y, 60).Should().BeTrue("the step has to scroll, or nothing is rebuilt");
            host.RenderFrame(new DisplayListBuilder());
            host.RenderFrame(new DisplayListBuilder());
        }
        var each = clock.Elapsed.TotalMilliseconds / steps;

        _output.WriteLine($"a scroll step over 50,000 selected, searched lines: {each:F2} ms (alarm {ScrollStepCeilingMs} ms)");
        each.Should().BeLessThan(ScrollStepCeilingMs, "a scroll step builds the lines in view, not the file");
    }

    /// <summary>
    /// An arrow asks the line where the next text element starts. Segmenting the line anew for
    /// every press made a long line cost its whole length per key; the controller's cells are
    /// segmented once per change of the line.
    /// </summary>
    [Fact]
    public void ArrowsAlongALongLine_StayInsideTheAlarm()
    {
        var line = string.Concat(Enumerable.Repeat("abc e\u0301 \U0001F600 \u4E2D ", 250));
        var editor = new CodeEditorController(line, CodeLanguages.CSharp);
        editor.Selection = new CodeRange(editor.Document.End);
        editor.Move(CodeMotion.Character, CodeDirection.Backward);

        const int presses = 1000;
        var clock = Stopwatch.StartNew();
        for (var i = 0; i < presses; i++) editor.Move(CodeMotion.Character, CodeDirection.Backward);
        var total = clock.Elapsed.TotalMilliseconds;

        _output.WriteLine($"{presses} arrows along a {line.Length}-unit line: {total:F1} ms (alarm {ArrowsCeilingMs} ms)");
        total.Should().BeLessThan(ArrowsCeilingMs, "an arrow must not cost the line's length");
    }
}
