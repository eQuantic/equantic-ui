using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// The text .NET writes for a framework exception built with no message, or with a null one, read
/// from .NET itself: eqc runs on the runtime the app runs on, so it builds the type with the
/// constructor the call binds, every argument null or empty, and reads its <c>Message</c>.
/// <para>
/// The runtime held a table of each type's text, written by hand, and a table is never complete: a
/// <c>TaskCanceledException</c> took its base's "The operation was canceled." where .NET writes "A
/// task was canceled.". Nor is the text the type's alone: <c>new SystemException()</c> writes "System
/// error." and <c>new SystemException(null)</c> writes "Exception of type 'System.SystemException' was
/// thrown.", so it is read per constructor.
/// </para>
/// </summary>
internal static class ExceptionDefaultMessage
{
    /// <summary>One read per constructor, keyed by its text so no compilation is kept alive.</summary>
    private static readonly ConcurrentDictionary<string, string?> Read = new(StringComparer.Ordinal);

    private static readonly SymbolDisplayFormat Qualified =
        new(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

    /// <summary>
    /// The text, or null where there is none to hand: a type .NET cannot build here, a constructor
    /// that throws on empty arguments, or <c>Exception.Message</c>'s own "Exception of type 'T' was
    /// thrown.", which the runtime writes itself, naming the type the app created.
    /// </summary>
    public static string? Of(IMethodSymbol constructor) =>
        Read.GetOrAdd(
            $"{constructor.ContainingType.ToDisplayString(Qualified)}({string.Join(",", constructor.Parameters.Select(p => NameOf(p.Type)))})",
            _ => Measure(constructor));

    private static string? Measure(IMethodSymbol constructor)
    {
        if (RuntimeType(constructor.ContainingType) is not { } type) return null;
        var built = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(candidate => Matches(candidate.GetParameters(), constructor.Parameters));
        if (built is null) return null;
        var culture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            var message = ((Exception)built.Invoke(built.GetParameters().Select(Empty).ToArray())).Message;
            return message == $"Exception of type '{type.FullName}' was thrown." ? null : message;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            CultureInfo.CurrentUICulture = culture;
        }
    }

    /// <summary>The runtime's type for a symbol, found by its metadata name in its assembly; the
    /// reference assembly a symbol comes from forwards to the one that implements it.</summary>
    private static Type? RuntimeType(INamedTypeSymbol symbol) =>
        symbol.IsGenericType
            ? null
            : Type.GetType($"{MetadataName(symbol)}, {symbol.ContainingAssembly.Identity.Name}", throwOnError: false);

    private static string MetadataName(INamedTypeSymbol symbol) =>
        symbol.ContainingType is { } outer ? $"{MetadataName(outer)}+{symbol.MetadataName}"
        : symbol.ContainingNamespace.IsGlobalNamespace ? symbol.MetadataName
        : $"{symbol.ContainingNamespace.ToDisplayString()}.{symbol.MetadataName}";

    private static bool Matches(ParameterInfo[] actual, ImmutableArray<IParameterSymbol> declared) =>
        actual.Length == declared.Length
        && actual.Zip(declared).All(pair => NameOf(pair.First.ParameterType) == NameOf(pair.Second.Type));

    private static string NameOf(Type type) =>
        type.IsArray ? NameOf(type.GetElementType()!) + "[]"
        : type.IsGenericType
            ? $"{type.Namespace}.{type.Name[..type.Name.IndexOf('`')]}<{string.Join(",", type.GetGenericArguments().Select(NameOf))}>"
        : (type.FullName ?? type.Name).Replace('+', '.');

    private static string NameOf(ITypeSymbol type) => type switch
    {
        IArrayTypeSymbol array => NameOf(array.ElementType) + "[]",
        INamedTypeSymbol { IsGenericType: true } generic =>
            $"{generic.ContainingNamespace.ToDisplayString()}.{generic.Name}<{string.Join(",", generic.TypeArguments.Select(NameOf))}>",
        _ => type.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString(Qualified),
    };

    /// <summary>An argument that says nothing: null, an empty sequence, or a value type's default.</summary>
    private static object? Empty(ParameterInfo parameter)
    {
        var type = parameter.ParameterType;
        if (type.IsArray) return Array.CreateInstance(type.GetElementType()!, 0);
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            return Array.CreateInstance(type.GetGenericArguments()[0], 0);
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}
