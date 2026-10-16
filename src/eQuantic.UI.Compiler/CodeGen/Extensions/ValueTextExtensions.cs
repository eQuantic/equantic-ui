using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace eQuantic.UI.Compiler.CodeGen.Extensions;

/// <summary>
/// A piece of syntax as C# READS it rather than as it was spelled: every identifier is its value, so
/// <c>Store.@Box</c> is <c>Store.Box</c>, and a name written with a Unicode escape is the letters it
/// escapes (#704).
/// </summary>
internal static class ValueTextExtensions
{
    /// <summary>
    /// The node's text, as <see cref="SyntaxNode.ToString"/> writes it (the trivia between its tokens
    /// kept, its own leading and trailing trivia not), with each identifier token written as its
    /// <see cref="SyntaxToken.ValueText"/>. A name eqc takes from a <c>TypeSyntax</c> (a module, an
    /// import, an annotation, a type it compares by name) is this, never the spelling: a type declared
    /// <c>@Box</c> is the type <c>Box</c>, and its module and every reference say so.
    /// </summary>
    public static string ValueText(this SyntaxNode node)
    {
        var spelled = node.ToString();
        // Only a verbatim identifier or an escaped one is spelled apart from its value.
        if (spelled.IndexOf('@') < 0 && spelled.IndexOf('\\') < 0) return spelled;

        var text = new StringBuilder(spelled.Length);
        var first = node.GetFirstToken();
        var last = node.GetLastToken();
        foreach (var token in node.DescendantTokens())
        {
            if (token != first) text.Append(token.LeadingTrivia.ToFullString());
            text.Append(token.IsKind(SyntaxKind.IdentifierToken) ? token.ValueText : token.Text);
            if (token != last) text.Append(token.TrailingTrivia.ToFullString());
        }

        return text.ToString();
    }
}
