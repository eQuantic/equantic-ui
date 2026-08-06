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

    /// <summary>
    /// Set while converting an ITERATOR method's body. A C# iterator yields a sequence, and every
    /// sequence in the emitted world is an ARRAY — so the method fills this buffer and returns it,
    /// instead of becoming a JS generator whose result `.length` reads as undefined the moment any
    /// LINQ operator touches it. Laziness is the trade, and it only matters for infinite sequences.
    /// </summary>
    public string? IteratorBuffer { get; set; }
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

    /// <summary>
    /// Set by a strategy whose translation already NAMES its receiver and already answers for a null
    /// one — a helper call rather than a member on the value. A <c>?.</c> in front of that would be
    /// nonsense, so the conditional access steps aside and takes the call as the whole chain.
    /// </summary>
    public bool NullGuardAnswered { get; set; }

    /// <summary>
    /// Whether the output is TYPESCRIPT. Off by default, because the same converter also produces
    /// plain `.mjs` — the conformance harness EXECUTES the emission, and `x: number` is a parse
    /// error there rather than a type. The module emitters turn it on; nothing else may.
    /// </summary>
    public bool TypeAnnotations { get; set; }
}
