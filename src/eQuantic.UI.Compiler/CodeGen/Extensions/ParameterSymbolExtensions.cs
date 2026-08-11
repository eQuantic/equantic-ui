using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// What kind of parameter a symbol IS, for the one distinction the emitter keeps tripping over:
/// a C# 12 primary-constructor parameter is not a local.
/// <para>
/// Roslyn models both as <see cref="IParameterSymbol"/>, so "is it a parameter?" answers yes for a
/// capture that behaves like an instance field. Every place that asks the question has to ask this
/// one instead — a bare emission of a primary-constructor parameter compiles, renders, and then
/// throws a ReferenceError whenever the code finally runs, which is usually somewhere else.
/// </para>
/// </summary>
public static class ParameterSymbolExtensions
{
    /// <summary>
    /// True for a C# 12 primary-constructor parameter — captured state, reached through
    /// <c>this</c>, not a local in scope.
    /// <para>
    /// The primary constructor is the one whose DECLARING SYNTAX is the type declaration itself,
    /// which is what tells it apart from an ordinary constructor (whose parameters really are
    /// locals, inside their own body).
    /// </para>
    /// </summary>
    public static bool IsPrimaryConstructorParameter(this ISymbol? symbol) =>
        symbol is IParameterSymbol
        {
            ContainingSymbol: IMethodSymbol { MethodKind: MethodKind.Constructor } constructor,
        }
        && constructor.DeclaringSyntaxReferences.Any(
            reference => reference.GetSyntax() is TypeDeclarationSyntax);

    /// <summary>A parameter or local that really IS in scope — a plain callable, emitted verbatim.</summary>
    public static bool IsInScopeBinding(this ISymbol? symbol) =>
        symbol is IParameterSymbol or ILocalSymbol && !symbol.IsPrimaryConstructorParameter();
}
