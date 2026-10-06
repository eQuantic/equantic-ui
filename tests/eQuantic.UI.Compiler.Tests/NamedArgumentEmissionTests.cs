using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// Named constructor arguments that SKIP earlier optional parameters, meeting an object initializer.
/// A class's arguments land in their parameters as the bound tree binds them, a skipped one passed as
/// undefined so the twin's own default runs, and the initializer is applied to what the constructor
/// built (#582, #583). It was a trailing config object, whose slot the skipped defaults had to be
/// counted up to: found by the Spreadsheet's resize grips, where
/// <c>new Positioned(grip, bottom: 0, start: 0) { Layer = 1 }</c> emitted two extra nulls and the layer
/// fell off the signature on the web. The vocabulary's twins still take that config object.
/// </summary>
public class NamedArgumentEmissionTests
{
    [Fact]
    public void NamedArgsSkippingEarlierParams_WithInitializer_LandInTheirParameters_ThenTheInitializer()
    {
        var js = TestHelper.ConvertExpression(
            "var node = new Anchor(\"x\", bottom: 1, start: 2) { Layer = 3 }");

        Assert.Contains("new Anchor('x', undefined, undefined, 1, 2)", js);
        Assert.Contains(".layer = ", js);
        Assert.DoesNotContain("{ layer: 3 }", js);
    }

    [Fact]
    public void NamedArgsAsAPrefix_WithInitializer_LeaveTheRestToTheirDefaults()
    {
        var js = TestHelper.ConvertExpression(
            "var node = new Anchor(\"x\", top: 1, end: 2) { Layer = 3 }");

        Assert.Contains("new Anchor('x', 1, 2)", js);
        Assert.Contains(".layer = ", js);
    }
}
