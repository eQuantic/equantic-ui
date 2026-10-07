using System.Globalization;
using System.Text.Encodings.Web;
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
    // Preview, to match both eqc and the transpiler side of the harness. The .NET side of a
    // conformance case has to ACCEPT everything the JS side is asked to translate, or the
    // comparison is between a compiler error and a value.
    private static readonly ScriptOptions Options = ScriptOptions.Default
        .WithLanguageVersion(Microsoft.CodeAnalysis.CSharp.LanguageVersion.Preview)
        .AddReferences(
            typeof(object).Assembly,
            typeof(System.Linq.Enumerable).Assembly,
            typeof(System.Collections.Generic.List<>).Assembly,
            typeof(System.Collections.Generic.Stack<>).Assembly,
            typeof(System.DateOnly).Assembly,
            // The vocabulary a component is written in, so a case can hand the .NET side the SAME
            // component source the emitter compiles (ExpressionVariableEmissionTests). A reference
            // brings no name into scope: a case that wants it says `using eQuantic.UI.Primitives;`.
            typeof(eQuantic.UI.Primitives.VisualNode).Assembly)
        // System.Threading.Tasks so an async case can NAME a Task: without it the .NET side fails
        // to compile while the JS side runs fine, which compares a compiler error to a value.
        .AddImports("System", "System.Linq", "System.Collections.Generic", "System.Text",
            "System.Threading.Tasks");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        // Match JSON.stringify of the transpiled output: object property names are camelCased
        // by the transpiler, so serialize .NET objects camelCase too for a fair comparison.
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // JS JSON.stringify does NOT HTML-escape; the default System.Text.Json encoder escapes
        // <, >, &, +, ' etc. as \uXXXX. Use the relaxed encoder so string results compare faithfully.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    static DotNetEvaluator()
    {
        // A value is written as JSON.stringify writes the runtime's representation of it, where
        // System.Text.Json's own JSON differs (a long as a BigInt's text, a decimal's text, an enum's
        // twin name, a double as JavaScript writes it, a tuple and a pair as arrays): RuntimeJson. The
        // culture a case runs in is its own (EvaluateToJson), never set here for the whole process.
        foreach (var converter in RuntimeJson.Converters) JsonOptions.Converters.Add(converter);
    }

    /// <summary>
    /// The JSON as <c>JSON.stringify</c> spells it, which escapes only what JSON requires: a quote, a
    /// backslash, a control character and a lone surrogate. Even the relaxed encoder escapes more —
    /// a no-break space, which is sv-SE's group separator, came out as <c>\u00A0</c> on this side and
    /// as the character on the other, and two spellings of one string compared unequal. A code unit it
    /// escaped that JSON.stringify writes as it is, is written as it is.
    /// </summary>
    private static string AsJavaScriptWritesIt(string json)
    {
        if (!json.Contains("\\u", StringComparison.Ordinal)) return json;
        var written = new System.Text.StringBuilder(json.Length);
        for (var i = 0; i < json.Length; i++)
        {
            var c = json[i];
            if (c != '\\' || i + 1 >= json.Length)
            {
                written.Append(c);
                continue;
            }
            if (json[i + 1] == 'u' && i + 5 < json.Length
                && int.TryParse(json.AsSpan(i + 2, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var unit)
                && unit >= 0x20 && unit != '"' && unit != '\\' && !char.IsSurrogate((char)unit))
            {
                written.Append((char)unit);
                i += 5;
                continue;
            }
            // Any other escape is the same on both sides, and its second character is not a new one.
            written.Append(c).Append(json[i + 1]);
            i++;
        }
        return written.ToString();
    }

    /// <summary>
    /// Evaluates the C# in the culture NAMED, the invariant one unless a case names another, never in
    /// whatever culture the host or the thread happens to be in: a case runs on the browser side with
    /// that culture installed, or with none, which is the invariant culture there too (#471). It was
    /// set once, in a static constructor, on whichever thread first touched this type.
    /// </summary>
    public static string EvaluateToJson(string csharpExpression, string prelude = "", CultureInfo? culture = null)
    {
        // A prelude (type declarations such as enums/records) runs before the trailing expression;
        // CSharpScript returns the value of that final expression.
        var script = string.IsNullOrWhiteSpace(prelude) ? csharpExpression : $"{prelude}\n{csharpExpression}";
        var previous = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = culture ?? CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = culture ?? CultureInfo.InvariantCulture;
        try
        {
            // The culture flows into the script's awaits with the execution context.
            var value = CSharpScript.EvaluateAsync<object?>(script, Options).GetAwaiter().GetResult();
            return ToJson(value);
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = previous;
        }
    }

    /// <summary>A .NET value as the canonical JSON a case's answers are compared in.</summary>
    public static string ToJson(object? value) => AsJavaScriptWritesIt(JsonSerializer.Serialize(value, JsonOptions));
}
