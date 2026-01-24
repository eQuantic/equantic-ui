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
        return _semanticModel?.GetSymbolInfo(node).Symbol;
    }

    public ITypeSymbol? GetType(SyntaxNode node)
    {
        return _semanticModel?.GetTypeInfo(node).Type;
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
