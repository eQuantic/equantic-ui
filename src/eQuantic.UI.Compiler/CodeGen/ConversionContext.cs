using Microsoft.CodeAnalysis;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// Context shared between conversion strategies.
/// </summary>
public class ConversionContext
{
    public SemanticModel? SemanticModel { get; set; }
    public required CSharpToJsConverter Converter { get; set; }
    public required SemanticHelper SemanticHelper { get; set; }
    public string? ExpectedType { get; set; }
    public string? CurrentClassName { get; set; }
    public HashSet<string> UsedHelpers { get; } = new();

    /// <summary>APP types the conversion itself introduced into the OUTPUT — names that never
    /// appear in the source syntax (an extension call reduced to `NodeExtensions.also(...)`), so
    /// the syntax-walking import collector cannot see them. The emitter unions these into the
    /// per-module import set.</summary>
    public HashSet<string> UsedAppTypes { get; } = new();

    /// <summary>Diagnostics raised during this conversion (unconverted or impossible constructs).</summary>
    public List<ConversionDiagnostic> Diagnostics { get; } = new();

    /// <summary>Record a diagnostic anchored at <paramref name="node"/>'s source position.</summary>
    public void Report(SyntaxNode node, ConversionSeverity severity, string code, string message)
    {
        var pos = node.GetLocation().GetLineSpan().StartLinePosition;
        Diagnostics.Add(new ConversionDiagnostic(severity, code, message, pos.Line + 1, pos.Character + 1));
    }

    public void ClearDiagnostics() => Diagnostics.Clear();

    // Cache to avoid reprocessing the same node multiple times
    private readonly Dictionary<SyntaxNode, string> _cache = new();

    public string? GetCached(SyntaxNode node)
    {
        return _cache.TryGetValue(node, out var result) ? result : null;
    }

    public void SetCached(SyntaxNode node, string result)
    {
        _cache[node] = result;
    }
}
