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

    /// <summary>
    /// JavaScript takes one catch: two clauses were two catches, a SyntaxError that cost the module,
    /// and a clause never tested its type (#474). The conformance suite runs the shapes; this pins
    /// that there is one catch, testing each type in order and rethrowing what none takes.
    /// </summary>
    [Fact]
    public void TwoClauses_AreOneCatchThatTestsEachTypeInOrder()
    {
        var result = TestHelper.ConvertStatement(@"
            try { Console.WriteLine(1); }
            catch (InvalidOperationException) { Console.WriteLine(2); }
            catch (ArgumentException e) when (e.Message.Length > 0) { Console.WriteLine(e.Message); }");

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(result, @"\bcatch\b"));
        Assert.Contains("catch ($e)", result);
        Assert.Contains("let e = $e;", result);
        var first = result.IndexOf("$eq.exceptions.is($e, 'System.InvalidOperationException')", StringComparison.Ordinal);
        var second = result.IndexOf("$eq.exceptions.is($e, 'System.ArgumentException') && $eq.exceptions.filter(() => ", StringComparison.Ordinal);
        Assert.True(first >= 0 && second > first, result);
        Assert.Contains("throw $e;", result);
    }

    /// <summary>`throw;` rethrows what the clause caught; JavaScript has no bare `throw;`, which is a
    /// SyntaxError.</summary>
    [Fact]
    public void ABareRethrow_RethrowsTheExceptionCaught()
    {
        var result = TestHelper.ConvertStatement(@"
            try { Console.WriteLine(1); }
            catch (Exception) { Console.WriteLine(2); throw; }");

        Assert.Contains("catch ($e)", result);
        Assert.Contains("throw $e;", result);
        Assert.DoesNotContain("throw;", result);
    }
}
