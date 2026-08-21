using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// LINQ QUERY syntax — <c>from x in xs where … orderby … select …</c> — lowered to the
/// method-syntax chain the C# compiler itself would build, and handed to the EXISTING operator
/// strategies, so both syntaxes are the same translation by construction: <c>where</c> becomes
/// .filter, a whole <c>orderby</c> run becomes one composite stable sort, <c>group…by</c> takes
/// the GroupBy shape. The clause bodies are re-parented COPIES of the in-tree originals, mapped
/// back through the synthetic-node registry so the model keeps answering inside them (the range
/// variable stays a plain name — <see cref="SymbolKind.RangeVariable"/> is already in the
/// identifier allowlist), and each synthetic operator invocation carries the very symbol the
/// model bound to its clause. The degenerate final <c>select x</c> is elided exactly when the
/// model elides it; when the query would otherwise BE its source, an identity .map keeps C#'s
/// fresh-sequence guarantee.
/// <para>
/// Deliberately fenced (EQ2008): <c>join</c>, <c>let</c>, a second <c>from</c>, and <c>into</c>
/// continuations — their C# lowering runs through compiler-generated transparent identifiers —
/// plus the typed <c>from T x in …</c> (an implicit Cast). Method syntax covers all of them.
/// </para>
/// </summary>
public class QueryExpressionStrategy : IConversionStrategy
{
    public int Priority => 10;

    public bool CanConvert(SyntaxNode node, ConversionContext context) => node is QueryExpressionSyntax;

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var query = (QueryExpressionSyntax)node;

        if (Fence(query) is { } fenced)
        {
            context.Report(query, ConversionSeverity.Error, "EQ2008",
                $"query syntax with {fenced} is not lowered — its C# translation runs through "
                + "compiler-generated transparent identifiers. Rewrite the query in method "
                + "syntax, where every operator is supported.");
            return context.Converter.ConvertExpression(query.FromClause.Expression);
        }

        return new Lowering(context, query).Emit();
    }

    private static string? Fence(QueryExpressionSyntax query)
    {
        if (query.FromClause.Type is not null) return "a typed range variable (an implicit Cast)";
        if (query.Body.Continuation is not null) return "an 'into' continuation";
        foreach (var clause in query.Body.Clauses)
        {
            switch (clause)
            {
                case FromClauseSyntax: return "a second 'from' clause (SelectMany)";
                case LetClauseSyntax: return "a 'let' clause";
                case JoinClauseSyntax: return "a 'join' clause";
            }
        }
        return null;
    }

    /// <summary>
    /// One query's lowering: builds the synthetic chain clause by clause, then registers the two
    /// correspondences the rest of the pipeline lives on — every re-parented node to its in-tree
    /// original (so the model answers inside the lambda bodies), and every operator invocation to
    /// the symbol the model bound to its clause (so the operator strategies claim symbol-first,
    /// not by name).
    /// </summary>
    private sealed class Lowering
    {
        private const string OperatorAnnotation = "eq-query-operator";

        private readonly ConversionContext _context;
        private readonly QueryExpressionSyntax _query;
        private readonly string _rangeVariable;
        private readonly List<SyntaxNode> _originals = new();
        private readonly List<IMethodSymbol?> _symbols = new();
        private ExpressionSyntax _chain;
        private bool _bareSource = true;

        public Lowering(ConversionContext context, QueryExpressionSyntax query)
        {
            _context = context;
            _query = query;
            _rangeVariable = query.FromClause.Identifier.Text;
            _chain = Reparent(query.FromClause.Expression);
        }

        public string Emit()
        {
            foreach (var clause in _query.Body.Clauses)
            {
                switch (clause)
                {
                    case WhereClauseSyntax where:
                        Apply("Where", where.Condition, _context.SemanticHelper.QueryOperator(where));
                        break;
                    case OrderByClauseSyntax orderBy:
                        var first = true;
                        foreach (var ordering in orderBy.Orderings)
                        {
                            var symbol = _context.SemanticHelper.QueryOperator(ordering);
                            var descending =
                                ordering.AscendingOrDescendingKeyword.IsKind(SyntaxKind.DescendingKeyword);
                            var name = symbol?.Name
                                ?? (first ? "OrderBy" : "ThenBy") + (descending ? "Descending" : "");
                            Apply(name, ordering.Expression, symbol);
                            first = false;
                        }
                        break;
                }
            }

            switch (_query.Body.SelectOrGroup)
            {
                case SelectClauseSyntax select:
                {
                    var symbol = _context.SemanticHelper.QueryOperator(select);
                    // The model elides the degenerate `select x`; without a model the same shape
                    // decides. A query that would otherwise BE its source still projects — C#
                    // guarantees a fresh sequence.
                    if (symbol is not null || !IsIdentity(select.Expression) || _bareSource)
                        Apply("Select", select.Expression, symbol);
                    break;
                }
                case GroupClauseSyntax group:
                {
                    var symbol = _context.SemanticHelper.QueryOperator(group);
                    var arguments = IsIdentity(group.GroupExpression)
                        ? new[] { Lambda(group.ByExpression) }
                        : new[] { Lambda(group.ByExpression), Lambda(group.GroupExpression) };
                    Call("GroupBy", arguments, symbol);
                    break;
                }
            }

            Register();
            return _context.Converter.ConvertExpression(_chain);
        }

        private bool IsIdentity(ExpressionSyntax expression) =>
            expression is IdentifierNameSyntax identifier
            && identifier.Identifier.Text == _rangeVariable;

        private void Apply(string name, ExpressionSyntax body, IMethodSymbol? symbol) =>
            Call(name, new[] { Lambda(body) }, symbol);

        private void Call(string name, ExpressionSyntax[] arguments, IMethodSymbol? symbol)
        {
            var access = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                _chain, SyntaxFactory.IdentifierName(name));
            _chain = SyntaxFactory.InvocationExpression(access, SyntaxFactory.ArgumentList(
                    SyntaxFactory.SeparatedList(arguments.Select(SyntaxFactory.Argument))))
                .WithAdditionalAnnotations(new SyntaxAnnotation(OperatorAnnotation, _symbols.Count.ToString()));
            _symbols.Add(symbol);
            _bareSource = false;
        }

        private ExpressionSyntax Lambda(ExpressionSyntax body) =>
            SyntaxFactory.SimpleLambdaExpression(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier(_rangeVariable)), Reparent(body));

        /// <summary>A tracked copy of an in-tree fragment, its nodes queued for mapping once the
        /// chain is fully composed — the annotations ride the copies through every wrap.</summary>
        private ExpressionSyntax Reparent(ExpressionSyntax fragment)
        {
            var originals = fragment.DescendantNodesAndSelf().ToArray();
            _originals.AddRange(originals);
            return (ExpressionSyntax)fragment.TrackNodes(originals);
        }

        private void Register()
        {
            var helper = _context.SemanticHelper;
            foreach (var original in _originals)
            {
                if (_chain.GetCurrentNode(original) is { } current)
                    helper.MapSynthetic(current, original);
            }
            foreach (var invocation in _chain.GetAnnotatedNodes(OperatorAnnotation))
            {
                var index = int.Parse(invocation.GetAnnotations(OperatorAnnotation).First().Data!);
                if (_symbols[index] is { } symbol)
                {
                    helper.MapSymbol(invocation, symbol);
                    helper.MapType(invocation, symbol.ReturnType);
                }
            }
        }
    }
}
