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

        // camelCase, and an ARROW rather than a `function`: a local function inside a method can
        // use the instance, and a `function` declaration rebinds `this` to undefined in a module.
        Assert.Contains("const run = () => {", js);
        Assert.Contains("let x = 10;", js);
        Assert.Contains("const local = () => {", js);
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

        // No annotations here: this harness converts to plain JS, which is also what the
        // conformance runner EXECUTES. Types are for the module emission.
        Assert.Contains("const add = (a, b) => {", js);
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

        Assert.Contains("const square = (x) => {", js);
        Assert.Contains("return x * x;", js);
    }
}
