using eQuantic.UI.Compiler.CodeGen;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class AdditionalTypesTests
{
    [Fact]
    public void DateTime_Now_Static_ConvertsToNewDate()
    {
        var code = "DateTime.Now";
        var js = ConvertExpression(code);
        Assert.Equal("new Date()", js);
    }
    
    // Instance members require semantic model - moved to integration tests
    /*
    [Fact]
    public void DateTime_InstanceMembers_ConvertsToJSMethods()
    {
        var code = "d.Year + d.Month";
        var js = ConvertExpression(code);
        Assert.Equal("d.getFullYear() + (d.getMonth() + 1)", js);
    }
    */

    [Fact]
    public void TimeSpan_FromSeconds_ConvertsToMilliseconds()
    {
        var code = "TimeSpan.FromSeconds(5)";
        var js = ConvertExpression(code);
        Assert.Equal("(5 * 1000)", js);
    }
    
    [Fact]
    public void Regex_IsMatch_ConvertsToTest()
    {
        var code = "Regex.IsMatch(s, \"^abc\")";
        var js = ConvertExpression(code);
        Assert.Equal("new RegExp('^abc').test(s)", js);
    }
    
    [Fact]
    public void HashSet_New_ConvertsToSet()
    {
        var code = "new HashSet<int>()";
        var js = ConvertExpression(code);
        Assert.Equal("new Set()", js);
    }
    
    /*
    [Fact]
    public void HashSet_Add_ConvertsToAdd()
    {
        var code = "set.Add(1)";
        var js = ConvertExpression(code);
        Assert.Equal("set.add(1)", js);
    }
    */

    private string ConvertExpression(string code)
    {
        var converter = new CSharpToJsConverter();
        var expr = SyntaxFactory.ParseExpression(code);
        return converter.ConvertExpression(expr);
    }
}
