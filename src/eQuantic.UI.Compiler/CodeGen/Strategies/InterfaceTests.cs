using Microsoft.CodeAnalysis;
using eQuantic.UI.Compiler.CodeGen.Ir;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// A type test against an INTERFACE (<c>x is IFoo</c>, <c>x as IFoo</c>, a type pattern <c>IFoo f</c>), as .NET
/// answers it off the type: whether the value's class implements the interface. JavaScript has no interfaces,
/// so the twin of every class, struct and component this compiler writes declares the interfaces a test can ask
/// for in <c>static $interfaces</c> (<see cref="DeclaredBy"/>), those of its bases included, and the test reads
/// it (<see cref="Eq.TypesImplements"/>). The test was <c>x != null</c>, true for any value: the element engine's
/// <c>(state as IDisposable)?.Dispose()</c> called a <c>dispose</c> no state had, and threw (#1464).
/// <para>
/// A test the browser cannot answer is refused (EQ2015) rather than guessed: an interface with type arguments,
/// which JavaScript erases, so <c>IFoo&lt;int&gt;</c> could not be told from <c>IFoo&lt;string&gt;</c>; and an
/// interface that values the browser holds as its own (a string, a number, an array) or classes the runtime
/// writes by hand implement, since none of them could say so: every interface of the platform but
/// <c>IDisposable</c>, and the visual vocabulary's. An interface of the app's own, or of an assembly the runtime
/// carries transpiled whole, is implemented only by classes this compiler writes, and <c>IDisposable</c> only by
/// classes, so a test of either reads what the class declares.
/// </para>
/// </summary>
internal static class InterfaceTests
{
    /// <summary>
    /// The array a twin's <c>static $interfaces</c> holds: the name .NET gives each interface of
    /// <paramref name="type"/> a test can ask for, its bases' included, or null when it has none.
    /// </summary>
    public static JsExpr? DeclaredBy(INamedTypeSymbol type)
    {
        var names = type.AllInterfaces.Where(Testable).Select(ExceptionTypes.NameOf).Distinct().OrderBy(name => name, StringComparer.Ordinal).ToList();
        return names.Count == 0 ? null : JsExpr.Array(names.Select(name => JsExpr.Literal(JsStringLiteral.Quote(name))).ToList());
    }

    /// <summary>
    /// The test that the value at <paramref name="access"/> implements <paramref name="contract"/>, or
    /// <c>false</c> once a test the browser cannot answer is refused (EQ2015) at <paramref name="at"/>.
    /// </summary>
    public static string Test(INamedTypeSymbol contract, string access, SyntaxNode at, ConversionContext context)
    {
        if (Refusal(contract) is { } reason)
        {
            context.Report(at, ConversionSeverity.Error, "EQ2015",
                $"A type test against the interface '{contract.ToDisplayString()}' has no JavaScript translation: {reason}. "
                + "Test for the class the value is, or for an interface of the app's own that the class implements.");
            return "false";
        }
        context.UsedHelpers.Add(Eq.Import);
        return $"{Eq.TypesImplements}({access}, {JsStringLiteral.Quote(ExceptionTypes.NameOf(contract))})";
    }

    /// <summary>Whether a test against <paramref name="contract"/> reads what a class declares.</summary>
    private static bool Testable(INamedTypeSymbol contract) => Refusal(contract) is null;

    /// <summary>Why a test against <paramref name="contract"/> cannot be answered, or null when it can.</summary>
    private static string? Refusal(INamedTypeSymbol contract)
    {
        if (contract.Arity > 0 || contract.ContainingType is { IsGenericType: true })
            return "JavaScript erases its type arguments, so the test could not tell its constructions apart";
        if (IsDisposable(contract)) return null;
        if (contract.Locations.Any(location => location.IsInSource)) return null;
        var space = contract.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (RuntimeProvidedTypeScanner.IsTranspiledNamespace(space)) return null;
        return RuntimeProvidedTypeScanner.IsRuntimeProvidedNamespace(space)
            ? "the runtime writes the classes that implement it by hand, and they declare no interface"
            : "values the browser holds as its own, a string, a number or an array, may implement it, and none of them could say so";
    }

    /// <summary>Whether <paramref name="contract"/> is <c>System.IDisposable</c>.</summary>
    private static bool IsDisposable(INamedTypeSymbol contract) =>
        contract is { Name: "IDisposable", Arity: 0, ContainingType: null }
        && contract.ContainingNamespace is { Name: "System", ContainingNamespace.IsGlobalNamespace: true };
}
