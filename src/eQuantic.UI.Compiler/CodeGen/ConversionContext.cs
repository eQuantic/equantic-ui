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
