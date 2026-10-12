using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace eQuantic.UI.Compiler.CodeGen.Extensions;

internal static class InvocationExpressionExtensions
{
    /// <summary>
    /// Whether the invocation is the <c>nameof</c> operator, which the compiler turns into text: what
    /// it is handed is never evaluated, so a name inside it is no name the code reaches when it runs.
    /// The MODEL says which one it is, because a method an app names <c>nameof</c> is a call like any
    /// other.
    /// </summary>
    internal static bool IsNameOf(this InvocationExpressionSyntax invocation, SemanticModel model) =>
        invocation.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" }
        && model.GetOperation(invocation) is INameOfOperation;
}
