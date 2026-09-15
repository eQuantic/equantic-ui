using eQuantic.UI.Conformance.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The folding the two host-newline conformance cases lean on, exercised where it can be.
///
/// <para>
/// Those cases compare only when the two sides DIFFER, and they differ only on Windows — so on any
/// other machine the folding never runs and the suite would be claiming a comparison it had never
/// made. This runs it, on the exact strings the Windows runner reported.
/// </para>
///
/// <para>
/// The values are JSON-ENCODED, because that is what both evaluators hand back: the escape in the
/// text is a backslash and an `r`, not a carriage return. A fold written against real control
/// characters would do nothing here and pass by matching two unchanged strings.
/// </para>
/// </summary>
public class HostNewlineFoldingTests
{
    // What the Windows runner actually printed, both sides, for `"ab\r\ncd\ref".ReplaceLineEndings()`.
    private const string FromWindowsDotNet = "\"ab\\r\\ncd\\r\\nef\"";
    private const string FromTheTwin = "\"ab\\ncd\\nef\"";

    [Fact]
    public void TheHostsNewlineFoldsToTheSdks()
    {
        ConformanceRunner.Folded(FromWindowsDotNet).Should().Be(FromTheTwin);
        ConformanceRunner.Folded(FromTheTwin).Should().Be(FromTheTwin, "folding is idempotent");
    }

    /// <summary>…and anything else survives it, which is the half that keeps the tolerance a
    /// tolerance: a translation defect must still fail after folding.</summary>
    [Theory]
    [InlineData("\"ab\\ncd\"", "\"ab\\ncd\\nef\"")]   // a missing line
    [InlineData("\"AB\\r\\n\"", "\"ab\\n\"")]          // the wrong case
    [InlineData("\"ab\\r\"", "\"ab\\n\"")]             // a bare CR, which is NOT the host's newline
    public void AnythingElseStillDiffers(string left, string right)
    {
        ConformanceRunner.Folded(left).Should().NotBe(ConformanceRunner.Folded(right));
    }
}
