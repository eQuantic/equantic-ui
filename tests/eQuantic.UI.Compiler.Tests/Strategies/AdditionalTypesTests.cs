using eQuantic.UI.Compiler.CodeGen;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class AdditionalTypesTests
{
    [Fact]
    public void DateTime_Now_Static_ConvertsToCompatFactory()
    {
        // DateTime now maps to the tick-precise `dateTime` compat type, not a lossy native Date.
        var code = "DateTime.Now";
        var js = ConvertExpression(code);
        Assert.Equal("dateTime.now()", js);
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
    public void TimeSpan_FromSeconds_ConvertsToCompatFactory()
    {
        // TimeSpan now maps to the tick-precise `timeSpan` compat type, not a bare millisecond number.
        var code = "TimeSpan.FromSeconds(5)";
        var js = ConvertExpression(code);
        Assert.Equal("timeSpan.fromSeconds(5)", js);
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
