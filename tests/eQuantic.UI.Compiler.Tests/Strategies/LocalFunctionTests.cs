using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class LocalFunctionTests : StrategyTestBase
{
    [Fact]
    public void LocalFunction_ConvertsToInnerFunction()
    {
        var code = @"
            void Run() {
                var x = 10;
                void Local() {
                    Console.WriteLine(x);
                }
                Local();
            }
        ";

        var js = Convert(code);

        // LocalFunctionStatementStrategy now enforces camelCase
        Assert.Contains("run() {", js);
        Assert.Contains("let x = 10;", js);
        Assert.Contains("function local() {", js);
        Assert.Contains("console.log(x);", js);
        Assert.Contains("local();", js);
    }

    [Fact]
    public void LocalFunction_WithParameters_ConvertsCorrectly()
    {
        var code = @"
            void Run() {
                int Add(int a, int b) {
                    return a + b;
                }
                var sum = Add(1, 2);
            }
        ";

        var js = Convert(code);

        Assert.Contains("function add(a, b) {", js);
        Assert.Contains("return a + b;", js);
        Assert.Contains("let sum = add(1, 2);", js);
    }

    [Fact]
    public void LocalFunction_ArrowSyntax_ConvertsToFunction()
    {
        var code = @"
            void Run() {
                int Square(int x) => x * x;
            }
        ";

        var js = Convert(code);

        Assert.Contains("function square(x) {", js);
        Assert.Contains("return x * x;", js);
    }
}
