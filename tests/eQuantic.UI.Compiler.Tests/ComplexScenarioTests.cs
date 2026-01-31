using System.Linq;
using Xunit;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen;

namespace eQuantic.UI.Compiler.Tests;

public class ComplexScenarioTests
{
    private readonly CSharpToJsConverter _converter = new CSharpToJsConverter();

    private string ConvertMethodBody(string bodyCode)
    {
        var classCode = $"class Wrapper {{ async Task Method() {{ {bodyCode} }} }}";
        var root = CSharpSyntaxTree.ParseText(classCode).GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        return _converter.Convert(method.Body).Trim();
    }

    [Fact]
    public void ComplexStrategyIntegration_AllStrategies_VerifyOutput()
    {
        var code = @"
            // 1. Service Provider & Console (Invocation)
            var service = this.GetRequiredService<IMyService>();
            Console.WriteLine($""Starting processing with {service}"");

            // 2. Dictionary & Initializers (New, Initializer, Dictionary Invocation)
            var config = new Dictionary<string, int> { { ""timeout"", 1000 }, { ""retries"", 3 } };
            
            // 3. Control Flow & Pattern Matching (Statement, IsPattern)
            if (config.ContainsKey(""timeout"")) 
            {
                // 4. Math Strategy (Math.Clamp special case)
                var safeTimeout = Math.Clamp(config[""timeout""], 100, 5000);
                
                // 5. Pattern Matching with Declaration (IsPatternStrategy)
                object input = ""data"";
                if (input is string s) 
                {
                     // 6. Lambdas & Collection Materialization (Lambda, CollectionMaterialization)
                     var values = new List<int> { 1, 2, 3 };
                     var doubled = values.Select(x => x * 2).ToList();
                     
                     // 7. Await & Unary (AwaitExpression, Unary)
                     await Task.Delay(safeTimeout);
                     var count = doubled.Count;
                     count++;
                }
            }
            
            // 8. Ternary & Assignment (Conditional, Assignment)
            var status = config.ContainsKey(""retries"") ? ""Ready"" : ""Error"";
        ";

        var js = ConvertMethodBody(code);

        // Debug output to see what we got if it fails
        // System.Console.WriteLine(js);

        // 1. Service Provider & Console
        // LocalDeclarationStrategy uses 'let'
        // ServiceProviderStrategy now maps GetRequiredService -> getService
        Assert.Contains("let service = this.getService('IMyService');", js);
        Assert.Contains("console.log(`Starting processing with ${service}`);", js);

        // 2. Dictionary & Initializers
        // Verify key casing handling (should preserve 'timeout' as it's a string literal key)
        Assert.Contains("let config = { 'timeout': 1000, 'retries': 3 };", js);

        // 3. Dictionary ContainsKey (now with parentheses for safety)
        Assert.Contains("if (('timeout' in config))", js);

        // 4. Math.Clamp -> Math.min(Math.max(val, min), max)
        Assert.Contains("let safeTimeout = Math.min(Math.max(config['timeout'], 100), 5000);", js);

        // 5. Pattern Matching IIFE
        // ((() => { s = input; return typeof input === 'string'; })())
        Assert.Contains("(() => { s = input; return typeof input === 'string'; })()", js);

        // 6. Lambdas & List
        Assert.Contains("let values = [1, 2, 3];", js);
        // Select -> map, ToList -> removed/passthrough
        Assert.Contains("let doubled = values.map((x) => x * 2);", js);

        // 7. Await
        // Task.Delay might not be fully transpiled if no specific strategy, but 'await' keyword must exist
        Assert.Contains("await ", js);
        
        // 8. Unary
        Assert.Contains("count++;", js);

        // 9. Ternary (ContainsKey now wrapped in parentheses)
        Assert.Contains("let status = ('retries' in config) ? 'Ready' : 'Error';", js);
    }
}
