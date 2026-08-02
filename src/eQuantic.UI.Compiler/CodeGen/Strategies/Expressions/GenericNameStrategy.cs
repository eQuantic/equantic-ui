using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// A GENERIC TYPE NAME standing where an expression goes — <c>Bucket&lt;string&gt;.Of(…)</c>,
/// <c>Comparer&lt;int&gt;.Default</c>. Type arguments are erased at runtime, and leaving them in is
/// not merely redundant: <c>Bucket&lt;string&gt;.of()</c> parses as a comparison in JavaScript.
/// <para>
/// The same erasure the object-creation path does for <c>new Bucket&lt;string&gt;()</c>, on the
/// other place a type name can appear.
/// </para>
/// </summary>
public class GenericNameStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context) =>
        node is GenericNameSyntax;

    public string Convert(SyntaxNode node, ConversionContext context) =>
        ((GenericNameSyntax)node).Identifier.Text;

    public int Priority => 5;
}
