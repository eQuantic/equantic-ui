using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class WithExpressionTests : StrategyTestBase
{
    [Fact]
    public void WithExpression_ConvertsToObjectSpread()
    {
        var code = @"
            void Run() {
                var p1 = new Person(""Alice"", 30);
                var p2 = p1 with { Age = 31 };
            }
        ";

        var js = Convert(code);

        // Expect match for p2 declaration with object spread
        // p1 is likely transpiled as p1 (camelCase if it was a property, but local variable is p1)
        // Age -> age? Yes, WithExpressionStrategy uses ConvertExpression(assignment.Left)
        // assignment.Left is IdentifierName "Age". IdentifierStrategy converts to "age".
        Assert.Matches(@"p2\s*=\s*\{\s*\.\.\.p1,\s*age:\s*31\s*\}", js);
    }

    [Fact]
    public void WithExpression_MultipleProperties_ConvertsCorrectly()
    {
        var code = @"
            var p2 = p1 with { Name = ""Bob"", Age = 40 };
        ";

        var js = Convert(code);

        Assert.Contains("...p1", js);
        Assert.Contains("name: 'Bob'", js.Replace("\"", "'")); // IdentifierStrategy likely camelCases it
        Assert.Contains("age: 40", js);
    }
}
