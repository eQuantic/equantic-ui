using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A Photon field keeps what was typed until the page builds it again, and then shows what the page
/// holds, whether or not that value changed since the last build (#996). It is what the web shows after
/// a render (#1026), so each scenario here is the browser's answer for the same page.
/// <para>
/// What the field shows is read where a person meets it: the text the measure measured, the text the
/// paint drew, what assistive technology hears, the caret and the editing value a phone's keyboard
/// reads. A reader that took the node's value instead of the field's would measure one text and draw
/// another, so each is checked rather than one of them standing in for the rest.
/// </para>
/// </summary>
public class AFieldKeepsWhatWasTypedTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    /// <summary>Every text the measure measured and the paint drew, in order.</summary>
    private sealed class Recorder : ITextMeasurer, ITextRasterizer
    {
        public List<string> Measured { get; } = [];
        public List<string> Drawn { get; } = [];

        public TextMeasurement Measure(string content, TypeStyle style, float typeScale, float maxWidth, int maxLines)
        {
            Measured.Add(content);
            return ApproximateTextMeasurer.Instance.Measure(content, style, typeScale, maxWidth, maxLines);
        }

        public TextRaster? Rasterize(string content, TypeStyle style, float typeScale,
            float maxWidth, int maxLines, float scale, TextAlignment align)
        {
            Drawn.Add(content);
            if (string.IsNullOrEmpty(content)) return null;
            var width = Math.Max(1, (int)Math.Min(content.Length * 8 * scale, maxWidth * scale));
            var height = Math.Max(1, (int)(style.LineHeight * scale));
            return new TextRaster(width, height, new byte[width * height]);
        }

        public void Clear()
        {
            Measured.Clear();
            Drawn.Clear();
        }
    }

    private enum Handler
    {
        /// <summary>Neither keeps the key nor calls <c>SetState</c>.</summary>
        Ignores,

        /// <summary>Keeps the value without <c>SetState</c>, as a form model can.</summary>
        KeepsQuietly,

        /// <summary>Calls <c>SetState</c> and keeps the value it had.</summary>
        Rejects,

        /// <summary>Calls <c>SetState</c> with the text upper-cased.</summary>
        UpperCases,

        /// <summary>Calls <c>SetState</c> with what was typed.</summary>
        Holds,
    }

    /// <summary>A page holding one field, which says when it builds. Through a <see cref="TextInput"/>
    /// when <paramref name="wrapped"/>: a component that holds the field and not its value, and rebuilds
    /// on its own when the focus moves.</summary>
    private sealed class Page(Handler handler, string initial, List<string> builds, bool wrapped = false)
        : StatefulComponent
    {
        private string _value = initial;
        private int _generation;

        /// <summary>Builds the page again for a reason that has nothing to do with the field.</summary>
        public void Rebuild() => SetState(() => { });

        /// <summary>Resets the field's value, as a page answering a refused code does.</summary>
        public void Reset() => SetState(() => _value = "");

        /// <summary>Builds the same field again, at the same position, holding <paramref name="value"/>.</summary>
        public void Hold(string value) => SetState(() => _value = value);

        /// <summary>Draws another field at this one's place: another key, its own value.</summary>
        public void Replace(string value) => SetState(() =>
        {
            _generation++;
            _value = value;
        });

        public override VisualNode Build(ComponentContext context)
        {
            builds.Add("page");
            Action<string> onChanged = handler switch
            {
                Handler.Ignores => _ => { },
                Handler.KeepsQuietly => value => _value = value,
                Handler.Rejects => _ => SetState(() => { }),
                Handler.UpperCases => value => SetState(() => _value = value.ToUpperInvariant()),
                _ => value => SetState(() => _value = value),
            };
            return wrapped
                ? new TextInput(_value, onChanged, "Field") { Key = $"field {_generation}" }
                : new TextEntry(_value, onChanged) { Label = "Field", Key = $"field {_generation}" };
        }
    }

    private sealed record Opened(PhotonHost Host, Page Page, Recorder Recorder, List<string> Builds);

    /// <summary>A host showing the page, with the field holding the caret.</summary>
    private static Opened Open(Handler handler, string initial = "", bool wrapped = false)
    {
        var builds = new List<string>();
        var recorder = new Recorder();
        var page = new Page(handler, initial, builds, wrapped);
        var host = new PhotonHost(page, Theme, ThemeMode.Light, 480, 200, recorder) { TextRasterizer = recorder };
        Frame(host);
        var field = host.LastFrame!.TextRegions.Single().Bounds;
        host.PressDown(field.Right - 1, field.Center.Y);
        host.PressUp(field.Right - 1, field.Center.Y);
        Frame(host);
        builds.Clear();
        recorder.Clear();
        return new Opened(host, page, recorder, builds);
    }

    private static void Frame(PhotonHost host) => host.RenderFrame(new DisplayListBuilder());

    /// <summary>What assistive technology hears the field hold.</summary>
    private static string? Heard(PhotonHost host) =>
        host.Semantics().Single(s => s.Role == SemanticRole.TextField).Value;

    [Fact]
    public void AHandlerThatIgnoresTheKey_LeavesItShowing_AndNothingBuilds()
    {
        var (host, _, recorder, builds) = Open(Handler.Ignores);

        host.TextInput("a");
        Frame(host);

        recorder.Drawn.Should().Contain("a", "the field draws the key the moment it is typed");
        Heard(host).Should().Be("a");
        builds.Should().BeEmpty("nothing marked a position, and the field keeps the key itself");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AHandlerThatKeepsTheValueWithoutSayingSo_ShowsEveryKey_BeforeAndAfterThePageBuilds(bool wrapped)
    {
        var (host, page, _, builds) = Open(Handler.KeepsQuietly, wrapped: wrapped);

        host.TextInput("a");
        Frame(host);
        host.TextInput("b");
        Frame(host);

        Heard(host).Should().Be("ab", "each key starts from the one before");
        builds.Should().BeEmpty();

        page.Rebuild();
        Frame(host);

        builds.Should().Equal(["page"]);
        Heard(host).Should().Be("ab", "the page built the field with the value it kept, which is what was typed");
    }

    /// <summary>
    /// A field component rebuilds on its own when the focus moves, from the value its parent last handed
    /// it. A form that kept the value without <c>SetState</c> never handed it the typed one, so the
    /// component has to keep what its field reported, or the field goes back to the value it held before
    /// the typing the moment the focus leaves.
    /// </summary>
    [Fact]
    public void AFormThatKeepsTheValueWithoutSayingSo_KeepsWhatWasTyped_WhenTheFocusLeavesItsFieldComponent()
    {
        var (host, _, _, _) = Open(Handler.KeepsQuietly, wrapped: true);

        host.TextInput("a");
        Frame(host);
        host.TextInput("b");
        Frame(host);
        host.KeyDown("Escape");
        Frame(host);

        Heard(host).Should().Be("ab");
    }

    [Fact]
    public void AHandlerThatRejectsTheKey_PutsTheFieldBack()
    {
        var (host, _, recorder, _) = Open(Handler.Rejects, initial: "ab");

        host.TextInput("!");
        Frame(host);

        Heard(host).Should().Be("ab", "the page built the field again, holding what it held");
        recorder.Drawn.Should().NotContain(drawn => drawn.Contains('!'),
            "the frame after the rejection draws the page's value");
    }

    [Fact]
    public void AFormatter_ShowsItsOutput()
    {
        var (host, _, _, _) = Open(Handler.UpperCases);

        host.TextInput("a");
        Frame(host);
        host.TextInput("b");
        Frame(host);
        host.TextInput("c");
        Frame(host);

        Heard(host).Should().Be("ABC");
    }

    /// <summary>
    /// #1026 on Photon: the page held <c>""</c>, a code was typed, and before the frame that would have
    /// drawn it the page reset the field to <c>""</c> again. A rule that takes the page's value only when
    /// it changed sees <c>""</c> twice and leaves the code on screen over an empty field.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AResetInTheSameFrameAsTheTyping_ShowsNothing(bool wrapped)
    {
        var (host, page, recorder, _) = Open(Handler.Holds, wrapped: wrapped);

        host.TextInput("1");
        host.TextInput("2");
        host.TextInput("3");
        page.Reset();
        Frame(host);

        Heard(host).Should().BeEmpty();
        recorder.Drawn.Should().NotContain(drawn => drawn.Contains("123"));
        host.EditingValue!.Value.Text.Should().BeEmpty("a phone's keyboard is told the field emptied");
    }

    [Fact]
    public void EverythingThatReadsTheField_ReadsWhatWasTyped()
    {
        var (host, _, recorder, _) = Open(Handler.Ignores);

        host.TextInput("a");
        host.TextInput("b");
        Frame(host);

        recorder.Measured.Should().Contain("ab", "the measure breaks the lines the paint draws");
        recorder.Drawn.Should().Contain("ab");
        Heard(host).Should().Be("ab");
        host.CaretIndex.Should().Be(2, "the caret sits after the last key");
        host.EditingValue!.Value.Text.Should().Be("ab");
    }

    /// <summary>
    /// Whether the page built the field is the position's to say, not a reader's. A reader holding the
    /// node of an older frame (the host keeps its last frame until the next is drawn) would otherwise
    /// take that node's value, and what was typed since the page last built the field would be gone.
    /// </summary>
    [Fact]
    public void AReaderHoldingAnOlderNode_LeavesWhatWasTyped()
    {
        var (host, page, _, _) = Open(Handler.Ignores);
        var older = host.LastFrame!.TextRegions.Single();

        page.Hold("built");
        Frame(host);
        host.TextInput("X");
        Frame(host);
        var shown = Heard(host);
        shown.Should().Contain("X").And.Contain("b");

        FieldText.Of(older.Element, older.Entry).Should().Be(shown);
        Frame(host);

        Heard(host).Should().Be(shown, "what was typed stays until the page builds the field again");
    }

    [Fact]
    public void AnotherFieldAtTheSamePlace_StartsFromItsOwnValue()
    {
        var (host, page, _, _) = Open(Handler.Ignores);

        host.TextInput("typed");
        Frame(host);
        Heard(host).Should().Be("typed");

        page.Replace("its own");
        Frame(host);

        Heard(host).Should().Be("its own", "what was typed belonged to the position that left");
    }
}
