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
/// CoreText on a Mac. A platform this machine cannot host is not silently skipped — the count of
/// implementations exercised is asserted, so a suite that quietly tested nothing fails instead.
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

    /// <summary>Every measurer this machine can actually host.</summary>
    public static TheoryData<string> Available
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var name in Measurers.Keys) data.Add(name);
            return data;
        }
    }

    private static readonly Dictionary<string, Func<ITextMeasurer>> Measurers = Build();

    private static Dictionary<string, Func<ITextMeasurer>> Build()
    {
        var map = new Dictionary<string, Func<ITextMeasurer>>
        {
            ["reference"] = () => ApproximateTextMeasurer.Instance,
        };
        if (OperatingSystem.IsMacOS())
            map["CoreText"] = () => new eQuantic.UI.Native.Shell.Apple.CoreTextService();
        return map;
    }

    /// <summary>
    /// The guard that keeps the rest honest. A differential suite whose subjects all failed to load
    /// passes by reporting nothing, which is the empty pass this repo has been caught by before.
    /// </summary>
    [Fact]
    public void TheSuiteExercisesEveryMeasurerThisMachineCanHost()
    {
        Measurers.Should().ContainKey("reference");
        if (OperatingSystem.IsMacOS())
            Measurers.Should().ContainKey("CoreText", "a Mac hosts CoreText and the contract is per implementation");
        Measurers.Should().HaveCountGreaterThan(OperatingSystem.IsMacOS() ? 1 : 0);
    }

    /// <summary>A line that was cut says so. The neutral fact, which is all a caller should need.</summary>
    [Theory]
    [MemberData(nameof(Available))]
    public void ACutLine_ReportsTheCut(string name)
    {
        var measured = Measurers[name]().Measure(Paragraph, Style, 1f, maxWidth: 160, maxLines: 2);

        measured.Lines.Should().HaveCount(2, "the paragraph does not fit in two lines by accident");
        measured.Lines[^1].Ellipsized.Should().BeTrue();
    }

    /// <summary>…and a line that was not cut does not, which is the half an assertion about presence
    /// forgets.</summary>
    [Theory]
    [MemberData(nameof(Available))]
    public void AWholeLine_ReportsNothing(string name)
    {
        var measured = Measurers[name]().Measure("short", Style, 1f, maxWidth: 400, maxLines: 0);

        measured.Lines.Should().OnlyContain(line => !line.Ellipsized);
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
        var measurer = Measurers[name]();

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
