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
        return Knows(node) ? _semanticModel!.GetSymbolInfo(node).Symbol : null;
    }

    public ITypeSymbol? GetType(SyntaxNode node)
    {
        return Knows(node) ? _semanticModel!.GetTypeInfo(node).Type : null;
    }

    /// <summary>
    /// Whether the model can be ASKED about this node. Roslyn throws for a node from another tree,
    /// and a strategy handed a REWRITTEN expression (the null-conditional path rebuilds
    /// <c>a?.B(x)</c> as <c>a.B(x)</c> so the normal pipeline can translate it) would otherwise take
    /// the whole compiler down. "I don't know" is the honest answer, and every strategy already
    /// handles it.
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
        if (_semanticModel == null) return false;
        var constant = _semanticModel.GetConstantValue(node);
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
