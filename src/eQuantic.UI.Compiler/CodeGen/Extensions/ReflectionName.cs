using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// A type's name as .NET's <c>Type.ToString()</c> writes it — <c>System.Comparison`1[System.Int32]</c>,
/// <c>App.Outer+Inner</c>, <c>System.Int32[]</c> — for a message .NET writes with a runtime type in it,
/// which the browser has no reflection to ask.
/// </summary>
internal static class ReflectionName
{
    internal static string Of(ITypeSymbol? type)
    {
        switch (type)
        {
            case null:
                return "";
            case IArrayTypeSymbol array:
                return $"{Of(array.ElementType)}[{new string(',', array.Rank - 1)}]";
            case ITypeParameterSymbol parameter:
                return parameter.Name;
            case INamedTypeSymbol named:
            {
                var name = named.MetadataName;
                for (var outer = named.ContainingType; outer is not null; outer = outer.ContainingType)
                    name = $"{outer.MetadataName}+{name}";
                var space = named.ContainingNamespace is { IsGlobalNamespace: false } ns ? ns.ToDisplayString() + "." : "";
                var arguments = named.TypeArguments.Length > 0 && !named.IsUnboundGenericType
                    ? $"[{string.Join(",", named.TypeArguments.Select(Of))}]"
                    : "";
                return space + name + arguments;
            }
            default:
                return type.ToDisplayString();
        }
    }
}
