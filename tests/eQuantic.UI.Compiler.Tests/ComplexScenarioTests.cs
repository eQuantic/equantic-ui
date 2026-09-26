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
        var body = method.Body
            ?? throw new InvalidOperationException("the harness wraps the snippet in a block-bodied method");
        return _converter.Convert(body).Trim();
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

        // 2. Dictionary & Initializers: with no model, the dictionary is known by the name its
        // creation writes, and the string literal keys keep their casing.
        Assert.Contains("let config = $eq.collections.dictionary([['timeout', 1000], ['retries', 3]]);", js);

        // 3. Dictionary ContainsKey, a name only a dictionary answers
        Assert.Contains("if (config.has('timeout'))", js);

        // 4. Math.Clamp -> Math.min(Math.max(val, min), max). The entry read inside it is a
        // dictionary's only where a model can say so, which this harness has none of.
        Assert.Contains("let safeTimeout = Math.min(Math.max(", js);
        Assert.Contains(", 100), 5000);", js);

        // 5. Pattern Matching: the bound variable is assigned inside the condition (guarded by &&).
        Assert.Contains("(typeof input === 'string' && (s = input, true))", js);

        // 6. Lambdas & List
        Assert.Contains("let values = [1, 2, 3];", js);
        // Select -> map, ToList -> removed/passthrough
        Assert.Contains("let doubled = values.map((x) => x * 2);", js);

        // 7. Await
        // Task.Delay might not be fully transpiled if no specific strategy, but 'await' keyword must exist
        Assert.Contains("await ", js);
        
        // 8. Unary
        Assert.Contains("count++;", js);

        // 9. Ternary
        Assert.Contains("let status = config.has('retries') ? 'Ready' : 'Error';", js);
    }
}
