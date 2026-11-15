using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// Whether a save changed a file inside its bodies alone (#1281). C# binds another file's code against
/// this file's declarations and never against its bodies, so after such a save every other file binds
/// as it did, and its module changes only if its compile read this file's syntax, which the compile
/// noted (<see cref="SourceReads"/>). Anything else (a declaration, a signature, an attribute, a
/// constant, an initializer, a using, the trivia between members) may change what another file binds
/// to, and the build compiles every file.
/// <para>
/// A body is the block or the expression body of a method, a constructor, a destructor, an operator, a
/// conversion and an accessor, and the expression body of a property and an indexer. A field's or a
/// property's initializer is not one: it may be a constant another file inlines.
/// </para>
/// </summary>
internal static class BodyOnlyEdit
{
    /// <summary>Whether <paramref name="before"/> and <paramref name="after"/> differ inside bodies alone.</summary>
    internal static bool Is(SyntaxTree before, SyntaxTree after) => OutsideBodies(before) == OutsideBodies(after);

    /// <summary>The file's text with each body taken out and a mark in its place.</summary>
    private static string OutsideBodies(SyntaxTree tree)
    {
        var root = tree.GetRoot();
        var text = root.ToFullString();
        var outside = new StringBuilder(text.Length);
        var at = 0;
        foreach (var body in root.DescendantNodes(node => !IsBody(node)).Where(IsBody))
        {
            outside.Append(text, at, body.SpanStart - at).Append('\u0001');
            at = body.Span.End;
        }
        return outside.Append(text, at, text.Length - at).ToString();
    }

    private static bool IsBody(SyntaxNode node) => node switch
    {
        BlockSyntax block => block.Parent is BaseMethodDeclarationSyntax or AccessorDeclarationSyntax,
        ArrowExpressionClauseSyntax arrow => arrow.Parent is BaseMethodDeclarationSyntax or AccessorDeclarationSyntax
            or PropertyDeclarationSyntax or IndexerDeclarationSyntax,
        _ => false,
    };
}
