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

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var name = (GenericNameSyntax)node;
        // A generic LOCAL FUNCTION named with its type arguments, `Func<int, int> f = Id<int>`, is a
        // method group of a function in scope and not a type: it takes the name its declaration took
        // (LocalFunctionName), where the source text left `Id` beside a declared `id`.
        var symbol = context.SemanticHelper.GetSymbol(name);
        if (symbol is IMethodSymbol { MethodKind: MethodKind.LocalFunction } localFunction)
            return LocalFunctionName.Of(localFunction);
        // With no model, the same function found by the syntax. Not as a receiver: `Bucket<string>.Of`
        // names a type.
        var isReceiver = name.Parent is MemberAccessExpressionSyntax access && access.Expression == name;
        if (symbol is null && !isReceiver && LocalFunctionName.InScope(name, name.Identifier.ValueText) is { } local)
            return LocalFunctionName.Of(local, context);
        return name.Identifier.Text;
    }

    public int Priority => 5;
}
