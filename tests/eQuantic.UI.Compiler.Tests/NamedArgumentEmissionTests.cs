using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// Named constructor arguments that SKIP earlier optional parameters, meeting an object
/// initializer: the reordered argument list already contains the skipped defaults, so the
/// trailing config object must land in the constructor's config slot — not be pushed past the
/// arity by counting the call site's arguments a second time. Found by the Spreadsheet's resize
/// grips: <c>new Positioned(grip, bottom: 0, start: 0) { Layer = 1 }</c> emitted two extra
/// nulls and the layer silently fell off the signature on the web.
/// </summary>
public class NamedArgumentEmissionTests
{
    [Fact]
    public void NamedArgsSkippingEarlierParams_WithInitializer_LandTheConfigInTheTrailingSlot()
    {
        var js = TestHelper.ConvertExpression(
            "var node = new Anchor(\"x\", bottom: 1, start: 2) { Layer = 3 }");

        Assert.Contains("new Anchor('x', null, null, 1, 2, { layer: 3 })", js);
    }

    [Fact]
    public void NamedArgsAsAPrefix_WithInitializer_StillFillTheGapToTheConfig()
    {
        // The already-working shape, pinned so the fix for the skipping case cannot regress it.
        var js = TestHelper.ConvertExpression(
            "var node = new Anchor(\"x\", top: 1, end: 2) { Layer = 3 }");

        Assert.Contains("new Anchor('x', 1, 2, null, null, { layer: 3 })", js);
    }
}
