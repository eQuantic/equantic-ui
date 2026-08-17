using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Design;

/// <summary>
/// The native twin of the JS emitter's design mode: every expression that CONSTRUCTS a node is
/// rewritten to stamp the C# span that built it, so a rendered Photon frame can answer "what is at
/// this pixel" with a file and a selection — the same identity the web canvas runs on.
/// <para>
/// C#-to-C#, applied to the compilation the frame is emitted from and never to anything a user
/// ships: <c>new Box(…)</c> becomes <c>VisualNode.DesignOrigin.Stamp(new Box(…), "path|s:c|e:c")</c>.
/// Invocations whose return type is a node are wrapped too — a helper method's call site is the
/// honest answer for a subtree built elsewhere — but the stamp helper keeps the FIRST origin, so a
/// construction's own exact span always beats the call that merely returned it.
/// </para>
/// <para>
/// Spans are taken from the ORIGINAL nodes before any rewriting moves them, and the rewritten call
/// is parsed from text: positions inside the new tree mean nothing and are never read.
/// </para>
/// </summary>
internal sealed class OriginRewriter : CSharpSyntaxRewriter
{
    private readonly SemanticModel _model;
    private readonly string _path;

    private OriginRewriter(SemanticModel model, string path)
    {
        _model = model;
        _path = path;
    }

    /// <summary>The tree with every node construction stamped, or the tree itself when it has none.</summary>
    public static SyntaxNode Rewrite(SyntaxTree tree, SemanticModel model)
    {
        var rewriter = new OriginRewriter(model, tree.FilePath);
        return rewriter.Visit(tree.GetRoot());
    }

    public override SyntaxNode? VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
    {
        var visited = base.VisitObjectCreationExpression(node);
        return visited is ExpressionSyntax expression && IsNode(_model.GetTypeInfo(node).Type)
            ? Stamp(expression, node)
            : visited;
    }

    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        var visited = base.VisitInvocationExpression(node);
        return visited is ExpressionSyntax expression
               && _model.GetSymbolInfo(node).Symbol is IMethodSymbol method
               && IsNode(method.ReturnType)
            ? Stamp(expression, node)
            : visited;
    }

    /// <summary>By name up the base chain, the same answer <c>RenderNative</c> gives for "is this a
    /// component" — symbol identity would be stricter and wrong across the ref/impl divide.</summary>
    private static bool IsNode(ITypeSymbol? type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.Name == "VisualNode") return true;
        }
        return false;
    }

    private ExpressionSyntax Stamp(ExpressionSyntax rewritten, SyntaxNode original)
    {
        var span = original.GetLocation().GetLineSpan();
        var origin = $"{_path}|{span.StartLinePosition.Line}:{span.StartLinePosition.Character}"
                     + $"|{span.EndLinePosition.Line}:{span.EndLinePosition.Character}";

        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.ParseExpression("global::eQuantic.UI.Primitives.VisualNode.DesignOrigin.Stamp"),
            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]
            {
                SyntaxFactory.Argument(rewritten),
                SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                    SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(origin))),
            })));
    }
}
