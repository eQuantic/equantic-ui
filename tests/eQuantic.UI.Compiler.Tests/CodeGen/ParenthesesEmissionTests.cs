using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.CodeGen;

/// <summary>
/// End to end through the authoritative harness: the author's parentheses are re-derived by the
/// writer — redundant pairs go, necessary pairs stay, and a pair JavaScript needs that C# did not
/// is put in.
/// </summary>
public class ParenthesesEmissionTests
{
    [Theory]
    [InlineData("var r = ((name)) + \"x\";", "let r = this.name + 'x';")]
    [InlineData("var r = (list1).Count;", "let r = this.list1.length;")]
    [InlineData("var r = (list1.Count) + 1;", "let r = this.list1.length + 1;")]
    [InlineData("var r = (list1.Count + 1) * 2;", "let r = (this.list1.length + 1) * 2;")]
    [InlineData("var r = FetchValue((name + \"x\"));", "let r = this.fetchValue(this.name + 'x');")]
    [InlineData("var r = (status == Status.Active ? list1 : list2).Count;",
                "let r = (this.status === 'active' ? this.list1 : this.list2).length;")]
    // ToString lowers through a template strategy (`String(…)`): an UNMIGRATED consumer, so the
    // author's parentheses around a number stay — which is also what keeps `(1).x` parsing.
    [InlineData("var r = (1).ToString();", "let r = String((1));")]
    [InlineData("var r = (list1).ToString();", "let r = String(this.list1);")]
    public void AuthorParentheses_AreRederived(string code, string expected)
    {
        TestHelper.ConvertCodeBlock(code).Trim().Should().Be(expected);
    }
}
