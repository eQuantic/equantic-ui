using Xunit;
using eQuantic.UI.Compiler.Tests;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class TryCatchStrategyTests
{
    [Fact]
    public void Convert_TryCatch_ReturnsTryCatch()
    {
        var code = @"
            try
            {
                var x = 1;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }";
            
        var result = TestHelper.ConvertStatement(code);
        
        Assert.Contains("try {", result);
        Assert.Contains("let x = 1", result);
        Assert.Contains("catch (ex)", result);
        // Console.WriteLine maps to console.log
        // ex.Message maps to ex.message (camelCase)
        Assert.Contains("console.log(ex.message)", result);
    }
    
    [Fact]
    public void Convert_TryCatchFinally_ReturnsFullBlock()
    {
        var code = @"
            try
            {
                var x = 1;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                var y = 2;
            }";
            
        var result = TestHelper.ConvertStatement(code);
        
        Assert.Contains("finally {", result);
        Assert.Contains("let y = 2", result);
    }
}
