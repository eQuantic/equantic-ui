using eQuantic.UI.Compiler.CodeGen;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
/// <para>
/// Everything that is WRITTEN, that is. A name in code the twin does not carry is no name of the
/// module, and it is left out by the rule of whoever leaves the code out, asked through that
/// owner's own function and never repeated here: the parser's for server code, the
/// constant strategy's for a read written as its value, and <c>nameof</c> and an attribute, which
/// are text and metadata (<see cref="DefaultInterfaceMembers"/> reads a default's names the same
/// way). It matters when two types share a simple name. A page that shows the app's own
/// <c>Order</c> and names the Domain's <c>Order</c> only where nothing is written had the Domain's
/// reached, since its <c>./Order</c> could mean either: compiled for the browser, where its server
/// code is refused.
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
    /// <param name="declarations">The declarations a module is written from.</param>
    /// <param name="modelFor">The model of a declaration's own file.</param>
    /// <param name="asComponent">
    /// Whether the module is a component's, whose <c>[ServerAction]</c> crosses as a stub written from
    /// its signature. What server code names is not what the module names: a <c>[ServerOnly]</c> method
    /// is no member of any twin (#816), and a Server Action's body is no part of a component's. Read with
    /// the rest of the class, a Server Action that mapped the Domain's <c>Order</c> into the app's own
    /// <c>Order</c> had the Domain's reached: compiled for the browser, where its server code is
    /// refused, and in collision with the app's (found in review of #827).
    /// </param>
    public static NamedSourceTypes Of(IEnumerable<SyntaxNode> declarations, Func<SyntaxNode, SemanticModel?> modelFor,
        bool asComponent)
    {
        var named = new NamedSourceTypes();
        foreach (var declaration in declarations)
            if (modelFor(declaration) is { } model)
                named.Read(declaration, model, asComponent);
        return named;
    }

    /// <summary>
    /// A node that writes nothing of what it names, each by its owner's rule: an attribute is
    /// metadata, <c>nameof</c> is the text of a name, and a constant the strategy inlines is its
    /// value (<see cref="InlinedConstantStrategy.ConstantWrittenAsItsValue"/>).
    /// </summary>
    private static bool WritesNothing(SyntaxNode node, SemanticModel model) => node switch
    {
        AttributeListSyntax => true,
        InvocationExpressionSyntax invocation => invocation.IsNameOf(model),
        MemberAccessExpressionSyntax or IdentifierNameSyntax =>
            InlinedConstantStrategy.ConstantWrittenAsItsValue(node, model.GetSymbolInfo(node).Symbol) is not null,
        _ => false,
    };

    private void Read(SyntaxNode declaration, SemanticModel model, bool asComponent)
    {
        // Each node is asked once, and a node left out takes what is under it along.
        var pending = new Stack<SyntaxNode>();
        pending.Push(declaration);
        while (pending.TryPop(out var node))
        {
            try
            {
                // The code no twin is written from, by the parser's own rule.
                if (Parser.ComponentParser.StaysOnTheServer(node, asComponent, model)) continue;
                if (WritesNothing(node, model))
                {
                    // Nothing of what it NAMES is written. What is written in its place is a value,
                    // and C# still converts that value where it stands: a constant handed to
                    // `this[Slot slot]` is written `Slot.fromInt(1)`, a call like any other, on the
                    // type that declares the conversion. Left out with the node, the module called
                    // a type it never imported (#833). Only the node knows where it is converted:
                    // an indexer's symbol and a constructor initializer carry no parameter type.
                    if (node is ExpressionSyntax value) Symbol(model.GetConversion(value).MethodSymbol);
                    continue;
                }
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
            foreach (var child in node.ChildNodes()) pending.Push(child);
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
