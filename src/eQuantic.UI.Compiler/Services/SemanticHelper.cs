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

    /// <summary>
    /// True for .NET value-shaped data that the transpiler models as plain objects/arrays and that
    /// compares by VALUE: records (class or struct), user structs, and value tuples. Excludes
    /// <c>Nullable&lt;T&gt;</c> (handled separately), primitives/string/decimal (their
    /// <see cref="SpecialType"/> is set), and the compat structs that carry their own equality
    /// (DateTime, TimeSpan, DateOnly, TimeOnly, DateTimeOffset, Guid).
    /// </summary>
    public static bool IsStructuralValueType(ITypeSymbol? type)
    {
        if (type == null) return false;
        if (type is INamedTypeSymbol n && n.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T)
            return false;
        if (type.SpecialType != SpecialType.None) return false; // int/string/decimal/bool/…
        if (type.IsTupleType) return true;
        if (type.IsRecord) return true;
        if (type.TypeKind == TypeKind.Struct)
        {
            return type.ToDisplayString() switch
            {
                "System.DateTime" or "System.TimeSpan" or "System.DateOnly" or "System.TimeOnly"
                    or "System.DateTimeOffset" or "System.Guid" => false,
                _ => true,
            };
        }
        return false;
    }

    public bool IsLinqMethod(SyntaxNode node, string methodName)
    {
        if (node is not Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax invocation) return false;
        if (invocation.Expression is not Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax memberAccess) return false;
        if (memberAccess.Name.Identifier.Text != methodName) return false;

        var symbol = GetSymbol(node); // Invocation symbol (MethodSymbol)
        if (symbol == null) return true; // Loose check if semantic model missing
        
        return IsLinqExtension(symbol.ContainingType);
    }

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
