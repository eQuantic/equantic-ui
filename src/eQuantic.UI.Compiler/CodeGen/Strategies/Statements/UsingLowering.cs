using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary>
/// The dispose a <c>using</c> promises, in the one shape both forms share: the statement form
/// (<c>using (var r = …) { }</c>) and the declaration form (<c>using var r = …;</c>, which owns
/// the REST of its block). A resource that never became disposable — a null, a twin without the
/// method — is skipped rather than thrown at, the way the runtime has always treated it.
/// </summary>
public static class UsingLowering
{
    public static JsStatement Dispose(string resource, bool isAsync)
    {
        var sync = JsStatement.If(
            JsExpr.Opaque($"{resource} && typeof {resource}.dispose === 'function'"),
            JsStatement.Block(new[] { JsStatement.Expression(JsExpr.Call(JsExpr.Member(JsExpr.Identifier(resource), "dispose"))) }),
            null);
        if (!isAsync) return sync;
        return JsStatement.If(
            JsExpr.Opaque($"{resource} && typeof {resource}.disposeAsync === 'function'"),
            JsStatement.Block(new[] { JsStatement.Expression(JsExpr.Opaque($"await {resource}.disposeAsync()")) }),
            sync);
    }

    internal static void Fence(VariableDeclaratorSyntax variable, TypeSyntax? declaredType, bool isAsync,
        ConversionContext context)
    {
        var type = (context.SemanticHelper.GetDeclaredSymbol(variable) as ILocalSymbol)?.Type
            ?? (variable.Initializer is { } init ? context.SemanticHelper.GetType(init.Value) : null)
            ?? (declaredType is not null && !declaredType.IsVar ? context.SemanticHelper.GetType(declaredType) : null);
        Fence(type, isAsync, variable.Initializer?.Value ?? (SyntaxNode)variable, context);
    }

    internal static void Fence(ITypeSymbol? resourceType, bool isAsync, SyntaxNode at, ConversionContext context)
    {
        if (resourceType.UnwrapNullable() is not INamedTypeSymbol type) return;
        var methodName = isAsync ? "DisposeAsync" : "Dispose";
        var interfaceName = isAsync ? "System.IAsyncDisposable" : "System.IDisposable";
        var contract = (context.ProjectCompilation ?? context.SemanticModel?.Compilation)
            ?.GetTypeByMetadataName(interfaceName)
            ?.GetMembers(methodName).OfType<IMethodSymbol>().FirstOrDefault();
        var method = type.TypeKind == TypeKind.Interface
            ? OwnDispose(type, methodName) ?? contract
            : (contract is not null && type.AllInterfaces.Any(face =>
                SymbolEqualityComparer.Default.Equals(face.OriginalDefinition, contract.ContainingType.OriginalDefinition))
                ? type.FindImplementationForInterfaceMember(contract) as IMethodSymbol
                : null) ?? OwnDispose(type, methodName);
        method?.ReportIfHostOnly(at, context, type);
    }

    private static IMethodSymbol? OwnDispose(INamedTypeSymbol type, string name)
    {
        for (var at = type; at is not null; at = at.BaseType)
            if (at.GetMembers(name).OfType<IMethodSymbol>()
                    .FirstOrDefault(method => method is { IsStatic: false, Parameters.Length: 0 }) is { } own)
                return own;
        return null;
    }
}
