using DefaultUIDashboard.Screens;
using eQuantic.UI.Code;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The dashboard's signature provider (<see cref="LedgerSignatures"/>), the one its code screen hands the
/// editor, asked as the panel asks it: about the caret at the end of what is written. A comma, a parenthesis
/// or a colon inside a string, a character or a comment separates nothing, which the SDK's own C# tokens
/// say: the provider counted every comma, and took a parenthesis in a string for the call (#298's review).
/// </summary>
public class LedgerSignaturesTests
{
    private static CodeSignatureHelp? Ask(string text)
    {
        var document = CodeDocument.FromText(text);
        return new LedgerSignatures().SignatureHelpAsync(document, document.End,
            new CodeSignatureContext(CodeSignatureTrigger.Invoked), CancellationToken.None).Result;
    }

    [Theory]
    [InlineData("var i = new Invoice(\"ACME,", 0)]
    [InlineData("var i = new Invoice(\"ACME, Inc.\", ", 1)]
    [InlineData("var i = new Invoice(\"say \\\"hi, there\\\"", 0)]
    [InlineData("var i = new Invoice(@\"C:\\ledger\\, x", 0)]
    [InlineData("var i = new Invoice(@\"a\"\"b, c", 0)]
    [InlineData("var i = new Invoice(\"ACME\" /* a, b */", 0)]
    [InlineData("var i = new Invoice(',', ", 1)]
    [InlineData("var i = new Invoice(\"(\", ", 1)]
    [InlineData("var i = new Invoice(\"a:b", 0)]
    [InlineData("var i = new Invoice(issuedAt: ", 1)]
    public void TheArgumentTheCaretIsOn_IsReadFromTheCode_NotFromItsText(string text, int parameter)
    {
        var help = Ask(text);

        help.Should().NotBeNull("the caret is in a call to Invoice");
        help!.ActiveParameter.Should().Be(parameter);
    }

    [Fact]
    public void AParenthesisInAComment_OpensNoCall()
    {
        Ask("var total = 0; // see Invoice(").Should().BeNull();
    }
}
