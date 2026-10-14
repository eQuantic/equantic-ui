using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Native.Generators;

/// <summary>
/// The name of the method a call to a builder invokes, read from syntax alone: the cheap first look
/// every declaration family takes before it asks the semantic model.
/// <para>
/// A call has two shapes, <c>builder.Bundle.UrlScheme(…)</c> and <c>builder.Bundle?.UrlScheme(…)</c>,
/// and the second is a member BINDING, not a member access. A family that looked only for the first
/// let the second widen what the app does at run time while declaring nothing at build time, which
/// on a phone is a link that opens and a manifest that says nothing opens it.
/// </para>
/// </summary>
internal static class BuilderCall
{
    /// <summary>The invoked method's name, or null for a node that is not a call to a member.</summary>
    internal static string? MethodName(SyntaxNode node) => node switch
    {
        InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } => access.Name.Identifier.ValueText,
        InvocationExpressionSyntax { Expression: MemberBindingExpressionSyntax binding } => binding.Name.Identifier.ValueText,
        _ => null,
    };
}
