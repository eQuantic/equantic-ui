using eQuantic.UI.Compiler.CodeGen;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// Every type declared in source that one declaration names, as the SYMBOLS say: each twin's name
/// with the files that declare it. It is what tells a module's <c>./Status</c> apart: the app's own
/// <c>Status</c>, or the one a referenced project declares.
/// <para>
/// It reads the whole of what the code can name, never a list of the ways to name it: every symbol a
/// name binds to with the type that declares it (a static member's owner, a constructor's type, the
/// static class an extension method is declared in, which the source never writes) and the type of
/// every expression (a target-typed <c>new(...)</c>, an inferred local, what a call returns), each
/// taken apart into its type arguments and elements. The emitters collect import candidates each in
/// its own way, and a candidate no walk of theirs recorded by symbol would be one this could not
/// place. So this reads everything and decides nothing: it only answers where a name is declared.
/// </para>
/// </summary>
internal sealed class NamedSourceTypes
{
    private readonly Dictionary<string, HashSet<ReferencedType>> _declarations = new(StringComparer.Ordinal);

    /// <summary>The declarations of the types whose twin is <paramref name="name"/>, as the
    /// declaration's symbols name them: each type with a file that declares it. None when no symbol
    /// of it names such a type.</summary>
    public IReadOnlyCollection<ReferencedType> Declarations(string name) =>
        _declarations.TryGetValue(name, out var declarations) ? declarations : [];

    /// <summary>The files that declare a type whose twin is <paramref name="name"/>.</summary>
    public IEnumerable<string> DeclaredIn(string name) =>
        Declarations(name).Select(declaration => declaration.File).Distinct(StringComparer.Ordinal);

    /// <summary>What <paramref name="declarations"/> name, each read with the model of its own file.</summary>
    public static NamedSourceTypes Of(IEnumerable<SyntaxNode> declarations, Func<SyntaxNode, SemanticModel?> modelFor)
    {
        var named = new NamedSourceTypes();
        foreach (var declaration in declarations)
            if (modelFor(declaration) is { } model)
                named.Read(declaration, model);
        return named;
    }

    private void Read(SyntaxNode declaration, SemanticModel model)
    {
        foreach (var node in declaration.DescendantNodesAndSelf())
        {
            try
            {
                switch (node)
                {
                    case ExpressionSyntax expression:
                        Symbol(model.GetSymbolInfo(expression).Symbol);
                        var type = model.GetTypeInfo(expression);
                        Type(type.Type);
                        Type(type.ConvertedType);
                        break;
                    case MemberDeclarationSyntax or ParameterSyntax or VariableDeclaratorSyntax:
                        Symbol(model.GetDeclaredSymbol(node));
                        break;
                }
            }
            catch
            {
                // A node the model cannot answer for names nothing this can place, as the import
                // sweep reads it (RuntimeProvidedTypeScanner): the conversion asks the same model
                // about the same node and reports what it cannot translate.
            }
        }
    }

    /// <summary>The types a symbol brings with it: the one that declares it, and the ones its own
    /// shape is written in.</summary>
    private void Symbol(ISymbol? symbol)
    {
        switch (symbol)
        {
            case null:
                return;
            case ITypeSymbol type:
                Type(type);
                return;
            case IMethodSymbol method:
                // A reduced extension call is written on its receiver, and its twin is called on the
                // static class that declares it.
                Type((method.ReducedFrom ?? method).ContainingType);
                Type(method.ReturnType);
                foreach (var parameter in method.Parameters) Type(parameter.Type);
                foreach (var argument in method.TypeArguments) Type(argument);
                return;
            case IPropertySymbol property:
                Type(property.ContainingType);
                Type(property.Type);
                return;
            case IFieldSymbol field:
                Type(field.ContainingType);
                Type(field.Type);
                return;
            case IEventSymbol @event:
                Type(@event.ContainingType);
                Type(@event.Type);
                return;
            case ILocalSymbol local:
                Type(local.Type);
                return;
            case IParameterSymbol parameter:
                Type(parameter.Type);
                return;
        }
    }

    private void Type(ITypeSymbol? type)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                Type(array.ElementType);
                return;
            case INamedTypeSymbol named:
                foreach (var argument in named.TypeArguments) Type(argument);
                // A nested type is named with its owners, and each of them is a type the code names.
                Type(named.ContainingType);
                var definition = named.OriginalDefinition;
                if (definition.Name.Length == 0) return;
                foreach (var reference in definition.DeclaringSyntaxReferences)
                {
                    if (string.IsNullOrEmpty(reference.SyntaxTree.FilePath)) continue;
                    var twin = definition.TwinReference();
                    if (!_declarations.TryGetValue(twin, out var declarations))
                        _declarations[twin] = declarations = [];
                    declarations.Add(new ReferencedType(twin, ClrName(definition), reference.SyntaxTree.FilePath));
                }
                return;
        }
    }

    /// <summary>The type's CLR name, as <see cref="Parser.ComponentParser.ClrIdentity"/> reads it
    /// off the declaration: the two are compared to find the declaration a symbol is.</summary>
    private static string ClrName(INamedTypeSymbol definition)
    {
        var name = definition.MetadataName;
        for (var owner = definition.ContainingType; owner is not null; owner = owner.ContainingType)
            name = $"{owner.MetadataName}+{name}";
        return definition.ContainingNamespace is { IsGlobalNamespace: false } space
            ? $"{space.ToDisplayString()}.{name}"
            : name;
    }
}
