using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// Helper for deep semantic analysis of Roslyn symbols.
/// Replaces brittle string comparisons with symbol-based checks.
/// </summary>
public class SemanticHelper
{
    private readonly SemanticModel? _semanticModel;

    public SemanticHelper(SemanticModel? semanticModel)
    {
        _semanticModel = semanticModel;
    }

    /// <summary>
    /// Checks if a symbol belongs to the System namespace (or sub-namespace).
    /// </summary>
    public bool IsSystemType(ISymbol? symbol)
    {
        return IsNamespace(symbol, "System");
    }

    /// <summary>
    /// Checks if a symbol belongs to the Microsoft namespace.
    /// </summary>
    public bool IsMicrosoftType(ISymbol? symbol)
    {
        return IsNamespace(symbol, "Microsoft");
    }

    /// <summary>
    /// Checks if a symbol belongs to the eQuantic.UI namespace.
    /// </summary>
    public bool IsEquanticType(ISymbol? symbol)
    {
        return IsNamespace(symbol, "eQuantic.UI");
    }

    /// <summary>
    /// Checks if the symbol represents System.Console.
    /// </summary>
    public bool IsSystemConsole(ISymbol? symbol)
    {
        if (symbol == null) return false;
        return symbol.Name == "Console" && IsSystemType(symbol);
    }
    
    /// <summary>
    /// Checks if the symbol is part of System.Linq
    /// </summary>
    public bool IsLinqExtension(ISymbol? symbol)
    {
        if (symbol == null) return false;
        return IsNamespace(symbol, "System.Linq");
    }

    public ISymbol? GetSymbol(SyntaxNode node)
    {
        node = Original(node);
        return Knows(node) ? _semanticModel!.GetSymbolInfo(node).Symbol : null;
    }

    public ITypeSymbol? GetType(SyntaxNode node)
    {
        if (_typeOverrides.TryGetValue(node, out var overridden)) return overridden;
        node = Original(node);
        return Knows(node) ? _semanticModel!.GetTypeInfo(node).Type : null;
    }

    /// <summary>The type a node is CONVERTED to (a collection expression takes its shape from its
    /// target), Original-aware and guarded.</summary>
    public ITypeSymbol? GetConvertedType(SyntaxNode node)
    {
        node = Original(node);
        return Knows(node) ? _semanticModel!.GetTypeInfo(node).ConvertedType : null;
    }

    /// <summary>The symbol a node DECLARES (a lambda parameter, a local), Original-aware and
    /// guarded — the model throws for a node outside its tree.</summary>
    public ISymbol? GetDeclaredSymbol(SyntaxNode node)
    {
        node = Original(node);
        return Knows(node) ? _semanticModel!.GetDeclaredSymbol(node) : null;
    }

    /// <summary>Whether the model can answer for this node — directly, or through its in-tree
    /// original. This is what "in-tree" means once rewriting is in play: a rewritten copy of an
    /// in-tree node is as known as the node it copies.</summary>
    public bool KnowsOrMapped(SyntaxNode node) => Knows(Original(node));

    /// <summary>A TYPE answer for a node that has no original — the `$r` receiver placeholder the
    /// null-conditional path introduces stands for a value whose type the model knows.</summary>
    public void MapType(SyntaxNode synthetic, ITypeSymbol? type)
    {
        if (type is not null) _typeOverrides[synthetic] = type;
    }

    private readonly Dictionary<SyntaxNode, ITypeSymbol> _typeOverrides = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// The in-tree node a SYNTHETIC node stands for, or the node itself. A strategy that rewrites
    /// syntax (the null-conditional path turns <c>a?.M(x)</c> into <c>$r.M(x)</c> so every other
    /// strategy can translate it) registers the correspondence here, and the model keeps
    /// answering for the rewritten nodes — symbols, types, the lambda parameters inside their
    /// arguments — instead of falling back to name heuristics.
    /// </summary>
    public SyntaxNode Original(SyntaxNode node) =>
        _originals.TryGetValue(node, out var original) ? original : node;

    private readonly Dictionary<SyntaxNode, SyntaxNode> _originals = new(ReferenceEqualityComparer.Instance);

    /// <summary>Registers a rewritten node as standing for an in-tree one.</summary>
    public void MapSynthetic(SyntaxNode synthetic, SyntaxNode original) => _originals[synthetic] = original;

    /// <summary>Drops the synthetic correspondences of the previous emission.</summary>
    public void ClearSynthetics()
    {
        _originals.Clear();
        _typeOverrides.Clear();
    }

    /// <summary>
    /// Whether the model can be ASKED about this node DIRECTLY. Roslyn throws for a node from
    /// another tree, so a rewritten node is never handed to the model as-is — the symbol and type
    /// accessors above translate it to its in-tree original first (see <see cref="Original"/>);
    /// a rewritten node with no original is honestly "don't know", which every strategy handles.
    /// </summary>
    public bool Knows(SyntaxNode node) =>
        _semanticModel is not null && ReferenceEquals(node.SyntaxTree, _semanticModel.SyntaxTree);

    /// <summary>
    /// The compile-time constant value of an expression (an enum member, a literal, a <c>const</c>), or
    /// <c>false</c> when it isn't a constant. Used to constant-fold enum↔int casts so the common case emits
    /// a literal instead of a runtime lookup.
    /// </summary>
    public bool TryGetConstantValue(SyntaxNode node, out object? value)
    {
        value = null;
        node = Original(node);
        if (!Knows(node)) return false;
        var constant = _semanticModel!.GetConstantValue(node);
        if (!constant.HasValue) return false;
        value = constant.Value;
        return value != null;
    }

    // The LINQ name-gate moved to ConversionContext.IsLinqMethod: whether a NAME may decide is a
    // policy question (CanGuess — authoritative model or not), and policy lives on the context.

    public bool IsStatic(SyntaxNode node)
    {
        var symbol = GetSymbol(node);
        return symbol?.IsStatic ?? false;
    }

    private bool IsNamespace(ISymbol? symbol, string namespaceStart)
    {
        if (symbol == null) return false;

        var containingNamespace = symbol.ContainingNamespace;
        if (containingNamespace == null) return false;

        var fullNamespace = containingNamespace.ToDisplayString();
        if (fullNamespace.StartsWith("global::")) fullNamespace = fullNamespace.Substring(8);
        
        // Exact match or starts with namespace. (e.g. System or System.Collections)
        return fullNamespace == namespaceStart || fullNamespace.StartsWith(namespaceStart + ".");
    }
}
