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
        // Match JSON.stringify defaults as closely as possible.
        WriteIndented = false,
    };

    public static string EvaluateToJson(string csharpExpression)
    {
        var value = CSharpScript.EvaluateAsync<object?>(csharpExpression, Options).GetAwaiter().GetResult();
        return JsonSerializer.Serialize(value, JsonOptions);
    }
}
