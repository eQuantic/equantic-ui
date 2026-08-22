using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
        if (_symbolOverrides.TryGetValue(node, out var overridden)) return overridden;
        node = Original(node);
        if (!Knows(node)) return null;
        if (_symbols.TryGetValue(node, out var remembered)) return remembered;
        var symbol = _semanticModel!.GetSymbolInfo(node).Symbol;
        _symbols[node] = symbol;
        return symbol;
    }

    public ITypeSymbol? GetType(SyntaxNode node)
    {
        if (_typeOverrides.TryGetValue(node, out var overridden)) return overridden;
        node = Original(node);
        if (!Knows(node)) return null;
        if (_types.TryGetValue(node, out var remembered)) return remembered;
        var type = _semanticModel!.GetTypeInfo(node).Type;
        _types[node] = type;
        return type;
    }

    /// <summary>
    /// What the model already answered, for THIS model. Dispatching one node runs the gate of
    /// every candidate strategy, and the ones that share its shape each ask the same question
    /// about the same node — six model queries per distinct node, measured, five of them repeats.
    /// The answer cannot change: a helper is rebuilt whenever the model is (SetSemanticModel), the
    /// overrides are consulted BEFORE this and still win, and the key is the node the model is
    /// actually asked about — the in-tree original, after any synthetic mapping. Only nodes the
    /// model CAN answer for are remembered: a node it cannot is always null, and holding it here
    /// would keep a synthetic alive past the ClearSynthetics that exists to drop it.
    /// </summary>
    private readonly Dictionary<SyntaxNode, ISymbol?> _symbols = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<SyntaxNode, ITypeSymbol?> _types = new(ReferenceEqualityComparer.Instance);

    /// <summary>The type a node is CONVERTED to (a collection expression takes its shape from its
    /// target), Original-aware and guarded.</summary>
    public ITypeSymbol? GetConvertedType(SyntaxNode node)
    {
        node = Original(node);
        return Knows(node) ? _semanticModel!.GetTypeInfo(node).ConvertedType : null;
    }

    /// <summary>
    /// The BOUND operation for a node — Roslyn's IOperation, where what the syntax does not spell
    /// out lives: whether an arithmetic is <c>checked</c>, which conversions were applied, what an
    /// operator resolved to. Original-aware and guarded like every other accessor; null where the
    /// model cannot answer, which every caller treats as "the syntax decides".
    /// </summary>
    public IOperation? GetOperation(SyntaxNode node)
    {
        node = Original(node);
        return Knows(node) ? _semanticModel!.GetOperation(node) : null;
    }

    /// <summary>What a <c>foreach</c> binds to — the element type, the conversion from the element
    /// to the loop variable, the enumerator — Original-aware and guarded.</summary>
    public ForEachStatementInfo? ForEachInfo(CommonForEachStatementSyntax statement)
    {
        var node = Original(statement);
        return Knows(node) && node is CommonForEachStatementSyntax original
            ? _semanticModel!.GetForEachStatementInfo(original)
            : null;
    }

    /// <summary>The symbol a node DECLARES (a lambda parameter, a local), Original-aware and
    /// guarded — the model throws for a node outside its tree.</summary>
    public ISymbol? GetDeclaredSymbol(SyntaxNode node)
    {
        node = Original(node);
        return Knows(node) ? _semanticModel!.GetDeclaredSymbol(node) : null;
    }

    /// <summary>The LINQ operator the model bound to a query clause — <c>where</c> answers Where,
    /// each <c>orderby</c> ordering answers OrderBy/OrderByDescending/ThenBy/ThenByDescending,
    /// <c>select</c>/<c>group…by</c> answer Select/GroupBy. Null where the model has no answer —
    /// including the DEGENERATE final <c>select x</c> after other clauses, which C# elides, so a
    /// null here is a lowering decision, not just ignorance. Original-aware: the clauses of a
    /// query nested inside another rewrite still answer.</summary>
    public IMethodSymbol? QueryOperator(SyntaxNode clauseOrOrdering)
    {
        clauseOrOrdering = Original(clauseOrOrdering);
        if (!Knows(clauseOrOrdering)) return null;
        return clauseOrOrdering switch
        {
            OrderingSyntax ordering => _semanticModel!.GetSymbolInfo(ordering).Symbol as IMethodSymbol,
            SelectOrGroupClauseSyntax selectOrGroup =>
                _semanticModel!.GetSymbolInfo(selectOrGroup).Symbol as IMethodSymbol,
            QueryClauseSyntax clause =>
                _semanticModel!.GetQueryClauseInfo(clause).OperationInfo.Symbol as IMethodSymbol,
            _ => null,
        };
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

    /// <summary>A SYMBOL answer for a synthetic node standing in for a binding the model made
    /// SOMEWHERE ELSE — the query-syntax lowering builds <c>xs.Where(x => …)</c> invocations whose
    /// operator the model bound to the query CLAUSE, not to any invocation node.</summary>
    public void MapSymbol(SyntaxNode synthetic, ISymbol? symbol)
    {
        if (symbol is not null) _symbolOverrides[synthetic] = symbol;
    }

    private readonly Dictionary<SyntaxNode, ISymbol> _symbolOverrides = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// The in-tree node a SYNTHETIC node stands for, or the node itself. A strategy that rewrites
    /// syntax (the null-conditional path turns <c>a?.M(x)</c> into <c>$r.M(x)</c> so every other
    /// strategy can translate it) registers the correspondence here, and the model keeps
    /// answering for the rewritten nodes — symbols, types, the lambda parameters inside their
    /// arguments — instead of falling back to name heuristics. Chased to a fixpoint: a rewrite OF
    /// a rewrite (a query nested in a query's source, a <c>?.</c> inside a <c>?.</c>) still lands
    /// on the in-tree node.
    /// </summary>
    public SyntaxNode Original(SyntaxNode node)
    {
        while (_originals.TryGetValue(node, out var original)) node = original;
        return node;
    }

    private readonly Dictionary<SyntaxNode, SyntaxNode> _originals = new(ReferenceEqualityComparer.Instance);

    /// <summary>Registers a rewritten node as standing for an in-tree one.</summary>
    public void MapSynthetic(SyntaxNode synthetic, SyntaxNode original)
    {
        if (!ReferenceEquals(synthetic, original)) _originals[synthetic] = original;
    }

    /// <summary>Drops the synthetic correspondences of the previous emission.</summary>
    public void ClearSynthetics()
    {
        _originals.Clear();
        _typeOverrides.Clear();
        _symbolOverrides.Clear();
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
