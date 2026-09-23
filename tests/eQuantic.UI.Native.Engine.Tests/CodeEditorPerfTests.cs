using System.Diagnostics;
using eQuantic.UI.Code;
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
            .Select(i => $"    var x{i} = compute(a, b); // é 😀 中"));
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

    /// <summary>
    /// An arrow asks the line where the next text element starts. Segmenting the line anew for
    /// every press made a long line cost its whole length per key; the controller's cells are
    /// segmented once per change of the line.
    /// </summary>
    [Fact]
    public void ArrowsAlongALongLine_StayInsideTheAlarm()
    {
        var line = string.Concat(Enumerable.Repeat("abc é 😀 中 ", 250));
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
