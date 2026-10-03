using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>Where a simple name stands in the expression around it.</summary>
internal static class SimpleNameExtensions
{
    /// <summary>
    /// Whether the name is read on its own, as <c>using static</c> brings a member in (<c>NaN</c>,
    /// <c>Join(…)</c>), rather than as the member part of <c>a.b</c>, <c>a?.b</c> or a qualified name,
    /// which the expression around it converts.
    /// </summary>
    internal static bool StandsAlone(this SimpleNameSyntax name) => name.Parent switch
    {
        MemberAccessExpressionSyntax access => access.Name != name,
        MemberBindingExpressionSyntax binding => binding.Name != name,
        QualifiedNameSyntax qualified => qualified.Right != name,
        _ => true,
    };
}
