using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Explicit <c>this</c> — bare as an argument (<c>Configure(this)</c>) or as a written receiver
/// (<c>this.Count</c>). Nothing claimed the node: the IMPLICIT form never produces it (a bare
/// member identifier resolves to <c>this.x</c> through IdentifierStrategy), so perfectly ordinary
/// explicit-<c>this</c> C# fell through to EQ1001. Surfaced by the syntax-surface enumeration,
/// not by a user.
/// </summary>
public class ThisExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context) => node is ThisExpressionSyntax;

    public string Convert(SyntaxNode node, ConversionContext context) => "this";

    public int Priority => 10;
}
