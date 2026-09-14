using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A cut line ends ONE way, and every measurer on this machine is asked the same question.
///
/// <para>
/// `ITextMeasurer.Measure` has promised "truncated to maxLines with a trailing ellipsis" since it was
/// written, and a cut line used to end four different ways. The web drew the mark with CSS; Android
/// put the character into the string itself; CoreText and DirectWrite cut and drew nothing, with the
/// exemption written in one platform class's doc comment — where nobody reading the interface would
/// find it. Nothing asked the implementations whether they met the contract, which is why the answer
/// could differ per platform for as long as it did.
/// </para>
///
/// <para>
/// The fix is Flutter's shape: the truncation happens in the LAYOUT, so the width a measurer reports
/// already includes the mark and the glyphs a rasterizer draws are the same line. `didExceedMaxLines`
/// is their `Ellipsized` — the neutral fact that a cut happened, readable without knowing how any
/// platform drew it.
/// </para>
///
/// <para>
/// This suite runs against whatever is available where it executes: the reference measurer always,
/// CoreText on a Mac, DirectWrite on Windows. A platform this machine cannot host is not silently
/// skipped — the implementations exercised are asserted, so a suite that quietly tested nothing
/// fails instead.
/// </para>
///
/// <para>
/// And one of them does not meet the contract yet. DirectWrite cuts and draws nothing, which is a
/// v1 fence stated in its own doc, so it is not in the contract theories: leaving it there is a
/// KNOWN-FAILING test, met as noise by anyone on Windows and invisible to this repository's CI,
/// whose only <c>dotnet test</c> runs on macOS. It is not simply left out either — an omission
/// says nothing and lets a gap age. It has its own assertion that it STILL does not draw the mark,
/// so the exemption cannot go stale: the day DirectWrite trims, that test fails and names what to
/// change. Raised by Copilot on #136.
/// </para>
/// </summary>
public class TruncationContractTests
{
    private const string Ellipsis = "…";

    /// <summary>Long enough that one line cannot hold it, and made of real words so a greedy wrap has
    /// somewhere to break.</summary>
    private const string Paragraph =
        "the quick brown fox jumps over the lazy dog and keeps going well past the edge of the box";

    private static readonly TypeStyle Style = PhotonTheme.Instance.Type(TypeRole.BodyM);

    /// <summary>
    /// A measurer this machine can host, and whether it CLAIMS the trailing mark.
    /// <para>
    /// The flag exists because one implementation does not meet the contract yet, and there are two
    /// wrong ways to say so. Running the contract theories over it leaves a known-failing test in
    /// the suite, which a Windows developer meets as noise on a clean checkout — and which THIS
    /// repository's CI cannot even see, since the only `dotnet test` runs on macOS
    /// (<c>ci.yml</c>, the `build-packages` job). Leaving it out entirely says nothing and lets the
    /// gap age quietly. So it is out of the contract theories and INTO its own, which asserts that
    /// it still fails — the exemption cannot go stale, because the day DirectWrite trims, that test
    /// fails and someone comes here to flip the flag.
    /// </para>
    /// </summary>
    private sealed record Measurer(string Name, Func<ITextMeasurer> Create, bool ClaimsTheMark);

    private static readonly IReadOnlyList<Measurer> Hosted = Build();

    private static List<Measurer> Build()
    {
        var all = new List<Measurer>
        {
            new("reference", () => ApproximateTextMeasurer.Instance, ClaimsTheMark: true),
        };
        if (OperatingSystem.IsMacOS())
            all.Add(new("CoreText", () => new eQuantic.UI.Native.Shell.Apple.CoreTextService(), true));
        if (OperatingSystem.IsWindows())
            all.Add(new("DirectWrite",
                () => new eQuantic.UI.Native.Shell.Windows.Graphics.DirectWriteTextService(),
                // v1 fence, stated in DirectWriteTextService's own doc: it cuts and draws nothing.
                // Needs a Windows machine to fix on, and `Withholding` below keeps asking.
                ClaimsTheMark: false));
        return all;
    }

    private static ITextMeasurer Named(string name) =>
        Hosted.First(m => m.Name == name).Create();

    /// <summary>The measurers that claim the contract — what the theories below are about.</summary>
    public static TheoryData<string> Available
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var m in Hosted.Where(m => m.ClaimsTheMark)) data.Add(m.Name);
            return data;
        }
    }

    /// <summary>
    /// The guard that keeps the rest honest. A differential suite whose subjects all failed to load
    /// passes by reporting nothing, which is the empty pass this repo has been caught by before.
    /// </summary>
    [Fact]
    public void TheSuiteExercisesEveryMeasurerThisMachineCanHost()
    {
        var names = Hosted.Select(m => m.Name).ToList();
        names.Should().Contain("reference");
        if (OperatingSystem.IsMacOS())
            names.Should().Contain("CoreText", "a Mac hosts CoreText and the contract is per implementation");
        if (OperatingSystem.IsWindows())
            names.Should().Contain("DirectWrite", "and a Windows box hosts DirectWrite");
        names.Should().HaveCountGreaterThan(
            OperatingSystem.IsMacOS() || OperatingSystem.IsWindows() ? 1 : 0);
        Available.Count.Should().BeGreaterThan(0,
            "the contract theories run over the claimants, and a machine where nothing claims it "
            + "would pass them all by having nothing to say");
    }

    /// <summary>
    /// THE GAP, asserted rather than skipped. `ITextMeasurer.Measure` promises a trailing ellipsis
    /// and DirectWrite reports the wrapped line's width with no mark on it, so a cut line there
    /// measures EXACTLY as wide as the wrap — which is the arithmetic proof that nothing was cut.
    ///
    /// <para>
    /// This is the shape an exemption has to take here: it fails the day the gap closes, and the
    /// failure says what to do. An exemption that only skipped would let a fixed implementation go
    /// on being described as broken, which is the stale half the coverage pins in this repository
    /// already guard both directions of.
    /// </para>
    /// </summary>
    /// <para>
    /// A FACT rather than a theory over the withholders, because on a Mac there are none and an
    /// empty theory is an xUnit error rather than a pass. Nothing is skipped silently by that:
    /// which implementations this machine hosts is asserted above, so a Windows run that somehow
    /// stopped seeing DirectWrite fails there instead of quietly finding nothing here.
    /// </para>
    [Fact]
    public void AMeasurerThatDoesNotClaimTheMark_StillDoesNotDrawIt()
    {
        foreach (var withholding in Hosted.Where(m => !m.ClaimsTheMark))
        {
            var measurer = withholding.Create();

            var cut = measurer.Measure(Paragraph, Style, 1f, maxWidth: 160, maxLines: 1);
            var wrapped = measurer.Measure(Paragraph, Style, 1f, maxWidth: 160, maxLines: 0);

            cut.Lines[0].Width.Should().Be(wrapped.Lines[0].Width,
                $"{withholding.Name} does not truncate: it reports the line the wrap produced, mark "
                + "and all missing. If this FAILS, the implementation started meeting the contract "
                + "— move it to ClaimsTheMark: true and delete this case rather than loosening it.");
        }
    }

    /// <summary>A line that was cut says so. The neutral fact, which is all a caller should need.</summary>
    [Theory]
    [MemberData(nameof(Available))]
    public void ACutLine_ReportsTheCut(string name)
    {
        var measured = Named(name).Measure(Paragraph, Style, 1f, maxWidth: 160, maxLines: 2);

        measured.Lines.Should().HaveCount(2, "the paragraph does not fit in two lines by accident");
        measured.Lines[^1].Ellipsized.Should().BeTrue();
    }

    /// <summary>…and a line that was not cut does not, which is the half an assertion about presence
    /// forgets.</summary>
    [Theory]
    [MemberData(nameof(Available))]
    public void AWholeLine_ReportsNothing(string name)
    {
        var measured = Named(name).Measure("short", Style, 1f, maxWidth: 400, maxLines: 0);

        measured.Lines.Should().OnlyContain(line => !line.Ellipsized);
    }

    /// <summary>
    /// THE PIXELS, not only the number. `Measure` and `Rasterize` go through one helper so they
    /// cannot disagree — but that is a claim about the code, and this is the assertion that would
    /// fail if the helper stopped being shared. Found in review: every existing CoreText raster test
    /// uses an unconstrained width, so none of them exercises truncation at all, and a regression
    /// could leave measured widths carrying the mark while the glyphs drew the untruncated line.
    ///
    /// <para>
    /// A truncated raster is WIDER than the same paragraph's natural first line, for the reason the
    /// measurement is: the cut runs to the character and pays for the mark. The bitmap is compared
    /// against the measurement rather than against a golden, because a golden would pin this
    /// machine's font and this suite is about the contract.
    /// </para>
    /// </summary>
    [Fact]
    public void TheRasterDrawsTheLineThatWasMeasured()
    {
        if (!OperatingSystem.IsMacOS()) return;
        var service = new eQuantic.UI.Native.Shell.Apple.CoreTextService();

        var cut = service.Measure(Paragraph, Style, 1f, maxWidth: 160, maxLines: 1);
        cut.Lines[0].Ellipsized.Should().BeTrue("the fixture has to truncate for this to mean anything");

        var raster = service.Rasterize(Paragraph, Style, 1f, maxWidth: 160, maxLines: 1,
            scale: 1f, TextAlignment.Start);

        raster.Should().NotBeNull();
        // Within a pixel: the raster is cut to the ink it drew, and the measurement is typographic.
        raster!.Width.Should().BeInRange((int)cut.Lines[0].Width - 2, (int)cut.Lines[0].Width + 2,
            "the rasterizer draws the line the measurer measured — the same truncated line, not the "
            + "frame's untruncated one");
    }

    /// <summary>
    /// WHICH SIDE survives — the half the rest of this file never asked about, and the half that
    /// was wrong for a whole release.
    ///
    /// <para>
    /// `0.2.0-preview.53` shipped `CTLineCreateTruncatedLine(..., 2, ...)` with a comment calling 2
    /// `kCTLineTruncationEnd`. `CTLine.h` says 2 is `kCTLineTruncationMiddle`. So macOS cut the
    /// MIDDLE out of every truncated label — "Replace hardco…with IAppTheme" — while the web
    /// realizer, emitting `text-overflow: ellipsis`, cut the end. The same tree, two different cuts,
    /// and each one looks deliberate on its own. Reported by the IDE consumer, who read the header,
    /// read the line and magnified the render rather than inferring from any one of them.
    /// </para>
    ///
    /// <para>
    /// Every other assertion here passed throughout: a mark appeared, the cut line was wider than a
    /// wrap, the raster matched the measurement. All of that is true of a middle cut. Nothing asked
    /// the one question that separates them, which is this file's own review question turned on
    /// itself — the instrument was never exercised in the condition it exists for.
    /// </para>
    ///
    /// <para>
    /// Asked without reading any text back, because a `MeasuredLine` carries a width and a flag and
    /// no characters: two strings that share a HEAD and differ in their TAIL must cut to the same
    /// width, since only the head survives. A middle cut keeps the tails and reports two widths; a
    /// start cut keeps them and reports two widths. Verified by putting each wrong constant back.
    /// </para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Available))]
    public void ACutLine_KeepsTheBeginning(string name)
    {
        var measurer = Named(name);
        const string Head = "the quick brown fox jumps over the lazy dog and keeps going";

        var narrow = measurer.Measure(Head + " iiiiiiiiiiii", Style, 1f, maxWidth: 160, maxLines: 1);
        var wide = measurer.Measure(Head + " WWWWWWWWWWWW", Style, 1f, maxWidth: 160, maxLines: 1);

        narrow.Lines[0].Ellipsized.Should().BeTrue("both fixtures have to truncate for this to mean anything");
        wide.Lines[0].Ellipsized.Should().BeTrue();

        wide.Lines[0].Width.Should().BeApproximately(narrow.Lines[0].Width, 0.01f,
            "two strings with the same head cut to the same line, because the head is what a "
            + "trailing ellipsis keeps. A measurer that reports two widths kept the TAILS — it is "
            + "cutting the middle or the start, and the contract, the handoff and the other "
            + "realizer all say the end");
    }

    /// <summary>
    /// THE CONTRACT, and the assertion that took three attempts to make honest.
    ///
    /// <para>
    /// A truncated line FILLS the box further than a wrapped one. A wrap stops at the last word that
    /// fits; a truncation keeps going to the character and pays for the mark out of what is left. So
    /// the same text cut to one line must measure WIDER than the first line the same text wraps to
    /// when nothing is cut.
    /// </para>
    ///
    /// <para>
    /// The first two versions of this test asserted that the cut line fits the box — which the
    /// UNFIXED code also satisfied, because a line the frame already wrapped fits by construction.
    /// Both passed with the fix reverted. What settled it was measuring: CoreText reported 138.27 in
    /// a 160 box where the mark is 11.84 wide, which is the natural wrap point untouched, and 144.81
    /// once the last line was rebuilt from the remaining content before being truncated.
    /// </para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Available))]
    public void ACutLine_FillsTheBoxFurtherThanAWrappedOne(string name)
    {
        var measurer = Named(name);

        var cut = measurer.Measure(Paragraph, Style, 1f, maxWidth: 160, maxLines: 1);
        cut.Lines.Should().ContainSingle();
        cut.Lines[0].Ellipsized.Should().BeTrue();

        var wrapped = measurer.Measure(Paragraph, Style, 1f, maxWidth: 160, maxLines: 0);
        wrapped.Lines.Count.Should().BeGreaterThan(1, "the paragraph has to overflow for this to mean anything");
        wrapped.Lines[0].Ellipsized.Should().BeFalse();

        cut.Lines[0].Width.Should().BeGreaterThan(wrapped.Lines[0].Width,
            "a truncation runs to the character and pays for the mark, where a wrap stops at the "
            + "last whole word — a measurer that reports the bare wrap is not truncating at all");

        cut.Lines[0].Width.Should().BeLessThanOrEqualTo(160.5f,
            "and it still fits what it was truncated to: the mark is inside the measurement, not "
            + "drawn on top of it");
    }
}
