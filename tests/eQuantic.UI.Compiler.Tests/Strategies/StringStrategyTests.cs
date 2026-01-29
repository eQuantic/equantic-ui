using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class StringStrategyTests
{
    // ============ Instance Methods ============

    [Fact]
    public void Split_NoArgs_MapsToSplitEmpty()
    {
        var result = TestHelper.ConvertExpression("str.Split()");
        result.Should().Be("this.str.split('')");
    }

    [Fact]
    public void Split_WithSeparator_MapsToSplit()
    {
        var result = TestHelper.ConvertExpression("str.Split(',')");
        result.Should().Be("this.str.split(',')");
    }

    [Fact]
    public void Replace_MapsToReplaceAll()
    {
        var result = TestHelper.ConvertExpression("str.Replace(\"old\", \"new\")");
        result.Should().Be("this.str.replaceAll('old', 'new')");
    }

    [Fact]
    public void StartsWith_MapsToStartsWith()
    {
        var result = TestHelper.ConvertExpression("str.StartsWith(\"prefix\")");
        result.Should().Be("this.str.startsWith('prefix')");
    }

    [Fact]
    public void EndsWith_MapsToEndsWith()
    {
        var result = TestHelper.ConvertExpression("str.EndsWith(\"suffix\")");
        result.Should().Be("this.str.endsWith('suffix')");
    }

    [Fact]
    public void Contains_String_MapsToIncludes()
    {
        var result = TestHelper.ConvertExpression("str.Contains(\"sub\")");
        result.Should().Be("this.str.includes('sub')");
    }

    [Fact]
    public void Substring_OneArg_MapsToSlice()
    {
        var result = TestHelper.ConvertExpression("str.Substring(5)");
        result.Should().Be("this.str.slice(5)");
    }

    [Fact]
    public void Substring_TwoArgs_MapsToSubstring()
    {
        var result = TestHelper.ConvertExpression("str.Substring(5, 10)");
        result.Should().Be("this.str.substring(5, 5 + 10)");
    }

    [Fact]
    public void IndexOf_MapsToIndexOf()
    {
        var result = TestHelper.ConvertExpression("str.IndexOf(\"x\")");
        result.Should().Be("this.str.indexOf('x')");
    }

    [Fact]
    public void LastIndexOf_MapsToLastIndexOf()
    {
        var result = TestHelper.ConvertExpression("str.LastIndexOf(\"x\")");
        result.Should().Be("this.str.lastIndexOf('x')");
    }

    [Fact]
    public void PadLeft_MapsTopadStart()
    {
        var result = TestHelper.ConvertExpression("str.PadLeft(10)");
        result.Should().Be("this.str.padStart(10)");
    }

    [Fact]
    public void PadLeft_WithChar_MapsTopadStart()
    {
        var result = TestHelper.ConvertExpression("str.PadLeft(10, '0')");
        result.Should().Be("this.str.padStart(10, '0')");
    }

    [Fact]
    public void PadRight_MapsTopadEnd()
    {
        var result = TestHelper.ConvertExpression("str.PadRight(10)");
        result.Should().Be("this.str.padEnd(10)");
    }

    [Fact]
    public void TrimStart_MapsToTrimStart()
    {
        var result = TestHelper.ConvertExpression("str.TrimStart()");
        result.Should().Be("this.str.trimStart()");
    }

    [Fact]
    public void TrimEnd_MapsToTrimEnd()
    {
        var result = TestHelper.ConvertExpression("str.TrimEnd()");
        result.Should().Be("this.str.trimEnd()");
    }

    [Fact]
    public void ToCharArray_MapsToSpread()
    {
        var result = TestHelper.ConvertExpression("str.ToCharArray()");
        result.Should().Be("[...this.str]");
    }

    [Fact]
    public void Insert_MapsToSliceConcat()
    {
        var result = TestHelper.ConvertExpression("str.Insert(5, \"text\")");
        result.Should().Be("(this.str.slice(0, 5) + 'text' + this.str.slice(5))");
    }

    [Fact]
    public void Remove_OneArg_MapsToSlice()
    {
        var result = TestHelper.ConvertExpression("str.Remove(5)");
        result.Should().Be("this.str.slice(0, 5)");
    }

    [Fact]
    public void Remove_TwoArgs_MapsToSliceConcat()
    {
        var result = TestHelper.ConvertExpression("str.Remove(5, 3)");
        result.Should().Be("(this.str.slice(0, 5) + this.str.slice(5 + 3))");
    }

    // ============ Static Methods ============

    [Fact]
    public void IsNullOrEmpty_MapsToFalsyCheck()
    {
        var result = TestHelper.ConvertExpression("string.IsNullOrEmpty(str)");
        result.Should().Be("(!this.str || this.str === '')");
    }

    [Fact]
    public void IsNullOrWhiteSpace_MapsToTrimCheck()
    {
        var result = TestHelper.ConvertExpression("string.IsNullOrWhiteSpace(str)");
        result.Should().Be("(!this.str || this.str.trim() === '')");
    }

    [Fact]
    public void Join_MapsToArrayJoin()
    {
        var result = TestHelper.ConvertExpression("string.Join(\", \", items)");
        result.Should().Be("this.items.join(', ')");
    }

    [Fact]
    public void Concat_MapsToPlus()
    {
        var result = TestHelper.ConvertExpression("string.Concat(a, b, c)");
        result.Should().Be("(this.a + this.b + this.c)");
    }

    [Fact]
    public void Compare_MapsToLocaleCompare()
    {
        var result = TestHelper.ConvertExpression("string.Compare(a, b)");
        result.Should().Be("this.a.localeCompare(this.b)");
    }

    [Fact]
    public void Equals_MapsToStrictEquality()
    {
        var result = TestHelper.ConvertExpression("string.Equals(a, b)");
        result.Should().Be("(this.a === this.b)");
    }
}
