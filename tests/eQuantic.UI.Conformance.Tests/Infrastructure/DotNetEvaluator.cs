using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace eQuantic.UI.Conformance.Tests.Infrastructure;

/// <summary>
/// Evaluates a C# expression with Roslyn scripting and serializes the result to canonical JSON,
/// for comparison against the JSON printed by the transpiled-and-executed JS.
/// </summary>
public static class DotNetEvaluator
{
    private static readonly ScriptOptions Options = ScriptOptions.Default
        .AddReferences(
            typeof(object).Assembly,
            typeof(System.Linq.Enumerable).Assembly,
            typeof(System.Collections.Generic.List<>).Assembly)
        .AddImports("System", "System.Linq", "System.Collections.Generic");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        // Match JSON.stringify of the transpiled output: object property names are camelCased
        // by the transpiler, so serialize .NET objects camelCase too for a fair comparison.
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string EvaluateToJson(string csharpExpression, string prelude = "")
    {
        // A prelude (type declarations such as enums/records) runs before the trailing expression;
        // CSharpScript returns the value of that final expression.
        var script = string.IsNullOrWhiteSpace(prelude) ? csharpExpression : $"{prelude}\n{csharpExpression}";
        var value = CSharpScript.EvaluateAsync<object?>(script, Options).GetAwaiter().GetResult();
        return JsonSerializer.Serialize(value, JsonOptions);
    }
}
